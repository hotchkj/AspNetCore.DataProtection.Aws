// Copyright(c) 2016 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Amazon;
using Amazon.S3;
using AspNetCore.DataProtection.Aws.S3;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace AspNetCore.DataProtection.Aws.IntegrationTests
{
    public sealed class S3IntegrationTests : IDisposable
    {
        private readonly IAmazonS3 s3client;
        private readonly S3XmlRepository xmlRepo;
        private readonly S3XmlRepositoryConfig config;
        // Usual max keys in S3 queries is 1000, so this should ensure we have several re-queries
        private const int LargeTestNumber = 2100;
        private const string ElementName = "name";
        private const string ElementContent = "test";

        public S3IntegrationTests()
        {
            // Expectation that local SDK has been configured correctly, whether via VS Tools or user config files
            s3client = new AmazonS3Client(RegionEndpoint.EUWest1);
            // Sadly S3 bucket names are globally unique, so other testers without write access need to change this name
            config = new S3XmlRepositoryConfig("hotchkj-dataprotection-s3-integration-tests-eu-west-1");
            xmlRepo = new S3XmlRepository(s3client, config);
        }

        public void Dispose()
        {
            s3client.Dispose();
        }

        public async Task PrepareLargeQueryTest()
        {
            config.KeyPrefix = "LargeQueryTest/";

            var myXml = new XElement(ElementName, ElementContent);

            for (int i = 0; i < LargeTestNumber; ++i)
            {
                await xmlRepo.StoreElementAsync(myXml, "LargeQueryTest" + i.ToString(), CancellationToken.None);
            }
        }

        [Fact]
        public async Task ExpectStoreToSucceed()
        {
            config.KeyPrefix = "DefaultTesting/";

            var myXml = new XElement(ElementName, ElementContent);
            var myTestName = "friendly";

            await xmlRepo.StoreElementAsync(myXml, myTestName, CancellationToken.None);
        }

        [Fact]
        public async Task ExpectNonExistentQueryToSucceedWithZero()
        {
            config.KeyPrefix = "DoesntExist/";

            var list = await xmlRepo.GetAllElementsAsync(CancellationToken.None);

            Assert.Equal(0, list.Count);
        }

        [Fact]
        public async Task ExpectEmptyQueryToSucceedWithZero()
        {
            config.KeyPrefix = "NothingHere/";

            var list = await xmlRepo.GetAllElementsAsync(CancellationToken.None);

            Assert.Equal(0, list.Count);
        }

        [Fact]
        public async Task ExpectLargeQueryToSucceed()
        {
            config.KeyPrefix = "LargeQueryTest/";

            var list = await xmlRepo.GetAllElementsAsync(CancellationToken.None);
            
            var expected = new XElement(ElementName, ElementContent);
            Assert.Equal(LargeTestNumber, list.Count);
            foreach(var item in list)
            {
                Assert.True(XNode.DeepEquals(expected, item));
            }
        }
    }
}
