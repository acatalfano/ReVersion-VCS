using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using ReVersionVCS_API_Lambdas;

using Amazon;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using System.Threading.Tasks;
using ReVersionVCS_API_Lambdas.Response_Objects;
using ReVersionVCS_API_Lambdas.Models;

namespace ReVersionVCS_API_Lambdas.Tests
{
    [TestClass]
    public class LambdaTests
    {
        Functions functions;
        public LambdaTests()
        {
            Environment.SetEnvironmentVariable("RDS_DB_HOSTNAME", "reversion-deltas.chtlbyyyutrl.us-east-1.rds.amazonaws.com");
            Environment.SetEnvironmentVariable("RDS_DB_PORT", "5432");
            Environment.SetEnvironmentVariable("RDS_DB_NAME", "ReVersion_Database");
            Environment.SetEnvironmentVariable("RDS_DB_USERNAME", "ReVersionDeltasMasterUser");
            Environment.SetEnvironmentVariable("RDS_DB_PASSWORD", "=8TrY>w6v9#Y[vX+");
            Environment.SetEnvironmentVariable("S3_REGION", "us-east-1");

            functions = new Functions();
        }

        [TestMethod]
        public async Task GetRepositoriesTest()
        {
            var context = new TestLambdaContext();
            var request = new APIGatewayProxyRequest
            {
                Path = "/repositories"
            };

            var response = await functions.GetRepositoriesAsync(request, context);

            string expected =
            "{" +
                "\"resources\":" +
                "[" +
                    "{\"href\":\"/repositories/firstRepo\",\"displayData\":\"firstRepo\",\"owner\":\"imaUser\"}," +
                    "{\"href\":\"/repositories/yetAnotherRepo\",\"displayData\":\"yetAnotherRepo\",\"owner\":\"imaUser\"}," +
                    "{\"href\":\"/repositories/helloRepo\",\"displayData\":\"helloRepo\",\"owner\":\"imanotherUser\"}" +
                "]" +
            "}";

            Assert.AreEqual(200, response.StatusCode);
            Assert.AreEqual(response.Body, expected);
        }

        [TestMethod]
        public async Task GetBranchTest()
        {
            var context = new TestLambdaContext();
            var request = new APIGatewayProxyRequest
            {
                Path = "/repositories/helloRepo/branches/helloBranch",
                PathParameters = new Dictionary<string, string>
                {
                    { "repositoryId", "helloRepo" },
                    { "branchId", "helloBranch" }
                }
            };

            var response = await functions.GetBranchAsync(request, context);

            Assert.AreEqual(200, response.StatusCode);
        }


        [TestMethod]
        public void TestEmptyPermissionRequests()
        {
            using (var db = new ReVersion_DatabaseContext())
            {
                List<PermissionLookup> actual = SQLOperations.QueryPermissionRequests(db, "adam");

                List<PermissionLookup> expected = new List<PermissionLookup>();

                Assert.AreEqual(actual.Count, expected.Count);
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
