using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ReVersionVCS_API_Lambdas.Models;
using ReVersionVCS_API_Lambdas.Response_Objects;
using ReVersionVCS_API_Lambdas.Request_Objects;
using System.Text.RegularExpressions;
using static ReVersionVCS_API_Lambdas.SQLOperations;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace ReVersionVCS_API_Lambdas
{
    public partial class Functions
    {
        private readonly JsonSerializerSettings jsonSerializerSettings;

        private S3Operations s3Ops;

        
        const string S3_REGION_ENVIRONMENT_VARIABLE_LOOKUP = "S3_REGION";

        public const string ID_QUERY_STRING_NAME = "Id";
        
        /// <summary>
        /// Default constructor that Lambda will invoke.
        /// </summary>
        public Functions()
        {
            // set jsonSerializerSettings to properly handle Camel-Case Names
            //  i.e. the JSON property "fooBar" serializes to the C# property "FooBar"
            DefaultContractResolver contractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            };
            jsonSerializerSettings = new JsonSerializerSettings
            {
                ContractResolver = contractResolver
            };
        }


        // TODO: add authorization check and return 401 (HttpStatusCode.Unauthorized) for everything (unless this is handled by API Gatway)
        // TODO: same for 500 code (HttpStatusCode.InternalServerError)
        /// <summary>
        /// A Lambda function that gets a list of all of the repositories
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> GetRepositoriesAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            return 
                await Task.Run(() =>
                    {
                        using (ReVersion_DatabaseContext db = new ReVersion_DatabaseContext())
                        {
                            List<RepositoryLookup> repositoryNames = QueryRepositories(db);
                            List<ResourceItem> repositoryResponse = repositoryNames.Select(x => new ResourceItem
                            {
                                DisplayData = x.Name,
                                Owner = x.Owner,
                                Href = request.Path + $"/{x.Name}"
                            }).ToList();

                            var response = new APIGatewayProxyResponse
                            {
                                StatusCode = (int)HttpStatusCode.OK,
                                Body = JsonConvert.SerializeObject(new { Resources = repositoryResponse }, jsonSerializerSettings),
                                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                            };

                            return response;
                        }
                    });
        }

        /// <summary>
        /// A Lambda function that creates a new repository
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> CreateRepositoryAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            using (var db = new ReVersion_DatabaseContext())
            {
                s3Ops = new S3Operations(Environment.GetEnvironmentVariable(S3_REGION_ENVIRONMENT_VARIABLE_LOOKUP));
                
                RepositoryData requestBody;
                try
                {
                    requestBody = JsonConvert.DeserializeObject<RepositoryData>(request.Body, jsonSerializerSettings);
                }
                catch (Exception)
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest
                    };
                }

                string repo = requestBody.RepositoryId;
                string user = requestBody.UserName;
                string bucketname = S3Operations.bucketPrefix + '.' + repo.ToLower() + ".main";
                if ( !BucketNameValid(db, bucketname) || !UsernameExists(db, user))
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest
                    };
                }

                if (RepositoryExists(db, repo))
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.Conflict
                    };
                }

                InsertIntoRepoTable(db, repo, user);
                InsertIntoBranchTable(db, repo, "main");
                InsertIntoRepoPermissionsTable(db, repo, user);
                await s3Ops.CreateS3BucketAsync(repo, "main");
                InsertIntoEventLog(db, repo, "main", user, requestBody.Message, "create_repository");

                var responseObject = new ResourceItem
                {
                    DisplayData = repo,
                    Href = request.Path + $"/{repo}",
                    Owner = user
                };

                await db.SaveChangesAsync();
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.Created,
                    Body = JsonConvert.SerializeObject(responseObject),
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                };
            }
        }

        /// <summary>
        /// A Lambda function that deletes an entire repository
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> DeleteRepositoryAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {

            using (var db = new ReVersion_DatabaseContext())
            {
                s3Ops = new S3Operations(Environment.GetEnvironmentVariable(S3_REGION_ENVIRONMENT_VARIABLE_LOOKUP));

                if (request.PathParameters == null || !request.PathParameters.ContainsKey("repositoryId"))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };

                string repo;
                if (!request.PathParameters.TryGetValue("repositoryId", out repo))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };

                if (!RepositoryExists(db, repo))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.NotFound };

                List<BranchLookup> branches = QueryBranches(db, repo);

                foreach (var branch in branches)
                {
                    if (branch.Locked)
                        return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.Conflict };
                }

                DeleteAllFromBranchesTable(db, repo);
                DeleteAllFromVersionsTable(db, repo);
                DeleteAllFromPermissionsTable(db, repo);
                DeleteAllFromPermissionRequestsTable(db, repo);
                DeleteFromRepositoryTable(db, repo);

                await db.SaveChangesAsync();
                return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.NoContent };
            }
        }

        /// <summary>
        /// A Lambda function that gets a list of all of the branches in the repository
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> GetBranchesAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            return
                await Task.Run(() =>
                    {
                        using (var db = new ReVersion_DatabaseContext())
                        {
                            if (request.PathParameters == null || !request.PathParameters.ContainsKey("repositoryId"))
                                return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };

                            string repo;
                            if (!request.PathParameters.TryGetValue("repositoryId", out repo))
                                return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };

                            if (!RepositoryExists(db, repo))
                                return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.NotFound };

                            List<BranchLookup> branches = QueryBranches(db, repo);

                            List<ResourceItem> responseObject =
                                (from branch in branches
                                 select new ResourceItem
                                 {
                                     DisplayData = branch.BranchName,
                                     Href = request.Path + $"/{branch.BranchName}"
                                 }).ToList();

                            string responseContent = JsonConvert.SerializeObject(new { Resources = responseObject }, jsonSerializerSettings);

                            return new APIGatewayProxyResponse
                            {
                                StatusCode = (int)HttpStatusCode.OK,
                                Body = responseContent,
                                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                            };
                        }
                    }
                );
        }

        /// <summary>
        /// A Lambda function that creates a new branch within the repository
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> CreateBranchAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            using (var db = new ReVersion_DatabaseContext())
            {
                s3Ops = new S3Operations(Environment.GetEnvironmentVariable(S3_REGION_ENVIRONMENT_VARIABLE_LOOKUP));
                if (request.PathParameters == null || !request.PathParameters.ContainsKey("repositoryId"))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };

                string repo;
                if (!request.PathParameters.TryGetValue("repositoryId", out repo))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };

                if (!RepositoryExists(db, repo))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.NotFound };

                NewBranchData requestBody;
                try
                {
                    requestBody = JsonConvert.DeserializeObject<NewBranchData>(request.Body);
                }
                catch (Exception)
                {
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };
                }

                if (!UsernameExists(db, requestBody.UserName))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.BadRequest };

                if (BranchExists(db, repo, requestBody.BranchId))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.Conflict };

                InsertIntoBranchTable(db, repo, requestBody.BranchId);
                await s3Ops.CreateS3BucketAsync(repo, requestBody.BranchId);
                InsertIntoEventLog(db, repo, requestBody.BranchId, requestBody.UserName, requestBody.Message, "create_branch");

                var responseObject = new ResourceItem
                {
                    DisplayData = requestBody.BranchId,
                    Href = request.Path + $"/{requestBody.BranchId}"
                };

                var responseContent = JsonConvert.SerializeObject(responseObject);

                await db.SaveChangesAsync();
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.Created,
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } },
                    Body = responseContent
                };
            }
        }

        /// <summary>
        /// A Lambda function that gets a list of all of the objects within a branch
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> GetBranchAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            return
                await Task.Run(() =>
                {
                    using (var db = new ReVersion_DatabaseContext())
                    {
                        context.Logger.Log("start of GetBranchAsync function");
                        if (request.PathParameters == null || !request.PathParameters.ContainsKey("repositoryId")
                            || !request.PathParameters.ContainsKey("branchId"))
                            return new APIGatewayProxyResponse
                            {
                                StatusCode = (int)HttpStatusCode.InternalServerError,
                                Body = "path parameters are empty or missing repositoryId or branchId"
                            };

                        context.Logger.Log("Passed the first check repositoryId and branchId exist");

                        string repo;
                        string branch;
                        if (!request.PathParameters.TryGetValue("repositoryId", out repo)
                            || !request.PathParameters.TryGetValue("branchId", out branch))
                            return new APIGatewayProxyResponse
                            {
                                StatusCode = (int)HttpStatusCode.InternalServerError,
                                Body = "missing repositoryId or branchId in the second check"
                            };

                        context.Logger.Log($"Passed the second check. repositoryId = {repo} and branchId = {branch}");

                        if (!BranchExists(db, repo, branch))
                            return new APIGatewayProxyResponse
                            {
                                StatusCode = (int)HttpStatusCode.NotFound,
                                Body = $"Branch does not exist for branch = {branch} and repository = {repo}"
                            };

                        context.Logger.Log($"Determined that branch exists for repository {repo} and branch {branch}");

                        HierarchyNode branchFiles = QueryHierarchy(db, repo, branch);

                        context.Logger.Log($"Successfully queried branch hierarchy from repository {repo} and branch {branch}");

                        return new APIGatewayProxyResponse
                        {
                            StatusCode = (int)HttpStatusCode.OK,
                            Body = JsonConvert.SerializeObject(branchFiles),
                            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                        };
                    }
                });
        }

        /// <summary>
        /// A Lambda function that commits all local changes to a branch
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> CommitChangesAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            return await Task.Run(() =>
            {
                s3Ops = new S3Operations(Environment.GetEnvironmentVariable(S3_REGION_ENVIRONMENT_VARIABLE_LOOKUP));

                return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };
            });
            // TODO
            //throw new NotImplementedException();
        }

        /// <summary>
        /// A Lambda function that gets a file within a branch
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> GetObjectAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            using (var db = new ReVersion_DatabaseContext())
            {
                s3Ops = new S3Operations(Environment.GetEnvironmentVariable(S3_REGION_ENVIRONMENT_VARIABLE_LOOKUP));

                if (request.PathParameters == null || !request.PathParameters.ContainsKey("repositoryId")
                    || !request.PathParameters.ContainsKey("branchId")
                    || !request.QueryStringParameters.ContainsKey("awsObjectKey"))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };
                string repo;
                string branch;
                string objectKey;
                if (!request.PathParameters.TryGetValue("repositoryId", out repo)
                    || !request.PathParameters.TryGetValue("branchId", out branch)
                    || !request.PathParameters.TryGetValue("awsObjectKey", out objectKey))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };

                if (!BranchExists(db, repo, branch))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.NotFound };

                string responseContent = await s3Ops.GetTextFromObjectAsync(repo, branch, objectKey);

                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.OK,
                    Body = responseContent,
                    Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                };
            }
        }

        /// <summary>
        /// A Lambda function that gets a list of all of the versions within a branch
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> GetVersionsAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            return await Task.Run(() =>
            {
                using (var db = new ReVersion_DatabaseContext())
                {
                    if (request.PathParameters == null || !request.PathParameters.ContainsKey("repositoryId")
                        || !request.PathParameters.ContainsKey("branchId")
                        || !request.QueryStringParameters.ContainsKey("awsObjectKey"))
                        return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };
                    string repo;
                    string branch;
                    if (!request.PathParameters.TryGetValue("repositoryId", out repo)
                        || !request.PathParameters.TryGetValue("branchId", out branch))
                        return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };

                    if (!BranchExists(db, repo, branch))
                        return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.NotFound };

                    var versionList = QueryVersions(db, repo, branch);
                    List<ResourceItem> responseContent =
                        versionList.Select(x => new ResourceItem
                        {
                            DisplayData = $"{x}",
                            Href = $"{request.Path}/{x}"
                        }).ToList();

                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.OK,
                        Body = JsonConvert.SerializeObject(new { Resources = responseContent }),
                        Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                    };
                }
            });
        }

        /// <summary>
        /// A Lambda function that gets a list of all of the files within a version of a branch
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> GetVersionAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            return await Task.Run(() =>
            {
                using (var db = new ReVersion_DatabaseContext())
                {
                    if (request.PathParameters == null || !request.PathParameters.ContainsKey("repositoryId")
                        || !request.PathParameters.ContainsKey("branchId")
                        || !request.QueryStringParameters.ContainsKey("versionId"))
                        return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };
                    string repo;
                    string branch;
                    string version;
                    if (!request.PathParameters.TryGetValue("repositoryId", out repo)
                        || !request.PathParameters.TryGetValue("branchId", out branch)
                        || !request.PathParameters.TryGetValue("versionId", out version))
                        return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };

                    if (!VersionExists(db, repo, branch, version))
                        return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.NotFound };

                    HierarchyNode versionFiles = QueryHierarchy(db, repo, branch, version);

                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.OK,
                        Body = JsonConvert.SerializeObject(versionFiles),
                        Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                    };
                }
            });
        }

        /// <summary>
        /// A Lambda function that gets a file from a non-current version of a branch
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> GetPastObjectAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            return await Task.Run(() =>
            {
                s3Ops = new S3Operations(Environment.GetEnvironmentVariable(S3_REGION_ENVIRONMENT_VARIABLE_LOOKUP));
                return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };
            });
            // TODO
            //throw new NotImplementedException();
        }

        /// <summary>
        /// A Lambda function that merges a branch with its parent
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> MergeBranchAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            return await Task.Run(() =>
            {
                s3Ops = new S3Operations(Environment.GetEnvironmentVariable(S3_REGION_ENVIRONMENT_VARIABLE_LOOKUP));
                return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };
            });
            // TODO
            //throw new NotImplementedException();
        }

        /// <summary>
        /// A Lambda function that places a lock on a branch
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> LockBranchAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            using (var db = new ReVersion_DatabaseContext())
            {
                if (request.PathParameters == null || !request.PathParameters.ContainsKey("repositoryId")
                    || !request.PathParameters.ContainsKey("branchId"))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };

                string repo;
                string branch;

                if (!request.PathParameters.TryGetValue("repositoryId", out repo)
                    || !request.PathParameters.TryGetValue("branchId", out branch))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };

                LogData requestBody;
                try
                {
                    requestBody = JsonConvert.DeserializeObject<LogData>(request.Body);
                }
                catch (Exception)
                {
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };
                }

                if (!UsernameExists(db, requestBody.UserName))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.BadRequest };

                if (!BranchExists(db, repo, branch))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.NotFound };

                LockState lockState = AcquireLock(db, repo, branch);

                if (lockState == LockState.AlreadyLockedConflict)
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.Conflict };

                if (lockState != LockState.SuccessfulLockOperation)
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };

                InsertIntoEventLog(db, repo, branch, requestBody.UserName, requestBody.Message, "place_lock");

                LockData responseObject = QueryLockEventByBranch(db, repo, branch);

                await db.SaveChangesAsync();
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.OK,
                    Body = JsonConvert.SerializeObject(responseObject),
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                };
            }
        }

        /// <summary>
        /// A Lambda function that "safely" removes a lock from a branch (only works if the same user set the lock)
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> UnlockBranchAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            using (var db = new ReVersion_DatabaseContext())
            {
                if (request.PathParameters == null || !request.PathParameters.ContainsKey("repositoryId")
                    || !request.PathParameters.ContainsKey("branchId"))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };

                string repo;
                string branch;

                if (!request.PathParameters.TryGetValue("repositoryId", out repo)
                    || !request.PathParameters.TryGetValue("branchId", out branch))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };

                LogData requestBody;
                try
                {
                    requestBody = JsonConvert.DeserializeObject<LogData>(request.Body);
                }
                catch (Exception)
                {
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };
                }

                if (!UsernameExists(db, requestBody.UserName))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.BadRequest };

                if (!BranchExists(db, repo, branch))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.NotFound };

                LockState lockState = SafeReleaseLock(db, repo, branch, requestBody.UserName);

                if (lockState == LockState.AlreadyUnlockedConflict || lockState == LockState.LockedByDifferentUser)
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.Conflict };

                if (lockState != LockState.SuccessfulLockOperation)
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };


                InsertIntoEventLog(db, repo, branch, requestBody.UserName, requestBody.Message, "safely_remove_lock");

                LockData responseObject = QueryLockEventByBranch(db, repo, branch);

                await db.SaveChangesAsync();
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.OK,
                    Body = JsonConvert.SerializeObject(responseObject),
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                };
            }
        }

        /// <summary>
        /// A Lambda function that "unsafely" removes a lock from a branch (any user can do this)
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> BreakLockAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            using (var db = new ReVersion_DatabaseContext())
            {
                if (request.PathParameters == null || !request.PathParameters.ContainsKey("repositoryId")
                    || !request.PathParameters.ContainsKey("branchId"))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };

                string repo;
                string branch;

                if (!request.PathParameters.TryGetValue("repositoryId", out repo)
                    || !request.PathParameters.TryGetValue("branchId", out branch))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };

                LogData requestBody;
                try
                {
                    requestBody = JsonConvert.DeserializeObject<LogData>(request.Body);
                }
                catch (Exception)
                {
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };
                }

                if (!UsernameExists(db, requestBody.UserName))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.BadRequest };

                if (!BranchExists(db, repo, branch))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.NotFound };

                LockState lockState = ReleaseLock(db, repo, branch);

                if (lockState == LockState.AlreadyUnlockedConflict)
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.Conflict };

                if (lockState != LockState.SuccessfulLockOperation)
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };


                InsertIntoEventLog(db, repo, branch, requestBody.UserName, requestBody.Message, "force_remove_lock");

                LockData responseObject = QueryLockEventByBranch(db, repo, branch);

                await db.SaveChangesAsync();
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.OK,
                    Body = JsonConvert.SerializeObject(responseObject),
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                };
            }
        }

        /// <summary>
        /// A Lambda function that requests permission for a user to access a repository
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> RequestPermissionAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            using (var db = new ReVersion_DatabaseContext())
            {
                if (request.PathParameters == null || !request.PathParameters.ContainsKey("repositoryId"))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };

                string repo;

                if (!request.PathParameters.TryGetValue("repositoryId", out repo))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };

                LogData requestBody;
                try
                {
                    requestBody = JsonConvert.DeserializeObject<LogData>(request.Body);
                }
                catch (Exception)
                {
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };
                }

                if (!UsernameExists(db, requestBody.UserName))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.BadRequest };

                if (!RepositoryExists(db, repo))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.NotFound };

                if (UserCanAccessRepository(db, requestBody.UserName, repo))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.Conflict };

                if (PermissionRequestIsLogged(db, repo, requestBody.UserName))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.NoContent };

                InsertIntoEventLog(db, repo, "main", requestBody.UserName, requestBody.Message, "request_permission");
                int eventId = QueryLastEventIdByUser(db, requestBody.UserName);

                InsertIntoPermissionRequestTable(db, repo, eventId);

                await db.SaveChangesAsync();
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.NoContent,
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                };
            }
        }


        // TODO: in the client-side, you need to remember to add /grant and /deny to all of the resource URLs!!!

        /// <summary>
        /// A Lambda function that gets a list of all of the unanswered permission requests submitted
        /// to access all of the repositories owned by a user
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> GetPermissionRequestsAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            return await Task.Run(() =>
            {
                context.Logger.Log("entering function");
                using (var db = new ReVersion_DatabaseContext())
                {
                    if (request.Headers == null || !request.Headers.ContainsKey("username"))
                        return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };

                    context.Logger.Log("passed initial parameter check");

                    string user;
                    if (!request.Headers.TryGetValue("username", out user))
                        return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.BadRequest };

                    context.Logger.Log($"username is {user}");

                    List<PermissionLookup> permissions = QueryPermissionRequests(db, user);

                    context.Logger.Log("okay, i just made a database call");

                    List<ResourceItem> responseObject =
                        permissions.Select(x => new ResourceItem
                        {
                            DisplayData = $"{x.RequestingUser} requested access to {x.RepositoryName} on " + x.LogTimestamp.ToString(@"MM/dd/yy H:mm tt"),
                            Href = $"repositories/{x.RequestId}"
                        }).ToList();

                    context.Logger.Log("i just made the response object");

                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.OK,
                        Body = JsonConvert.SerializeObject(new { Resources = responseObject }),
                        Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                    };
                }
            });
        }

        /// <summary>
        /// A Lambda function that grants a user's request to access a repository
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> GrantPermissionAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            using (var db = new ReVersion_DatabaseContext())
            {
                if (request.PathParameters == null || !request.PathParameters.ContainsKey("requestId"))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };

                if (!request.PathParameters.TryGetValue("requestId", out string permissionRequest))
                {
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };
                }

                int requestId;
                try
                {
                    requestId = Convert.ToInt32(permissionRequest);
                }
                catch (Exception)
                {
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.BadRequest };
                }


                LogData requestBody;
                try
                {
                    requestBody = JsonConvert.DeserializeObject<LogData>(request.Body);
                }
                catch (Exception)
                {
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };
                }

                if (!UsernameExists(db, requestBody.UserName))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.BadRequest };

                string repo = QueryRepositoryNameFromRequest(db, requestId);

                if (!UserOwnsRepository(db, repo, requestBody.UserName))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.Forbidden };

                InsertIntoRepoPermissionsTable(db, repo, requestBody.UserName);
                DeleteFromPermissionRequestTable(db, requestId);
                InsertIntoEventLog(db, repo, "main", requestBody.UserName, requestBody.Message, "grant_permission");

                await db.SaveChangesAsync();
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.NoContent,
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                };
            }
        }

        /// <summary>
        /// A Lambda function that rejects a user's request to access a repository
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> DenyPermissionAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            using (var db = new ReVersion_DatabaseContext())
            {
                if (request.PathParameters == null || !request.PathParameters.ContainsKey("requestId"))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };

                if (!request.PathParameters.TryGetValue("requestId", out string permissionRequest))
                {
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };
                }

                int requestId;
                try
                {
                    requestId = Convert.ToInt32(permissionRequest);
                }
                catch (Exception)
                {
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.BadRequest };
                }


                LogData requestBody;
                try
                {
                    requestBody = JsonConvert.DeserializeObject<LogData>(request.Body);
                }
                catch (Exception)
                {
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.InternalServerError };
                }

                if (!UsernameExists(db, requestBody.UserName))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.BadRequest };

                string repo = QueryRepositoryNameFromRequest(db, requestId);

                if (!UserOwnsRepository(db, repo, requestBody.UserName))
                    return new APIGatewayProxyResponse { StatusCode = (int)HttpStatusCode.Forbidden };

                DeleteFromPermissionRequestTable(db, requestId);
                InsertIntoEventLog(db, repo, "main", requestBody.UserName, requestBody.Message, "deny_permission");

                await db.SaveChangesAsync();
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.NoContent,
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                };
            }
        }


        /// <exception cref="ArgumentNullException">from call to Convert.FromBase64String</exception>
        /// <exception cref="FormatException">from call to Convert.FromBase64String if not in base64 format</exception>
        /// <exception cref="ArgumentException">from call to Encoding.GetString if byte array contains invalid Unicode code points</exception>
        /// <exception cref="DecoderFallbackException">if fallback occurred due to character encodinng in .NET
        ///                                        AND "DecoderFallback" is set to "DecoderExceptionFallback"</exception>
        private string DecodeBase64String(string str)
            => Encoding.UTF8.GetString(Convert.FromBase64String(str));

        private string EncodeBase64String(string str) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(str));

        private bool BucketNameValid(ReVersion_DatabaseContext db, string bucketName)
        {
            var nameArray = bucketName.Split('.');
            if (nameArray.Length != 3) return false;
            if (!nameArray[0].Equals(S3Operations.bucketPrefix)) return false;
            /*
            if (nameArray[2].ToLower().Equals("main") && RepositoryExists(db, nameArray[1])) return false;
            if (!nameArray[2].ToLower().Equals("main") && BranchExists(db, nameArray[1], nameArray[2])) return false;
            */
            foreach (string str in nameArray)
            {
                if (Regex.IsMatch(str, @"[\s._]")) return false;
                if (str.Length > 25) return false;
                if (!Regex.IsMatch(str, @"^[a-zA-Z0-9][a-zA-Z0-9-]*[a-zA-Z0-9]$")) return false;
            }

            return true;
        }

    }
}
