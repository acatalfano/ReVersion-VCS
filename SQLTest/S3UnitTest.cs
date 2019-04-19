using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using ReVersionVCS_API_Lambdas;
using System.Threading.Tasks;
using Amazon.S3.Model;

namespace ReVersionVCS_API_Lambdas.Tests
{
    [TestClass]
    public class S3UnitTest
    {
        private readonly S3Operations ops = new S3Operations("us-east-1");

        [TestMethod]
        public void CreateBucketTest()
        {
            List<Task<PutBucketResponse>> tasks = new List<Task<PutBucketResponse>>();
            var next = ops.CreateS3BucketAsync("imaRepo", "imanotherBranch");
            //next.Start();
            tasks.Add(next);
            next = ops.CreateS3BucketAsync("imaRepo", "imaThirdBranch");
            //next.Start();
            tasks.Add(next);
            next = ops.CreateS3BucketAsync("imanotherRepo", "imanotherBranch");
            //next.Start();
            tasks.Add(next);
            next = ops.CreateS3BucketAsync("imaRepo", "imaBranch");
            tasks.Add(next);


            Task.WaitAll(tasks.ToArray());

        }

        [TestMethod]
        public void DeleteImaRepoBucketsTest()
        {
            List<string> branches = new List<string>
            {
                "imaBranch", "imanotherBranch", "imaThirdBranch"
            };

            ops.DeleteBranchBucketsByRepoAsync("imaRepo", branches).Wait();
        }

        [TestMethod]
        public void DeleteImanotherRepoBucketsTest()
        {
            List<string> branches = new List<string> { "imanotherBranch" };
            ops.DeleteBranchBucketsByRepoAsync("imanotherRepo", branches).Wait();
        }

        [TestMethod]
        public void UpdateBucketTest()
        {
            List<FileData> input = new List<FileData>
            {
                new FileData { BranchName = "imaBranch", RepositoryName = "imaRepo",
                                ObjectKey = "/root/textfile.txt", Content = "hello update!!" },
                new FileData { BranchName = "imaBranch", RepositoryName = "imaRepo",
                                ObjectKey = "/textfile.txt", Content = "hello root!" },
                
            };
            ops.UpdateS3ObjectsAsync(input).Wait();

            input = new List<FileData>
            {
                new FileData { BranchName = "imanotherBranch", RepositoryName = "imaRepo",
                                ObjectKey = "/root/textfile.txt", Content = "ima decoy!" },
                new FileData { BranchName = "imanotherBranch", RepositoryName = "imanotherRepo",
                                ObjectKey = "/root/heyyyyy", Content = "hello! hello! wazzappp??" }
            };

            ops.UpdateS3ObjectsAsync(input).Wait();
        }

        [TestMethod]
        public void GetContentTest()
        {
            string actual = ops.GetTextFromObjectAsync("imaRepo", "imaBranch", "/root/textfile.txt").Result;
            string expected = "hello update!!";

            Assert.AreEqual(actual, expected);
        }

        [TestMethod]
        public void MergeTest()
        {
            // async Task<???something???> MergeIntoBucketAsync(string repoName, string branchName, string parentBranchName)
            
            // TODO: write this method and then test it! (or don't, maybe the logic can be separate)
        }
    }
}
