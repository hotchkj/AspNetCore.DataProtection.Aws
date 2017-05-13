// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Amazon;
using Amazon.S3;
using AspNetCore.DataProtection.Aws.S3;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AspNetCore.DataProtection.Aws.IntegrationTests
{
    public sealed class S3ManagerIntegrationTests : IDisposable
    {
        private readonly IAmazonS3 s3Client;
        private readonly ICleanupS3 s3Cleanup;

        public S3ManagerIntegrationTests()
        {
            // Expectation that local SDK has been configured correctly, whether via VS Tools or user config files
            s3Client = new AmazonS3Client(RegionEndpoint.EUWest1);
            s3Cleanup = new CleanupS3(s3Client);
        }

        public void Dispose()
        {
            s3Client.Dispose();
        }

        [Fact]
        public async Task ExpectFullKeyManagerExplicitAwsStoreRetrieveToSucceed()
        {
            var config = new S3XmlRepositoryConfig(S3IntegrationTests.BucketName) { KeyPrefix = "RealXmlKeyManager1/" };
            await s3Cleanup.ClearKeys(S3IntegrationTests.BucketName, config.KeyPrefix);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddDataProtection()
                             .PersistKeysToAwsS3(s3Client, config);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var keyManager = new XmlKeyManager(serviceProvider.GetRequiredService<IXmlRepository>(),
                                               serviceProvider.GetRequiredService<IAuthenticatedEncryptorConfiguration>(),
                                               serviceProvider);

            var activationDate = new DateTimeOffset(new DateTime(1980, 1, 1));
            var expirationDate = new DateTimeOffset(new DateTime(1980, 6, 1));
            keyManager.CreateNewKey(activationDate, expirationDate);

            IReadOnlyCollection<IKey> keys = keyManager.GetAllKeys();

            Assert.Equal(1, keys.Count);
            Assert.Equal(activationDate, keys.Single().ActivationDate);
            Assert.Equal(expirationDate, keys.Single().ExpirationDate);
        }

        [Fact]
        public async Task ExpectFullKeyManagerStoreRetrieveToSucceed()
        {
            var config = new S3XmlRepositoryConfig(S3IntegrationTests.BucketName) { KeyPrefix = "RealXmlKeyManager2/" };
            await s3Cleanup.ClearKeys(S3IntegrationTests.BucketName, config.KeyPrefix);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(s3Client);
            serviceCollection.AddDataProtection()
                             .PersistKeysToAwsS3(config);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var keyManager = new XmlKeyManager(serviceProvider.GetRequiredService<IXmlRepository>(),
                                               serviceProvider.GetRequiredService<IAuthenticatedEncryptorConfiguration>(),
                                               serviceProvider);

            var activationDate = new DateTimeOffset(new DateTime(1980, 1, 1));
            var expirationDate = new DateTimeOffset(new DateTime(1980, 6, 1));
            keyManager.CreateNewKey(activationDate, expirationDate);

            IReadOnlyCollection<IKey> keys = keyManager.GetAllKeys();

            Assert.Equal(1, keys.Count);
            Assert.Equal(activationDate, keys.Single().ActivationDate);
            Assert.Equal(expirationDate, keys.Single().ExpirationDate);
        }
    }
}
