// Copyright(c) 2016 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Amazon;
using Amazon.KeyManagementService;
using Amazon.S3;
using AspNetCore.DataProtection.Aws.Kms;
using AspNetCore.DataProtection.Aws.S3;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AspNetCore.DataProtection.Aws.IntegrationTests
{
    public sealed class CombinedManagerIntegrationTests : IDisposable
    {
        private readonly IAmazonS3 s3client;
        private readonly IAmazonKeyManagementService kmsClient;
        private readonly ICleanupS3 s3cleanup;

        public CombinedManagerIntegrationTests()
        {
            // Expectation that local SDK has been configured correctly, whether via VS Tools or user config files
            s3client = new AmazonS3Client(RegionEndpoint.EUWest1);
            kmsClient = new AmazonKeyManagementServiceClient(RegionEndpoint.EUWest1);
            s3cleanup = new CleanupS3(s3client);
        }

        public void Dispose()
        {
            s3client.Dispose();
            kmsClient.Dispose();
        }

        [Fact]
        public async Task ExpectFullKeyManagerExplicitAwsStoreRetrieveToSucceed()
        {
            var s3Config = new S3XmlRepositoryConfig(S3IntegrationTests.BucketName);
            s3Config.KeyPrefix = "CombinedXmlKeyManager1/";
            await s3cleanup.ClearKeys(S3IntegrationTests.BucketName, s3Config.KeyPrefix);
            var kmsConfig = new KmsXmlEncryptorConfig(KmsIntegrationTests.ApplicationName, KmsIntegrationTests.KmsTestingKey);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddDataProtection()
                             .PersistKeysToAwsS3(s3client, s3Config)
                             .ProtectKeysWithAwsKms(kmsClient, kmsConfig);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var keyManager = new XmlKeyManager(serviceProvider.GetRequiredService<IXmlRepository>(),
                                               serviceProvider.GetRequiredService<IAuthenticatedEncryptorConfiguration>(),
                                               serviceProvider);

            var activationDate = new DateTimeOffset(new DateTime(1980, 1, 1));
            var expirationDate = new DateTimeOffset(new DateTime(1980, 6, 1));
            keyManager.CreateNewKey(activationDate, expirationDate);

            var keys = keyManager.GetAllKeys();

            Assert.Equal(1, keys.Count);
            Assert.Equal(activationDate, keys.Single().ActivationDate);
            Assert.Equal(expirationDate, keys.Single().ExpirationDate);
        }

        [Fact]
        public async Task ExpectFullKeyManagerStoreRetrieveToSucceed()
        {
            var s3Config = new S3XmlRepositoryConfig(S3IntegrationTests.BucketName);
            s3Config.KeyPrefix = "CombinedXmlKeyManager2/";
            await s3cleanup.ClearKeys(S3IntegrationTests.BucketName, s3Config.KeyPrefix);
            var kmsConfig = new KmsXmlEncryptorConfig(KmsIntegrationTests.ApplicationName, KmsIntegrationTests.KmsTestingKey);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(s3client);
            serviceCollection.AddSingleton(kmsClient);
            serviceCollection.AddDataProtection()
                             .PersistKeysToAwsS3(s3Config)
                             .ProtectKeysWithAwsKms(kmsConfig);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var keyManager = new XmlKeyManager(serviceProvider.GetRequiredService<IXmlRepository>(),
                                               serviceProvider.GetRequiredService<IAuthenticatedEncryptorConfiguration>(),
                                               serviceProvider);

            var activationDate = new DateTimeOffset(new DateTime(1980, 1, 1));
            var expirationDate = new DateTimeOffset(new DateTime(1980, 6, 1));
            keyManager.CreateNewKey(activationDate, expirationDate);

            var keys = keyManager.GetAllKeys();

            Assert.Equal(1, keys.Count);
            Assert.Equal(activationDate, keys.Single().ActivationDate);
            Assert.Equal(expirationDate, keys.Single().ExpirationDate);
        }
    }
}
