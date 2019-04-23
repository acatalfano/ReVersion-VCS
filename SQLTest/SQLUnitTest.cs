using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReVersionVCS_API_Lambdas.Models;
using ReVersionVCS_API_Lambdas;
using System.Collections.Generic;
using System;
using System.Linq;

namespace ReVersionVCS_API_Lambdas.Tests
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
        public SQLUnitTest()
        {
            Environment.SetEnvironmentVariable("RDS_DB_HOSTNAME", "reversion-deltas.chtlbyyyutrl.us-east-1.rds.amazonaws.com");
            Environment.SetEnvironmentVariable("RDS_DB_NAME", "ReVersion_Database");
            Environment.SetEnvironmentVariable("RDS_DB_USERNAME", "ReVersionDeltasMasterUser");
            Environment.SetEnvironmentVariable("RDS_DB_PASSWORD", "=8TrY>w6v9#Y[vX+");
        }

        [TestMethod]
        public void TestMethod1()
        {
            using (ReVersion_DatabaseContext db = new ReVersion_DatabaseContext())
            {
                /*
                SQLOperations.InsertIntoUsersTable(db, "imaUser");
                SQLOperations.InsertIntoUsersTable(db, "imanotherUser");
                db.SaveChanges();
                
                SQLOperations.InsertIntoRepoTable(db, "firstRepo", "imaUser");
                SQLOperations.InsertIntoRepoTable(db, "repo2", "imanotherUser");
                SQLOperations.InsertIntoRepoTable(db, "yetAnotherRepo", "imaUser");
                db.SaveChanges();
                */
                List<RepositoryLookup> repos = SQLOperations.QueryRepositories(db);

                List<RepositoryLookup> expected = new List<RepositoryLookup>
                {
                    new RepositoryLookup { Name = "firstRepo", Owner = "imaUser"},
                    new RepositoryLookup { Name = "helloRepo", Owner = "imanotherUser"},
                    new RepositoryLookup { Name = "yetAnotherRepo", Owner = "imaUser"}
                };

                SameListContents(repos, expected);
                
                //SQLOperations.DeleteFromRepositoryTable(db, "repo2");
                //db.SaveChanges();
                //expected.Remove("repo2");
                //repos = SQLOperations.QueryRepositories(db);

                //SameListContents(repos, expected);
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
                    Assert.IsFalse(SQLOperations.UserCanAccessRepository(db, item.Username, item.RepositoryName));
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
                SQLOperations.InsertIntoBranchTable(db, "firstRepo", "imaNewBranch");
                SQLOperations.InsertIntoBranchTable(db, "firstRepo", "imanotherBranch");
                SQLOperations.InsertIntoRepoTable(db, "helloRepo", "imanotherUser");

                db.SaveChanges();

                SQLOperations.InsertIntoBranchTable(db, "helloRepo", "helloBranch");

                db.SaveChanges();

                List<BranchLookup> branchesActual = SQLOperations.QueryBranches(db, "firstRepo");
                List<BranchLookup> branchesExpected = new List<BranchLookup>
                {
                    new BranchLookup { BranchName = "imaNewBranch", Locked = false },
                    new BranchLookup { BranchName = "imanotherBranch", Locked = false }
                };

                SameListContents(branchesActual, branchesExpected);
            }

        }
        /*

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
                    SQLOperations.QueryLatestHierarchy(db, "helloRepo", "helloBranch");

                SameListContents(actualFileData, expectedFileData);

                SQLOperations.UpdateHierarchyDatumInBranchesTable(db, "helloRepo", "helloBranch", hierarchy);

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

                actualFileData = SQLOperations.QueryLatestHierarchy(db, "helloRepo", "helloBranch");

                SameListContents(actualFileData, expectedFileData);
                
            }
        }
        */

        [TestMethod]
        public void TestBranchDelete()
        {
            using (ReVersion_DatabaseContext db = new ReVersion_DatabaseContext())
            {
                SQLOperations.DeleteAllFromBranchesTable(db, "firstRepo");
                db.SaveChanges();

                List<BranchLookup> branchesActual = SQLOperations.QueryBranches(db, "firstRepo");
                List<BranchLookup> branchesExpected = new List<BranchLookup>();

                SameListContents(branchesActual, branchesExpected);

                branchesActual = SQLOperations.QueryBranches(db, "helloRepo");
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
                int actualId = SQLOperations.QueryBranchId(db, "helloRepo", "helloBranch");
                Assert.AreEqual(actualId, 3);
            }
        }

        [TestMethod]
        public void TestBranchLocks()
        {
            using (ReVersion_DatabaseContext db = new ReVersion_DatabaseContext())
            {
                bool lockedActual = SQLOperations.BranchIsLocked(db, "helloRepo", "helloBranch");
                Assert.AreEqual(lockedActual, false, $"Lock Query: {lockedActual}");
                
                SQLOperations.LockState actualLockState =
                    SQLOperations.AcquireLock(db, "helloRepo", "helloBranch");
                db.SaveChanges();

                Assert.AreEqual(actualLockState, SQLOperations.LockState.SuccessfulLockOperation,
                    $"Initial Lock: {actualLockState}");
                actualLockState = SQLOperations.AcquireLock(db, "helloRepo", "helloBranch");
                db.SaveChanges();

                Assert.AreEqual(actualLockState, SQLOperations.LockState.AlreadyLockedConflict,
                    $"Second Lock: {actualLockState}");

                lockedActual = SQLOperations.BranchIsLocked(db, "helloRepo", "helloBranch");
                Assert.AreEqual(lockedActual, true, $"Second lock query: {lockedActual}");

                actualLockState = SQLOperations.ReleaseLock(db, "helloRepo", "helloBranch");
                db.SaveChanges();

                Assert.AreEqual(actualLockState, SQLOperations.LockState.SuccessfulLockOperation,
                    $"Release Lock: {actualLockState}");

                actualLockState = SQLOperations.ReleaseLock(db, "helloRepo", "helloBranch");
                db.SaveChanges();

                Assert.AreEqual(actualLockState, SQLOperations.LockState.AlreadyUnlockedConflict,
                    $"Second release lock: {actualLockState}");

                lockedActual = SQLOperations.BranchIsLocked(db, "helloRepo", "helloBranch");

                Assert.AreEqual(lockedActual, false, $"last lock query: {lockedActual}");
            }
        }
        /*
        [TestMethod]
        public void TestEvents()
        {
            using (ReVersion_DatabaseContext db = new ReVersion_DatabaseContext())
            {
                SQLOperations.InsertIntoEventLog(db, "helloBranch", "imaUser", "hey ima message!!!", type: "place_lock");
                SQLOperations.AcquireLock(db, "helloRepo", "helloBranch");
                SQLOperations.InsertIntoEventLog(db, "yetAnotherBranch", "imaUser", "hey imyetanother message!!!", type: "merge_branch");
                SQLOperations.InsertIntoEventLog(db, "helloBranch", "imanotherUser", "hey imanother message!!!", type: "grant_permission");
                SQLOperations.InsertIntoEventLog(db, "helloBranch", "imaUser", "hey im back! same user same branch!", type: "commit_changes");
                SQLOperations.InsertIntoEventLog(db, "helloBranch", "imanotherUser", "hey im back! same user same branch!", type: "commit_changes");
                SQLOperations.InsertIntoEventLog(db, "helloBranch", "imanotherUser", "hey im back! same user same branch!", type: "commit_changes");
                db.SaveChanges();

                int actualLastImaUser = SQLOperations.QueryLastEventIdByUser(db, "imaUser");
                int actualLastImanotherUser = SQLOperations.QueryLastEventIdByUser(db, "imanotherUser");

                Assert.AreEqual(actualLastImaUser, 4);
                Assert.AreEqual(actualLastImanotherUser, 6);

                SQLOperations.LockState actualLockState =
                    SQLOperations.SafeReleaseLock(db, "helloRepo", "helloBranch", "imanotherUser");
                Assert.AreEqual(actualLockState, SQLOperations.LockState.LockedByDifferentUser);

                actualLockState = SQLOperations.SafeReleaseLock(db, "yetAnotherRepo", "yetAnotherBranch", "imaUser");
                Assert.AreEqual(actualLockState, SQLOperations.LockState.AlreadyUnlockedConflict);

                actualLockState = SQLOperations.SafeReleaseLock(db, "helloRepo", "helloBranch", "imaUser");
                Assert.AreEqual(actualLockState, SQLOperations.LockState.SuccessfulLockOperation);

            }

        }
        */
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
                    //FileHierarchy = hierData.GetHierarchyList(),
                    EventId = 5
                };

                SQLOperations.InsertIntoVersionsTable(db, data);
                db.SaveChanges();

                List<int> versionsActual = SQLOperations.QueryVersions(db, "yetAnotherRepo", "yetAnotherBranch");
                List<int> versionsExpected = new List<int> { 1 };
                SameListContents(versionsActual, versionsExpected);
            }
        }
        /*
        [TestMethod]
        public void TestFileHierarchyListCtor()
        {
            List<string> hierarchy = new List<string>{ "A/B/C/",    "/A/B/D",   "A/E",      "/F/",
                                                        "F/B",      "A/B/",     "A/B"               };

            FileHierarchyData hierarchyData = new FileHierarchyData(hierarchy);

            //List<HierarchyNode> prelimActualList = hierarchyData.GetHierarchyList();
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
        */
        [TestMethod]
        public void TestVersionDelete()
        {
            using (ReVersion_DatabaseContext db = new ReVersion_DatabaseContext())
            {
                SQLOperations.DeleteAllFromVersionsTable(db, "yetAnotherRepo");
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
                SQLOperations.InsertIntoPermissionRequestTable(db, "helloRepo", 7);
                SQLOperations.InsertIntoPermissionRequestTable(db, "helloRepo", 8);
                SQLOperations.InsertIntoPermissionRequestTable(db, "firstRepo", 9);
                SQLOperations.InsertIntoPermissionRequestTable(db, "yetAnotherRepo", 10);
                SQLOperations.InsertIntoPermissionRequestTable(db, "firstRepo", 11);
                db.SaveChanges();
                */
                /*
                SQLOperations.DeleteAllFromPermissionRequestsTable(db, "helloRepo");
                SQLOperations.DeleteFromPermissionRequestTable(db, 3);
                SQLOperations.DeleteFromPermissionRequestTable(db, 4);
                SQLOperations.DeleteFromPermissionRequestTable(db, 5);
                db.SaveChanges();
                */

                int actual = SQLOperations.QueryUserFromRequest(db, 4);
                Assert.AreEqual(actual, 2);
                actual = SQLOperations.QueryUserFromRequest(db, 3);
                Assert.AreEqual(actual, 4);

                actual = SQLOperations.QueryMainTrunkFromRequest(db, 5);
                Assert.AreEqual(actual, 6);
                actual = SQLOperations.QueryMainTrunkFromRequest(db, 1);
                Assert.AreEqual(actual, 8);
                /*
                actual = SQLOperations.QueryRepositoryIdFromRequest(db, 2);
                Assert.AreEqual(actual, 9);
                actual = SQLOperations.QueryRepositoryIdFromRequest(db, 3);
                Assert.AreEqual(actual, 1);
                */

                List<PermissionLookup> actualPerms = SQLOperations.QueryPermissionRequests(db, "imanotherUser");
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
            SQLOperations.InsertIntoRepoPermissionsTable(db, "firstRepo", "imaUser");
            SQLOperations.InsertIntoRepoPermissionsTable(db, "yetAnotherRepo", "imaUser");
            SQLOperations.InsertIntoRepoPermissionsTable(db, "yetAnotherRepo", "imanotherUser");

            db.SaveChanges();

            foreach (var item in repos.Zip(users, (r, u) => new { RepositoryName = r, Username = u }))
            {
                if (item.Username.Equals("imanotherUser") && item.RepositoryName.Equals("firstRepo"))
                {
                    Assert.IsFalse(SQLOperations.UserCanAccessRepository(db, item.Username, item.RepositoryName));
                }
                else
                {
                    Assert.IsTrue(SQLOperations.UserCanAccessRepository(db, item.Username, item.RepositoryName));
                }
            }

            List<string> permsActual = SQLOperations.QueryPermissions(db, "imaUser");
            List<string> permsExpected = new List<string> { "firstRepo", "yetAnotherRepo" };

            SameListContents(permsActual, permsExpected);
        }



        [TestMethod]
        private void TestPermissionDeletion(ReVersion_DatabaseContext db, List<string> users, List<string> repos)
        {
            SQLOperations.DeleteAllFromPermissionsTable(db, "yetAnotherRepo");

            db.SaveChanges();

            foreach (var item in repos.Zip(users, (r, u) => new { RepositoryName = r, Username = u }))
            {
                if ( item.RepositoryName.Equals("firstRepo") && item.Username.Equals("imaUser") )
                {
                    Assert.IsTrue(SQLOperations.UserCanAccessRepository(db, item.Username, item.RepositoryName));
                }
                else
                {
                    Assert.IsFalse(SQLOperations.UserCanAccessRepository(db, item.Username, item.RepositoryName));
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