using System;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using System.IO;

namespace ReVersionVCS_API_Lambdas
{
    public class S3Operations
    {

        private IAmazonS3 s3Client;

        public const string bucketPrefix = "ReVersionVCS";

        private RegionEndpoint bucketRegion;


        public S3Operations(string regionName)
        {
            bucketRegion = RegionEndpoint.GetBySystemName(regionName);
        }

        public async Task<PutBucketResponse> CreateS3BucketAsync(string repoName, string branchName)
        {
            string bucketName = GetBucketName(repoName, branchName);
            using (s3Client = new AmazonS3Client(bucketRegion))
            {
                var responseTask = await CreateBucketAsync(bucketName);
                return responseTask;
            }
        }

        public async Task<HttpStatusCode> DeleteBranchBucketsByRepoAsync(string repoName, List<string> branchNames)
        {
            using (s3Client = new AmazonS3Client(bucketRegion))
            {
                try
                {
                    foreach (var branch in branchNames)
                    {
                        string bucketName = GetBucketName(repoName, branch);
                        List<string> objects = await GetObjectKeysAsync(bucketName);
                        foreach (var item in objects)
                        {
                            await s3Client.DeleteObjectAsync(bucketName, item);
                        }
                        await s3Client.DeleteBucketAsync(bucketName);
                    }
                    return HttpStatusCode.OK;
                }
                catch(Exception)
                {
                    return HttpStatusCode.InternalServerError;
                }
            }
        }

        public async Task<HttpStatusCode> UpdateS3ObjectsAsync(List<FileData> files)
        {
            using (s3Client = new AmazonS3Client(bucketRegion))
            {
                try
                {
                    foreach (var item in files)
                    {
                        var response = await UpdateObjectAsync(item);
                    }
                    return HttpStatusCode.OK;
                }
                catch (AmazonS3Exception e)
                {
                    throw new AmazonS3Exception("Error writing an object", e);
                }
                catch (Exception e)
                {
                    throw new Exception("Unknown error encountered on server", e);
                    //return HttpStatusCode.InternalServerError;
                }
            }
        }

        public async Task<string> GetTextFromObjectAsync(string repoName, string branchName, string objectKey)
        {
            using (s3Client = new AmazonS3Client(bucketRegion))
            {
                var bucketName = GetBucketName(repoName, branchName);
                try
                {
                    return await GetObjectContentAsync(repoName, branchName, objectKey);
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }
        /*
        public async Task MergeIntoBucketAsync(string repoName, string branchName, string parentBranchName)
        {
            using (s3Client = new AmazonS3Client(bucketRegion))
            {
                // TODO TODO TODO TODO





                throw new NotImplementedException();
            }
        }
        */



        private async Task<PutBucketResponse> CreateBucketAsync(string bucketName)
        {
            try
            {
                if (!(await AmazonS3Util.DoesS3BucketExistAsync(s3Client, bucketName)))
                {
                    var putBucketRequest = new PutBucketRequest
                    {
                        BucketName = bucketName,
                        UseClientRegion = true
                    };

                    PutBucketResponse putBucketResponse = await s3Client.PutBucketAsync(putBucketRequest);
                    return putBucketResponse;
                }
                else
                {
                    return new PutBucketResponse { HttpStatusCode = HttpStatusCode.Conflict };
                }
            }
            catch (Exception)
            {
                return new PutBucketResponse { HttpStatusCode = HttpStatusCode.InternalServerError };
            }
        }

        private async Task<string> FindBucketLocationAsync(string repositoryName, string branchName)
        {
            var request = new GetBucketLocationRequest()
            {
                BucketName = GetBucketName(repositoryName, branchName)
            };
            GetBucketLocationResponse response = await s3Client.GetBucketLocationAsync(request);
            return response.Location.ToString();
        }

        private async Task<List<string>> GetObjectKeysAsync(string bucketName, string prefix = null)
        {
            ListObjectsRequest request =
                (string.IsNullOrEmpty(prefix)) ?
                new ListObjectsRequest { BucketName = bucketName } :
                new ListObjectsRequest { BucketName = bucketName, Prefix = prefix };

            List<string> bucketList = new List<string>();
            ListObjectsResponse response;
            do
            {
                response = await s3Client.ListObjectsAsync(request);
                bucketList.AddRange( response.S3Objects.Select(x => x.Key) );
                request.Marker = response.NextMarker;
            } while (response.IsTruncated);

            return bucketList;
        }

        private async Task<PutObjectResponse> UpdateObjectAsync(FileData file)
        {
            try
            {
                var findResponse = await FindBucketLocationAsync(file.RepositoryName, file.BranchName);
            }
            catch(Exception e)
            {
                throw new Exception($"did not find bucket: {GetBucketName(file.RepositoryName, file.BranchName)}", e);
            }
            var request = new PutObjectRequest
            {
                BucketName = GetBucketName(file.RepositoryName, file.BranchName),
                Key = file.ObjectKey,
                ContentBody = file.Content
            };

            PutObjectResponse response = await s3Client.PutObjectAsync(request);
            return response;
        }

        private async Task<string> GetObjectContentAsync(string repoName, string branchName, string objectKey)
        {
            GetObjectRequest request = new GetObjectRequest
            {
                BucketName = GetBucketName(repoName, branchName),
                Key = objectKey
            };
            using (GetObjectResponse response = await s3Client.GetObjectAsync(request))
            using (Stream responseStream = response.ResponseStream)
            using (StreamReader reader = new StreamReader(responseStream))
            {
                string responseBody = await reader.ReadToEndAsync();

                return responseBody;
            }
        }

        private string GetBucketName(string repoName, string branchName)
            => (bucketPrefix + '.' + repoName + '.' + branchName).ToLower();


    }
}
