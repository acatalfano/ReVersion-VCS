using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReVersionVCS_API_Lambdas.Models;
using ReVersionVCS_API_Lambdas;
using System.Collections.Generic;
using System;
using System.Linq;

namespace ReVersionVCS_API_LambdasTests
{
    [TestClass]
    public class SQLUnitTest
    {
        const string hierarchy =
                    "{" +
                        "\"Name\": \"\"," +
                        "\"Type\": \"Directory\"," +
                        "\"Children\": [" +
                            "{" +
                                "\"Name\": \"root\"," +
                                "\"Type\": \"Directory\"," +
                                "\"Children\": [" +
                                    "{" +
                                        "\"Name\": \"rootChild\"," +
                                        "\"Type\": \"Directory\"," +
                                        "\"Children\": [" +
                                            "{" +
                                                "\"Name\": \"file1\"," +
                                                "\"Type\": \"File\"," +
                                                "\"Children\": []" +
                                            "}," +
                                            "{" +
                                                "\"Name\": \"subChild\"," +
                                                "\"Type\": \"Directory\"," +
                                                "\"Children\": [" +
                                                    "{" +
                                                        "\"Name\": \"file2\"," +
                                                        "\"Type\": \"File\"," +
                                                        "\"Children\": []" +
                                                    "}," +
                                                    "{" +
                                                        "\"Name\": \"file3\"," +
                                                        "\"Type\": \"File\"," +
                                                        "\"Children\": []" +
                                                    "}" +
                                                "]" +
                                            "}," +
                                            "{" +
                                                "\"Name\": \"subChild2\"," +
                                                "\"Type\": \"Directory\"," +
                                                "\"Children\": []" +
                                            "}" +
                                        "]" +
                                    "}" +
                                "]" +
                            "}," +
                            "{" +
                                "\"Name\": \"root2\"," +
                                "\"Type\": \"Directory\"," +
                                "\"Children\": [" +
                                    "{" +
                                        "\"Name\": \"loneFile\"," +
                                        "\"Type\": \"File\"," +
                                        "\"Children\": []" +
                                    "}" +
                                "]" +
                            "}" +
                        "]" +
                    "}";
        public SQLOperations sqlOps { get; set; }

        public SQLUnitTest()
        {
            Environment.SetEnvironmentVariable("RDS_DB_HOSTNAME", "reversion-deltas.chtlbyyyutrl.us-east-1.rds.amazonaws.com");
            Environment.SetEnvironmentVariable("RDS_DB_NAME", "ReVersion_Database");
            Environment.SetEnvironmentVariable("RDS_DB_USERNAME", "ReVersionDeltasMasterUser");
            Environment.SetEnvironmentVariable("RDS_DB_PASSWORD", "=8TrY>w6v9#Y[vX+");

            sqlOps = new SQLOperations();
        }

        [TestMethod]
        public void TestMethod1()
        {
            using (ReVersion_DatabaseContext db = new ReVersion_DatabaseContext())
            {
                /*
                sqlOps.InsertIntoUsersTable(db, "imaUser");
                sqlOps.InsertIntoUsersTable(db, "imanotherUser");
                db.SaveChanges();
                
                sqlOps.InsertIntoRepoTable(db, "firstRepo", "imaUser");
                sqlOps.InsertIntoRepoTable(db, "repo2", "imanotherUser");
                sqlOps.InsertIntoRepoTable(db, "yetAnotherRepo", "imaUser");
                db.SaveChanges();
                */
                List<string> repos = sqlOps.QueryRepositories(db);

                List<string> expected = new List<string> { "firstRepo", "repo2", "yetAnotherRepo" };

                SameListContents(repos, expected);
                
                //sqlOps.DeleteFromRepositoryTable(db, "repo2");
                //db.SaveChanges();
                expected.Remove("repo2");
                repos = sqlOps.QueryRepositories(db);

                SameListContents(repos, expected);
            }
        }

        [TestMethod]
        public void TestPermissions()
        {
            using (ReVersion_DatabaseContext db = new ReVersion_DatabaseContext())
            {
                List<string> users = new List<string> { "imaUser", "imanotherUser" };
                List<string> repos = new List<string> { "firstRepo", "yetAnotherRepo" };

                foreach(var item in repos.Zip(users, (r, u) => new { RepositoryName = r, Username = u }))
                {
                    Assert.IsFalse(sqlOps.UserCanAccessRepository(db, item.Username, item.RepositoryName));
                }

                TestPermissionInsertion(db, users, repos);
                //TestPermissionDeletion(db, users, repos);

            }
        }

        [TestMethod]
        public void TestBranch()
        {
            using (ReVersion_DatabaseContext db = new ReVersion_DatabaseContext())
            {
                sqlOps.InsertIntoBranchTable(db, "firstRepo", "imaNewBranch");
                sqlOps.InsertIntoBranchTable(db, "firstRepo", "imanotherBranch");
                sqlOps.InsertIntoRepoTable(db, "helloRepo", "imanotherUser");

                db.SaveChanges();

                sqlOps.InsertIntoBranchTable(db, "helloRepo", "helloBranch");

                db.SaveChanges();

                List<BranchLookup> branchesActual = sqlOps.QueryBranches(db, "firstRepo");
                List<BranchLookup> branchesExpected = new List<BranchLookup>
                {
                    new BranchLookup { BranchName = "imaNewBranch", Locked = false },
                    new BranchLookup { BranchName = "imanotherBranch", Locked = false }
                };

                SameListContents(branchesActual, branchesExpected);
            }

        }


        [TestMethod]
        public void TestBranchHierarchy()
        {
            using (ReVersion_DatabaseContext db = new ReVersion_DatabaseContext())
            {
                List<HierarchyNode> expectedFileData = new List<HierarchyNode>
                {
                    new HierarchyNode
                    {
                        Name = "",
                        Path = "/"
                    }
                };

                List<HierarchyNode> actualFileData =
                    sqlOps.QueryLatestHierarchy(db, "helloRepo", "helloBranch");

                SameListContents(actualFileData, expectedFileData);

                sqlOps.UpdateHierarchyDatumInBranchesTable(db, "helloRepo", "helloBranch", hierarchy);

                db.SaveChanges();
                
                expectedFileData = new List<HierarchyNode>
                {
                    new HierarchyNode { Name = "", Path = "/" },
                    new HierarchyNode { Name = "root", Path = "/" },
                    new HierarchyNode { Name = "rootChild", Path = "/root/" },
                    new HierarchyNode { Name = "file1", Path = "/root/rootChild/" },
                    new HierarchyNode { Name = "subChild", Path = "/root/rootChild/" },
                    new HierarchyNode { Name = "file2", Path = "/root/rootChild/subChild/" },
                    new HierarchyNode { Name = "file3", Path = "/root/rootChild/subChild/" },
                    new HierarchyNode { Name = "subChild2", Path = "/root/rootChild/" },
                    new HierarchyNode { Name = "root2", Path = "/" },
                    new HierarchyNode { Name = "loneFile", Path = "/root2/" }
                };

                actualFileData = sqlOps.QueryLatestHierarchy(db, "helloRepo", "helloBranch");

                SameListContents(actualFileData, expectedFileData);
                
            }
        }


        [TestMethod]
        public void TestBranchDelete()
        {
            using (ReVersion_DatabaseContext db = new ReVersion_DatabaseContext())
            {
                sqlOps.DeleteAllFromBranchesTable(db, "firstRepo");
                db.SaveChanges();

                List<BranchLookup> branchesActual = sqlOps.QueryBranches(db, "firstRepo");
                List<BranchLookup> branchesExpected = new List<BranchLookup>();

                SameListContents(branchesActual, branchesExpected);

                branchesActual = sqlOps.QueryBranches(db, "helloRepo");
                branchesExpected = new List<BranchLookup>
                {
                    new BranchLookup { BranchName = "helloBranch", Locked = false }
                };

                SameListContents(branchesActual, branchesExpected);
            }
        }

        [TestMethod]
        public void TestBranchIdQuery()
        {
            using (ReVersion_DatabaseContext db = new ReVersion_DatabaseContext())
            {
                int actualId = sqlOps.QueryBranchId(db, "helloRepo", "helloBranch");
                Assert.AreEqual(actualId, 3);
            }
        }

        [TestMethod]
        public void TestBranchLocks()
        {
            using (ReVersion_DatabaseContext db = new ReVersion_DatabaseContext())
            {
                bool lockedActual = sqlOps.BranchIsLocked(db, "helloRepo", "helloBranch");
                Assert.AreEqual(lockedActual, false, $"Lock Query: {lockedActual}");
                
                SQLOperations.LockState actualLockState =
                    sqlOps.AcquireLock(db, "helloRepo", "helloBranch");
                db.SaveChanges();

                Assert.AreEqual(actualLockState, SQLOperations.LockState.SuccessfulLockOperation,
                    $"Initial Lock: {actualLockState}");
                actualLockState = sqlOps.AcquireLock(db, "helloRepo", "helloBranch");
                db.SaveChanges();

                Assert.AreEqual(actualLockState, SQLOperations.LockState.AlreadyLockedConflict,
                    $"Second Lock: {actualLockState}");

                lockedActual = sqlOps.BranchIsLocked(db, "helloRepo", "helloBranch");
                Assert.AreEqual(lockedActual, true, $"Second lock query: {lockedActual}");

                actualLockState = sqlOps.ReleaseLock(db, "helloRepo", "helloBranch");
                db.SaveChanges();

                Assert.AreEqual(actualLockState, SQLOperations.LockState.SuccessfulLockOperation,
                    $"Release Lock: {actualLockState}");

                actualLockState = sqlOps.ReleaseLock(db, "helloRepo", "helloBranch");
                db.SaveChanges();

                Assert.AreEqual(actualLockState, SQLOperations.LockState.AlreadyUnlockedConflict,
                    $"Second release lock: {actualLockState}");

                lockedActual = sqlOps.BranchIsLocked(db, "helloRepo", "helloBranch");

                Assert.AreEqual(lockedActual, false, $"last lock query: {lockedActual}");
            }
        }

        [TestMethod]
        public void TestEvents()
        {
            using (ReVersion_DatabaseContext db = new ReVersion_DatabaseContext())
            {
                sqlOps.InsertIntoEventLog(db, "helloBranch", "imaUser", "hey ima message!!!", type: "place_lock");
                sqlOps.AcquireLock(db, "helloRepo", "helloBranch");
                sqlOps.InsertIntoEventLog(db, "yetAnotherBranch", "imaUser", "hey imyetanother message!!!", type: "merge_branch");
                sqlOps.InsertIntoEventLog(db, "helloBranch", "imanotherUser", "hey imanother message!!!", type: "grant_permission");
                sqlOps.InsertIntoEventLog(db, "helloBranch", "imaUser", "hey im back! same user same branch!", type: "commit_changes");
                sqlOps.InsertIntoEventLog(db, "helloBranch", "imanotherUser", "hey im back! same user same branch!", type: "commit_changes");
                sqlOps.InsertIntoEventLog(db, "helloBranch", "imanotherUser", "hey im back! same user same branch!", type: "commit_changes");
                db.SaveChanges();

                int actualLastImaUser = sqlOps.QueryLastEventIdByUser(db, "imaUser");
                int actualLastImanotherUser = sqlOps.QueryLastEventIdByUser(db, "imanotherUser");

                Assert.AreEqual(actualLastImaUser, 4);
                Assert.AreEqual(actualLastImanotherUser, 6);

                SQLOperations.LockState actualLockState =
                    sqlOps.SafeReleaseLock(db, "helloRepo", "helloBranch", "imanotherUser");
                Assert.AreEqual(actualLockState, SQLOperations.LockState.LockedByDifferentUser);

                actualLockState = sqlOps.SafeReleaseLock(db, "yetAnotherRepo", "yetAnotherBranch", "imaUser");
                Assert.AreEqual(actualLockState, SQLOperations.LockState.AlreadyUnlockedConflict);

                actualLockState = sqlOps.SafeReleaseLock(db, "helloRepo", "helloBranch", "imaUser");
                Assert.AreEqual(actualLockState, SQLOperations.LockState.SuccessfulLockOperation);

            }

        }
        
        [TestMethod]
        public void TestVersions()
        {
            using (ReVersion_DatabaseContext db = new ReVersion_DatabaseContext())
            {
                FileHierarchyData hierData = new FileHierarchyData(hierarchy);
                hierData.AddNode("/A/B/alpha/beTa");
                hierData.DeleteNode("F/");

                VersionData data = new VersionData
                {
                    RepositoryName = "yetAnotherRepo",
                    BranchName = "yetAnotherBranch",
                    ParentBranchName = "yetAnotherOtherBranch",
                    NewBranch = true,
                    DeltaContent = "here's Delta!",
                    FileHierarchy = hierData.GetHierarchyList(),
                    EventId = 5
                };

                sqlOps.InsertIntoVersionsTable(db, data);
                db.SaveChanges();

                List<int> versionsActual = sqlOps.QueryVersions(db, "yetAnotherRepo", "yetAnotherBranch");
                List<int> versionsExpected = new List<int> { 1 };
                SameListContents(versionsActual, versionsExpected);
            }
        }

        [TestMethod]
        public void TestFileHierarchyListCtor()
        {
            List<string> hierarchy = new List<string>{ "A/B/C/",    "/A/B/D",   "A/E",      "/F/",
                                                        "F/B",      "A/B/",     "A/B"               };

            FileHierarchyData hierarchyData = new FileHierarchyData(hierarchy);

            List<HierarchyNode> prelimActualList = hierarchyData.GetHierarchyList();
            List<string> finalActualList = new List<string>();

            foreach (HierarchyNode item in prelimActualList)
            {
                finalActualList.Add(item.ToString());
            }

            List<string> expectedHierarchy = new List<string>
            {
                "/", "/A/", "/F/", "/F/B", "/A/B", "/A/B/", "/A/E", "/A/B/C/", "/A/B/D"
            };

            SameListContents(expectedHierarchy, finalActualList);


            hierarchyData.DeleteNode("/A/B/");
            hierarchyData.DeleteNode("/A/B/D");
            hierarchyData.DeleteNode("/A/B/");

            expectedHierarchy = new List<string> { "/", "/A/", "/A/E", "/A/B", "/F/", "/F/B" };
            finalActualList = new List<string>();
            prelimActualList = hierarchyData.GetHierarchyList();

            foreach (HierarchyNode item in prelimActualList)
            {
                finalActualList.Add(item.ToString());
            }

            SameListContents(expectedHierarchy, finalActualList);
        }

        [TestMethod]
        public void TestVersionDelete()
        {
            using (ReVersion_DatabaseContext db = new ReVersion_DatabaseContext())
            {
                sqlOps.DeleteAllFromVersionsTable(db, "yetAnotherRepo");
                db.SaveChanges();
            }
        }

        [TestMethod]
        public void TestPermissionRequests()
        {
            // List<PermissionLookup> QueryPermissionRequests(db, username)
            //              PermissionLookup { string Message, string RequestingUser, string RepositoryName, int RequestId, DateTime LogTimestamp }
            // int QueryRepositoryIdFromRequest(db, requestId)
            // int QueryMainTrunkFromRequest(db, requestId)
            // int QueryUserFromRequest(db, requestId)
            //
            // void DeleteAllFromPermissionRequestsTable(db, repoName)
            // void InsertIntoPermissionRequestTable(db, repoName, eventId)
            // void DeleteFromPermissionRequestTable(db, requestId)

            using (ReVersion_DatabaseContext db = new ReVersion_DatabaseContext())
            {
                /*
                sqlOps.InsertIntoPermissionRequestTable(db, "helloRepo", 7);
                sqlOps.InsertIntoPermissionRequestTable(db, "helloRepo", 8);
                sqlOps.InsertIntoPermissionRequestTable(db, "firstRepo", 9);
                sqlOps.InsertIntoPermissionRequestTable(db, "yetAnotherRepo", 10);
                sqlOps.InsertIntoPermissionRequestTable(db, "firstRepo", 11);
                db.SaveChanges();
                */
                /*
                sqlOps.DeleteAllFromPermissionRequestsTable(db, "helloRepo");
                sqlOps.DeleteFromPermissionRequestTable(db, 3);
                sqlOps.DeleteFromPermissionRequestTable(db, 4);
                sqlOps.DeleteFromPermissionRequestTable(db, 5);
                db.SaveChanges();
                */

                int actual = sqlOps.QueryUserFromRequest(db, 4);
                Assert.AreEqual(actual, 2);
                actual = sqlOps.QueryUserFromRequest(db, 3);
                Assert.AreEqual(actual, 4);

                actual = sqlOps.QueryMainTrunkFromRequest(db, 5);
                Assert.AreEqual(actual, 6);
                actual = sqlOps.QueryMainTrunkFromRequest(db, 1);
                Assert.AreEqual(actual, 8);

                actual = sqlOps.QueryRepositoryIdFromRequest(db, 2);
                Assert.AreEqual(actual, 9);
                actual = sqlOps.QueryRepositoryIdFromRequest(db, 3);
                Assert.AreEqual(actual, 1);


                List<PermissionLookup> actualPerms = sqlOps.QueryPermissionRequests(db, "imanotherUser");
                List<PermissionLookup> expectedPerms = new List<PermissionLookup>
                {
                    new PermissionLookup { RequestId = 1, RepositoryName = "helloRepo", Message = "permit", RequestingUser = "adam"},
                    new PermissionLookup { RequestId = 2, RepositoryName = "helloRepo", Message = "permit", RequestingUser = "somebody"}
                };

                SameListContents(actualPerms, expectedPerms);
            }
        }

        [TestMethod]
        private void TestPermissionInsertion(ReVersion_DatabaseContext db, List<string> users, List<string> repos)
        {
            sqlOps.InsertIntoRepoPermissionsTable(db, "firstRepo", "imaUser");
            sqlOps.InsertIntoRepoPermissionsTable(db, "yetAnotherRepo", "imaUser");
            sqlOps.InsertIntoRepoPermissionsTable(db, "yetAnotherRepo", "imanotherUser");

            db.SaveChanges();

            foreach (var item in repos.Zip(users, (r, u) => new { RepositoryName = r, Username = u }))
            {
                if (item.Username.Equals("imanotherUser") && item.RepositoryName.Equals("firstRepo"))
                {
                    Assert.IsFalse(sqlOps.UserCanAccessRepository(db, item.Username, item.RepositoryName));
                }
                else
                {
                    Assert.IsTrue(sqlOps.UserCanAccessRepository(db, item.Username, item.RepositoryName));
                }
            }

            List<string> permsActual = sqlOps.QueryPermissions(db, "imaUser");
            List<string> permsExpected = new List<string> { "firstRepo", "yetAnotherRepo" };

            SameListContents(permsActual, permsExpected);
        }



        [TestMethod]
        private void TestPermissionDeletion(ReVersion_DatabaseContext db, List<string> users, List<string> repos)
        {
            sqlOps.DeleteAllFromPermissionsTable(db, "yetAnotherRepo");

            db.SaveChanges();

            foreach (var item in repos.Zip(users, (r, u) => new { RepositoryName = r, Username = u }))
            {
                if ( item.RepositoryName.Equals("firstRepo") && item.Username.Equals("imaUser") )
                {
                    Assert.IsTrue(sqlOps.UserCanAccessRepository(db, item.Username, item.RepositoryName));
                }
                else
                {
                    Assert.IsFalse(sqlOps.UserCanAccessRepository(db, item.Username, item.RepositoryName));
                }
            }
        }


        private void SameListContents<T>(List<T> actual, List<T> expected)
        {
            Assert.AreEqual(actual.Count, expected.Count);
            foreach (T item in expected)
            {
                
                int actualCount = actual.FindAll(x => x.Equals(item)).Count;
                int expectedCount = expected.FindAll(x => x.Equals(item)).Count;
                Assert.AreEqual(actualCount, expectedCount);
            }
        }
    }
}