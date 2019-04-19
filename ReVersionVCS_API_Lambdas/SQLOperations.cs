using System;
using System.Collections.Generic;
using System.Linq;
using ReVersionVCS_API_Lambdas.Models;

namespace ReVersionVCS_API_Lambdas
{
    public partial class SQLOperations
    {
        public List<string> QueryRepositories(ReVersion_DatabaseContext db) =>
            (from repository in db.Repositories
             select repository.Name)
            .ToList();

        public List<HierarchyNode> QueryLatestHierarchy(ReVersion_DatabaseContext db, string repoName, string branchName)
        {
            string hierarchyJson =
                (from repository in db.Repositories
                 join branch in db.Branches on repository.Id equals branch.RepositoryId
                 where repository.Name == repoName
                 && branch.Name == branchName
                 select branch.LatestFileHierarchy)
                .Single();

            FileHierarchyData hierarchyData = new FileHierarchyData(hierarchyJson);

            return hierarchyData.GetHierarchyList();
        }

        public List<BranchLookup> QueryBranches(ReVersion_DatabaseContext db, string repoName) =>
                (from repository in db.Repositories
                 join branch in db.Branches
                 on repository.Id equals branch.RepositoryId
                 where repository.Name == repoName
                 select new BranchLookup { Locked = branch.Locked, BranchName = branch.Name })
                    .ToList();
        
        public int QueryBranchId(ReVersion_DatabaseContext db, string repoName, string branchName) =>
            (from repository in db.Repositories
             join branch in db.Branches
             on repository.Id equals branch.RepositoryId
             where repository.Name == repoName
                 && branch.Name == branchName
             select branch.Id)
                 .Single();

        public List<int> QueryVersions(ReVersion_DatabaseContext db, string repoName, string branchName) =>
            (from repository in db.Repositories
             join branch in db.Branches on repository.Id equals branch.RepositoryId
             where repository.Name == repoName && branch.Name == branchName
             join version in db.Versions on branch.Id equals version.BranchId
             select version.VersionNumber)
                .ToList();

        public bool UserCanAccessRepository(ReVersion_DatabaseContext db, string username, string repoName)
        {
            List<string> repositoryPermissions = QueryPermissions(db, username);
            return repositoryPermissions.Contains(repoName);
        }
        
        public List<string> QueryPermissions(ReVersion_DatabaseContext db, string username) =>
            (from user in db.Users
             where user.UserName == username
             join permission in db.RepositoryPermissions on user.Id equals permission.PermittedUser
             join repository in db.Repositories on permission.RepositoryId equals repository.Id
             select repository.Name)
            .ToList();

        public List<PermissionLookup> QueryPermissionRequests(ReVersion_DatabaseContext db, string username)
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

        public int QueryRepositoryIdFromRequest(ReVersion_DatabaseContext db, int requestId)
        {
            int trunkId = QueryMainTrunkFromRequest(db, requestId);
            return
               (from branch in db.Branches
                where branch.Id == trunkId
                select branch.RepositoryId)
               .Single();
        }

        public int QueryMainTrunkFromRequest(ReVersion_DatabaseContext db, int requestId) =>
            (from request in db.PermissionRequests
             where request.Id == requestId
             join log in db.EventLogs on request.EventId equals log.Id
             join branch in db.Branches on log.BranchId equals branch.Id
             select branch.Id)
            .Single();

        public int QueryLastEventIdByUser(ReVersion_DatabaseContext db, string username) =>
            (from log in db.EventLogs
             join user in db.Users on log.UserId equals user.Id
             where user.UserName == username
             orderby log.LoggedAt descending, log.Id descending
             select log.Id)
            .First();
        
        public int QueryUserFromRequest(ReVersion_DatabaseContext db, int requestId) =>
            (from request in db.PermissionRequests
             where request.Id == requestId
             join log in db.EventLogs on request.EventId equals log.Id
             select log.UserId)
            .Single();

        public bool BranchIsLocked(ReVersion_DatabaseContext db, string repoName, string branchName) =>
                (from repository in db.Repositories
                 join branch in db.Branches on repository.Id equals branch.RepositoryId
                 where  repository.Name == repoName
                 && branch.Name == branchName
                 select branch.Locked)
                .Single();

    }
}