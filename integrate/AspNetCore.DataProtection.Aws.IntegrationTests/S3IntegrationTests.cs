// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Amazon;
using Amazon.S3;
using AspNetCore.DataProtection.Aws.S3;
using Xunit;

namespace AspNetCore.DataProtection.Aws.IntegrationTests
{
    public sealed class S3IntegrationTests : IDisposable
    {
        private readonly IAmazonS3 s3Client;
        private readonly ICleanupS3 s3Cleanup;
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
            s3Client = new AmazonS3Client(RegionEndpoint.EUWest1);
            // Override the default for ease of debugging. Explicitly turn on for compression tests.
            config = new S3XmlRepositoryConfig(BucketName) { ClientSideCompression = false };
            xmlRepo = new S3XmlRepository(s3Client, new DirectOptionsSnapshot<S3XmlRepositoryConfig>(config));
            s3Cleanup = new CleanupS3(s3Client);
        }

        public void Dispose()
        {
            s3Client.Dispose();
        }

        public async Task PrepareLargeQueryTest()
        {
            config.KeyPrefix = "LargeQueryTest/";

            var myXml = new XElement(ElementName, ElementContent);

            for (var i = 0; i < LargeTestNumber; ++i)
            {
                await xmlRepo.StoreElementAsync(myXml, "LargeQueryTest" + i, CancellationToken.None);
            }
        }

        [Fact]
        public async Task ExpectDefaultStoreRetrieveToSucceed()
        {
            config.KeyPrefix = "DefaultTesting/";
            await s3Cleanup.ClearKeys(BucketName, config.KeyPrefix);

            var myXml = new XElement(ElementName, ElementContent);
            var myTestName = "friendly";

            await xmlRepo.StoreElementAsync(myXml, myTestName, CancellationToken.None);

            IReadOnlyCollection<XElement> list = await xmlRepo.GetAllElementsAsync(CancellationToken.None);

            Assert.Equal(1, list.Count);
            Assert.True(XNode.DeepEquals(myXml, list.First()));
        }

        [Fact]
        public async Task ExpectCompressedStoreRetrieveToSucceed()
        {
            config.KeyPrefix = "CompressTesting/";
            config.ClientSideCompression = true;
            await s3Cleanup.ClearKeys(BucketName, config.KeyPrefix);

            var myXml = new XElement(ElementName, ElementContent);
            var myTestName = "friendly_compressed";

            await xmlRepo.StoreElementAsync(myXml, myTestName, CancellationToken.None);

            IReadOnlyCollection<XElement> list = await xmlRepo.GetAllElementsAsync(CancellationToken.None);

            Assert.Equal(1, list.Count);
            Assert.True(XNode.DeepEquals(myXml, list.First()));
        }

        [Fact]
        public async Task ExpectCompatibilityToCompressedStoreRetrieveToSucceed()
        {
            config.KeyPrefix = "ForwardsCompatibilityCompressTesting/";
            config.ClientSideCompression = false;
            await s3Cleanup.ClearKeys(BucketName, config.KeyPrefix);

            var myXml = new XElement(ElementName, ElementContent);
            var myTestName = "friendly_not_so_compressed";

            await xmlRepo.StoreElementAsync(myXml, myTestName, CancellationToken.None);

            config.ClientSideCompression = true;

            IReadOnlyCollection<XElement> list = await xmlRepo.GetAllElementsAsync(CancellationToken.None);

            Assert.Equal(1, list.Count);
            Assert.True(XNode.DeepEquals(myXml, list.First()));
        }

        [Fact]
        public async Task ExpectCompatibilityFromCompressedStoreRetrieveToSucceed()
        {
            config.KeyPrefix = "BackwardsCompatibilityCompressTesting/";
            config.ClientSideCompression = true;
            await s3Cleanup.ClearKeys(BucketName, config.KeyPrefix);

            var myXml = new XElement(ElementName, ElementContent);
            var myTestName = "friendly_compressed";

            await xmlRepo.StoreElementAsync(myXml, myTestName, CancellationToken.None);

            config.ClientSideCompression = false;

            IReadOnlyCollection<XElement> list = await xmlRepo.GetAllElementsAsync(CancellationToken.None);

            Assert.Equal(1, list.Count);
            Assert.True(XNode.DeepEquals(myXml, list.First()));
        }

        [Fact]
        public async Task ExpectKmsStoreRetrieveToSucceed()
        {
            config.KeyPrefix = "KmsTesting/";
            config.ServerSideEncryptionKeyManagementServiceKeyId = "alias/KmsIntegrationTesting";
            config.ServerSideEncryptionMethod = ServerSideEncryptionMethod.AWSKMS;
            await s3Cleanup.ClearKeys(BucketName, config.KeyPrefix);

            var myXml = new XElement(ElementName, ElementContent);
            var myTestName = "friendly";

            await xmlRepo.StoreElementAsync(myXml, myTestName, CancellationToken.None);

            IReadOnlyCollection<XElement> list = await xmlRepo.GetAllElementsAsync(CancellationToken.None);

            Assert.Equal(1, list.Count);
            Assert.True(XNode.DeepEquals(myXml, list.First()));
        }

        [Fact]
        public async Task ExpectCustomStoreRetrieveToSucceed()
        {
            config.KeyPrefix = "CustomKeyTesting/";
            config.ServerSideEncryptionCustomerMethod = ServerSideEncryptionCustomerMethod.AES256;
            config.ServerSideEncryptionCustomerProvidedKey = "x+AmYqxeD//Ky4vt0HmXxSVGll7TgEkJK6iTPGqFJbk=";
            await s3Cleanup.ClearKeys(BucketName, config.KeyPrefix);

            var myXml = new XElement(ElementName, ElementContent);
            var myTestName = "friendly";

            await xmlRepo.StoreElementAsync(myXml, myTestName, CancellationToken.None);

            IReadOnlyCollection<XElement> list = await xmlRepo.GetAllElementsAsync(CancellationToken.None);

            Assert.Equal(1, list.Count);
            Assert.True(XNode.DeepEquals(myXml, list.First()));
        }

        [Fact]
        public async Task ExpectNonExistentQueryToSucceedWithZero()
        {
            config.KeyPrefix = "DoesntExist/";

            IReadOnlyCollection<XElement> list = await xmlRepo.GetAllElementsAsync(CancellationToken.None);

            Assert.Equal(0, list.Count);
        }

        [Fact]
        public async Task ExpectEmptyQueryToSucceedWithZero()
        {
            config.KeyPrefix = "NothingHere/";

            IReadOnlyCollection<XElement> list = await xmlRepo.GetAllElementsAsync(CancellationToken.None);

            Assert.Equal(0, list.Count);
        }

        [Fact]
        public async Task ExpectLargeQueryToSucceed()
        {
            config.KeyPrefix = "LargeQueryTest/";

            IReadOnlyCollection<XElement> list = await xmlRepo.GetAllElementsAsync(CancellationToken.None);

            var expected = new XElement(ElementName, ElementContent);
            Assert.Equal(LargeTestNumber, list.Count);
            foreach (var item in list)
            {
                Assert.True(XNode.DeepEquals(expected, item));
            }
        }
    }
}
