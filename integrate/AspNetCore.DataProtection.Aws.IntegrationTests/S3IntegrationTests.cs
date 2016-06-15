// Copyright(c) 2016 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Amazon;
using Amazon.S3;
using AspNetCore.DataProtection.Aws.S3;
using System;
using System.Linq;
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
        // Sadly S3 bucket names are globally unique, so other testers without write access need to change this name
        internal const string BucketName = "hotchkj-dataprotection-s3-integration-tests-eu-west-1";

        public S3IntegrationTests()
        {
            // Expectation that local SDK has been configured correctly, whether via VS Tools or user config files
            s3client = new AmazonS3Client(RegionEndpoint.EUWest1);
            config = new S3XmlRepositoryConfig(BucketName);
            xmlRepo = new S3XmlRepository(s3client, config);
        }

        public void Dispose()
        {
            s3client.Dispose();
        }

        public async Task PrepareLargeQueryTest()
        {
            try
            {
                config.KeyPrefix = "LargeQueryTest/";

                var myXml = new XElement(ElementName, ElementContent);

                for (int i = 0; i < LargeTestNumber; ++i)
                {
                    await xmlRepo.StoreElementAsync(myXml, "LargeQueryTest" + i, CancellationToken.None);
                }
            }
            finally
            {
                config.SetToDefaults();
            }
        }

        [Fact]
        public async Task ExpectDefaultStoreRetrieveToSucceed()
        {
            try
            {
                config.KeyPrefix = "DefaultTesting/";

                var myXml = new XElement(ElementName, ElementContent);
                var myTestName = "friendly";

                await xmlRepo.StoreElementAsync(myXml, myTestName, CancellationToken.None);

                var list = await xmlRepo.GetAllElementsAsync(CancellationToken.None);

                Assert.Equal(1, list.Count);
                Assert.True(XNode.DeepEquals(myXml, list.First()));
            }
            finally
            {
                config.SetToDefaults();
            }
        }

        [Fact]
        public async Task ExpectKmsStoreRetrieveToSucceed()
        {
            try
            {
                config.KeyPrefix = "KmsTesting/";
                config.ServerSideEncryptionKeyManagementServiceKeyId = "alias/KmsIntegrationTesting";
                config.ServerSideEncryptionMethod = ServerSideEncryptionMethod.AWSKMS;

                var myXml = new XElement(ElementName, ElementContent);
                var myTestName = "friendly";

                await xmlRepo.StoreElementAsync(myXml, myTestName, CancellationToken.None);

                var list = await xmlRepo.GetAllElementsAsync(CancellationToken.None);

                Assert.Equal(1, list.Count);
                Assert.True(XNode.DeepEquals(myXml, list.First()));
            }
            finally
            {
                config.SetToDefaults();
            }
        }

        [Fact]
        public async Task ExpectCustomStoreRetrieveToSucceed()
        {
            try
            {
                config.KeyPrefix = "CustomKeyTesting/";
                config.ServerSideEncryptionCustomerMethod = ServerSideEncryptionCustomerMethod.AES256;
                config.ServerSideEncryptionCustomerProvidedKey = "x+AmYqxeD//Ky4vt0HmXxSVGll7TgEkJK6iTPGqFJbk=";

                var myXml = new XElement(ElementName, ElementContent);
                var myTestName = "friendly";

                await xmlRepo.StoreElementAsync(myXml, myTestName, CancellationToken.None);

                var list = await xmlRepo.GetAllElementsAsync(CancellationToken.None);

                Assert.Equal(1, list.Count);
                Assert.True(XNode.DeepEquals(myXml, list.First()));
            }
            finally
            {
                config.SetToDefaults();
            }
        }

        [Fact]
        public async Task ExpectNonExistentQueryToSucceedWithZero()
        {
            try
            {
                config.KeyPrefix = "DoesntExist/";

                var list = await xmlRepo.GetAllElementsAsync(CancellationToken.None);

                Assert.Equal(0, list.Count);
            }
            finally
            {
                config.SetToDefaults();
            }
        }

        [Fact]
        public async Task ExpectEmptyQueryToSucceedWithZero()
        {
            try
            {
                config.KeyPrefix = "NothingHere/";

                var list = await xmlRepo.GetAllElementsAsync(CancellationToken.None);

                Assert.Equal(0, list.Count);
            }
            finally
            {
                config.SetToDefaults();
            }
        }

        [Fact]
        public async Task ExpectLargeQueryToSucceed()
        {
            try
            {
                config.KeyPrefix = "LargeQueryTest/";

                var list = await xmlRepo.GetAllElementsAsync(CancellationToken.None);

                var expected = new XElement(ElementName, ElementContent);
                Assert.Equal(LargeTestNumber, list.Count);
                foreach (var item in list)
                {
                    Assert.True(XNode.DeepEquals(expected, item));
                }
            }
            finally
            {
                config.SetToDefaults();
            }
        }
    }
}
