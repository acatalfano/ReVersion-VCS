using System.Collections.Generic;
using System.Linq;
using ReVersionVCS_API_Lambdas.Models;

namespace ReVersionVCS_API_Lambdas
{
    public partial class SQLOperations
    {

        public enum LockState
        {
            AlreadyLockedConflict,
            AlreadyUnlockedConflict,
            SuccessfulLockOperation,
            LockedByDifferentUser
        }

        public void InsertIntoRepoTable(ReVersion_DatabaseContext db, string repoName, string username) =>
            db.Repositories.AddAsync(new Repository
            {
                Name = repoName,
                Owner = (from user in db.Users
                         where user.UserName == username
                         select user.Id)
                        .Single()
            });

        public void InsertIntoBranchTable(ReVersion_DatabaseContext db, string repoName, string branchName) =>
            db.Branches.Add(
                (from repository in db.Repositories
                 where repository.Name == repoName
                 select new Branch
                 {
                     RepositoryId = repository.Id,
                     Name = branchName
                 })
                .Single()
                );

        public void InsertIntoRepoPermissionsTable(ReVersion_DatabaseContext db, string repoName, string username) =>
            db.RepositoryPermissions.Add(
                (from repository in db.Repositories
                 where repository.Name == repoName
                 from user in db.Users
                 where user.UserName == username
                 select new RepositoryPermission
                 {
                     PermittedUser = user.Id,
                     RepositoryId = repository.Id
                 })
                .Single()
                );

        public void InsertIntoEventLog(ReVersion_DatabaseContext db, string branchName, string username, string message, string type)
        {
            db.EventLogs.Add(
                (from user in db.Users
                 from branch in db.Branches
                 where user.UserName == username
                 && branch.Name == branchName
                 select new EventLog
                 {
                     Type = type,
                     BranchId = branch.Id,
                     UserId = user.Id,
                     Message = message
                 })
                .Single()
               );
        }

        public void DeleteAllFromBranchesTable(ReVersion_DatabaseContext db, string repoName) =>
            db.Branches.RemoveRange(
                from branch in db.Branches
                join repository in db.Repositories on branch.RepositoryId equals repository.Id
                where repository.Name == repoName
                select branch);

        public void DeleteAllFromVersionsTable(ReVersion_DatabaseContext db, string repoName) =>
            db.Versions.RemoveRange(
                from repository in db.Repositories
                where repository.Name == repoName
                join branch in db.Branches on repository.Id equals branch.RepositoryId
                join version in db.Versions on branch.Id equals version.BranchId
                select version);

        public void DeleteAllFromPermissionsTable(ReVersion_DatabaseContext db, string repoName) =>
            db.RepositoryPermissions.RemoveRange(
                from repository in db.Repositories
                where repository.Name == repoName
                join permission in db.RepositoryPermissions on repository.Id equals permission.RepositoryId
                select permission);

        public void DeleteAllFromPermissionRequestsTable(ReVersion_DatabaseContext db, string repoName) =>
            db.PermissionRequests.RemoveRange(
                from repository in db.Repositories
                where repository.Name == repoName
                join request in db.PermissionRequests on repository.Id equals request.RepositoryId
                select request);
        
        public void DeleteFromRepositoryTable(ReVersion_DatabaseContext db, string repoName) =>
            db.Repositories.RemoveRange(
                from repository in db.Repositories
                where repository.Name == repoName
                select repository);

        public LockState AcquireLock(ReVersion_DatabaseContext db, string repoName, string branchName)
        {
            Branch branchEntity =
            (from repository in db.Repositories
             where repository.Name == repoName
             join branch in db.Branches on repository.Id equals branch.RepositoryId
             where branch.Name == branchName
             select branch)
            .Single();

            if (branchEntity.Locked)
            {
                return LockState.AlreadyLockedConflict;
            }

            branchEntity.Locked = true;
            return LockState.SuccessfulLockOperation;
        }

        public void InsertIntoVersionsTable(ReVersion_DatabaseContext db, VersionData data) =>
            db.Versions.Add((from thisBranch in db.Branches
                             from parentBranch in db.Branches
                             join repository in db.Repositories on thisBranch.RepositoryId equals repository.Id
                             where thisBranch.Name == data.BranchName
                             && parentBranch.Name == (data.NewBranch ? data.ParentBranchName : data.BranchName)
                             && repository.Name == data.RepositoryName
                             select new Version
                             {
                                 VersionNumber = thisBranch.VersionNumber,
                                 BranchId = thisBranch.Id,
                                 ParentBranch = parentBranch.Id,
                                 RollbackDelta = data.DeltaContent,
                                 FileHierarchy = data.FileHierarchyString(),
                                 UpdateEventId = data.EventId
                             })
                            .Single());

        public void UpdateHierarchyDatumInBranchesTable(ReVersion_DatabaseContext db, string repoName, string branchName, string hierarchyDatum)
        {
            Branch branchEntity =
                (from repository in db.Repositories
                 where repository.Name == repoName
                 join branch in db.Branches on repository.Id equals branch.RepositoryId
                 where branch.Name == branchName
                 select branch)
                .Single();

            branchEntity.VersionNumber += 1;
            branchEntity.LatestFileHierarchy = hierarchyDatum;
        }

        public LockState ReleaseLock(ReVersion_DatabaseContext db, string repoName, string branchName)
        {
            Branch branchEntity =
                (from repository in db.Repositories
                 where repository.Name == repoName
                 join branch in db.Branches on repository.Id equals branch.RepositoryId
                 where branch.Name == branchName
                 select branch)
                .Single();

            if (!branchEntity.Locked)
            {
                return LockState.AlreadyUnlockedConflict;
            }
            
            branchEntity.Locked = false;
            return LockState.SuccessfulLockOperation;
        }

        public LockState SafeReleaseLock(ReVersion_DatabaseContext db, string repoName, string branchName, string username)
        {
            Branch branchEntity =
                (from repository in db.Repositories
                 join branch in db.Branches on repository.Id equals branch.RepositoryId
                 where repository.Name == repoName
                 && branch.Name == branchName
                 select branch)
                .Single();

            if (!branchEntity.Locked)
                return LockState.AlreadyUnlockedConflict;

            string usernameQuery =
                (from log in db.EventLogs
                 where branchEntity.Id == log.BranchId
                 && log.Type == "place_lock"
                 join user in db.Users on log.UserId equals user.Id
                 orderby log.LoggedAt descending, log.Id descending
                 select user.UserName)
                .First();

            if (username != usernameQuery)
                return LockState.LockedByDifferentUser;

            branchEntity.Locked = false;
            return LockState.SuccessfulLockOperation;
        }

        public void InsertIntoPermissionRequestTable(ReVersion_DatabaseContext db, string repoName, int eventId) =>
            db.PermissionRequests.Add(new PermissionRequest
            {
                EventId = eventId,
                RepositoryId = (from repository in db.Repositories
                                where repository.Name == repoName
                                select repository.Id).Single()
            });

        public void DeleteFromPermissionRequestTable(ReVersion_DatabaseContext db, int requestId) =>
            db.PermissionRequests.Remove(db.PermissionRequests.Find(requestId));
        
        public void InsertIntoUsersTable(ReVersion_DatabaseContext db, string username) =>
            db.Users.Add(new User
            {
                UserName = username
            });
    }
}