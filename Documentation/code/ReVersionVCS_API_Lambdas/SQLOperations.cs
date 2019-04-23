using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ReVersionVCS_API_Lambdas.Models;

namespace ReVersionVCS_API_Lambdas
{
    public static partial class SQLOperations
    {
        public static List<RepositoryLookup> QueryRepositories(ReVersion_DatabaseContext db) =>
            (from repository in db.Repositories
             join user in db.Users on repository.Owner equals user.Id
             select new RepositoryLookup { Name = repository.Name, Owner = user.UserName })
            .ToList();

        // NOT FULLY TESTED
        // updated version not yet tested, should work fine
        // (except maybe the case with the "versionId" parameter specified
        public static HierarchyNode QueryHierarchy(ReVersion_DatabaseContext db, string repoName, string branchName, string versionId = null)
        {
            var latestHierarchyJson =
                from repository in db.Repositories
                join branch in db.Branches on repository.Id equals branch.RepositoryId
                where repository.Name == repoName
                && branch.Name == branchName
                select branch.LatestFileHierarchy;

            var versionHierarchyJson =
                from repository in db.Repositories
                join branch in db.Branches on repository.Id equals branch.RepositoryId
                join version in db.Versions on branch.Id equals version.BranchId
                where repository.Name.Equals(repoName)
                && branch.Name.Equals(branchName)
                && version.VersionNumber.Equals(versionId ?? "-1")
                select version.FileHierarchy;

            string hierarchyJson = string.IsNullOrEmpty(versionId) ?
                                        latestHierarchyJson.Single() :
                                        versionHierarchyJson.Single();

            FileHierarchyData hierarchyData = new FileHierarchyData(hierarchyJson);

            return hierarchyData.GetHierarchyList();
        }

        public static List<BranchLookup> QueryBranches(ReVersion_DatabaseContext db, string repoName) =>
                (from repository in db.Repositories
                 join branch in db.Branches
                 on repository.Id equals branch.RepositoryId
                 where repository.Name == repoName
                 select new BranchLookup { Locked = branch.Locked, BranchName = branch.Name })
                    .ToList();
        
        public static int QueryBranchId(ReVersion_DatabaseContext db, string repoName, string branchName) =>
            (from repository in db.Repositories
             join branch in db.Branches
             on repository.Id equals branch.RepositoryId
             where repository.Name == repoName
                 && branch.Name == branchName
             select branch.Id)
                 .Single();

        public static List<int> QueryVersions(ReVersion_DatabaseContext db, string repoName, string branchName) =>
            (from repository in db.Repositories
             join branch in db.Branches on repository.Id equals branch.RepositoryId
             where repository.Name == repoName && branch.Name == branchName
             join version in db.Versions on branch.Id equals version.BranchId
             select version.VersionNumber)
                .ToList();

        public static bool UserCanAccessRepository(ReVersion_DatabaseContext db, string username, string repoName) =>
            QueryPermissions(db, username).Contains(repoName);
        
        public static List<string> QueryPermissions(ReVersion_DatabaseContext db, string username) =>
            (from user in db.Users
             where user.UserName == username
             join permission in db.RepositoryPermissions on user.Id equals permission.PermittedUser
             join repository in db.Repositories on permission.RepositoryId equals repository.Id
             select repository.Name)
            .ToList();

        public static List<PermissionLookup> QueryPermissionRequests(ReVersion_DatabaseContext db, string username)
        {
            var query =
                from user in db.Users
                where user.UserName == username
                join repository in db.Repositories on user.Id equals repository.Owner
                join request in db.PermissionRequests on repository.Id equals request.RepositoryId
                join log in db.EventLogs on request.EventId equals log.Id
                join requestUser in db.Users on log.UserId equals requestUser.Id
                select new PermissionLookup
                {
                    RequestId = request.Id,
                    RequestingUser = requestUser.UserName,
                    RepositoryName = repository.Name,
                    Message = log.Message,
                    LogTimestamp = log.LoggedAt
                };

            return query.ToList();
        }

        public static string QueryRepositoryNameFromRequest(ReVersion_DatabaseContext db, int requestId)
        {
            int trunkId = QueryMainTrunkFromRequest(db, requestId);
            return
               (from repository in db.Repositories
                join request in db.PermissionRequests on repository.Id equals request.RepositoryId
                where request.Id.Equals(requestId)
                select repository.Name)
               .Single();
        }

        public static int QueryMainTrunkFromRequest(ReVersion_DatabaseContext db, int requestId) =>
            (from request in db.PermissionRequests
             where request.Id == requestId
             join log in db.EventLogs on request.EventId equals log.Id
             join branch in db.Branches on log.BranchId equals branch.Id
             select branch.Id)
            .Single();

        public static int QueryLastEventIdByUser(ReVersion_DatabaseContext db, string username) =>
            (from log in db.EventLogs
             join user in db.Users on log.UserId equals user.Id
             where user.UserName == username
             orderby log.LoggedAt descending, log.Id descending
             select log.Id)
            .First();
        
        public static int QueryUserFromRequest(ReVersion_DatabaseContext db, int requestId) =>
            (from request in db.PermissionRequests
             where request.Id == requestId
             join log in db.EventLogs on request.EventId equals log.Id
             select log.UserId)
            .Single();

        public static bool BranchIsLocked(ReVersion_DatabaseContext db, string repoName, string branchName) =>
                (from repository in db.Repositories
                 join branch in db.Branches on repository.Id equals branch.RepositoryId
                 where  repository.Name == repoName
                 && branch.Name == branchName
                 select branch.Locked)
                .Single();

        // Untested
        public static bool PermissionRequestIsLogged(ReVersion_DatabaseContext db, string repoName, string username) =>
            (from request in db.PermissionRequests
             join repository in db.Repositories on request.RepositoryId equals repository.Id
             join branch in db.Branches on repository.Id equals branch.RepositoryId
             join log in db.EventLogs on branch.Id equals log.BranchId
             join user in db.Users on log.UserId equals user.Id
             where repository.Name.Equals(repoName)
             && branch.Name.Equals("main")
             && user.UserName.Equals(username)
             && log.Type.Equals("request_permission")
             select log.Id)
            .Any();

        public static bool UserOwnsRepository(ReVersion_DatabaseContext db, string repo, string username) =>
            (from repository in db.Repositories
             join user in db.Users on repository.Owner equals user.Id
             where repository.Name.Equals(repo)
             && user.UserName.Equals(username)
             select user.Id).Any();

        public static LockData QueryLockEventByBranch(ReVersion_DatabaseContext db, string repoName, string branchName) =>
            (from repository in db.Repositories
             join branch in db.Branches on repository.Id equals branch.RepositoryId
             join log in db.EventLogs on branch.Id equals log.BranchId
             join user in db.Users on log.UserId equals user.Id
             where repository.Name.Equals(repoName)
             && branch.Name.Equals(branchName)
             orderby log.LoggedAt descending, log.Id descending
             select new LockData
             {
                 UserName = user.UserName,
                 Message = log.Message,
                 Timestamp = log.LoggedAt,
                 LockedBranchId = branch.Name
             }).First();

        public static bool UserCanAccessBranch(ReVersion_DatabaseContext db, string repoName, string branchName, string username) =>
            (from repository in db.Repositories
             join branch in db.Branches on repository.Id equals branch.RepositoryId
             join permission in db.RepositoryPermissions on repository.Id equals permission.RepositoryId
             join user in db.Users on permission.PermittedUser equals user.Id
             where repository.Name.Equals(repoName)
             && branch.Name.Equals(branchName)
             && user.UserName.Equals(username)
             select user.Id).Any();

        public async static Task<bool> RequestExistsAsync(ReVersion_DatabaseContext db, int requestId) =>
            await db.PermissionRequests.FindAsync(requestId) != null;

        public static bool RepositoryExists(ReVersion_DatabaseContext db, string repoName) =>
            (from repository in db.Repositories
             where repository.Name.Equals(repoName)
             select repository.Id).Any();

        public static bool BranchExists(ReVersion_DatabaseContext db, string repoName, string branchName) =>
            (from repository in db.Repositories
             join branch in db.Branches on repository.Id equals branch.RepositoryId
             where repository.Name == repoName
             && branch.Name == branchName
             select branch.Id).Any();

        public static bool UsernameExists(ReVersion_DatabaseContext db, string username) =>
            (from user in db.Users
             where user.UserName == username
             select user.Id).Any();

        public static bool VersionExists(ReVersion_DatabaseContext db, string repoName, string branchName, string versionName) =>
            QueryVersions(db, repoName, branchName).Where(x => x.Equals(versionName)).Any();
    }
}