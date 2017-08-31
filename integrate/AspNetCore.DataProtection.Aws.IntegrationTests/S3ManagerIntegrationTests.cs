// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using AspNetCore.DataProtection.Aws.S3;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.Internal;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AspNetCore.DataProtection.Aws.IntegrationTests
{
    public sealed class S3ManagerIntegrationTests : IClassFixture<ConfigurationFixture>, IDisposable
    {
        private readonly IAmazonS3 s3Client;
        private readonly ICleanupS3 s3Cleanup;
        private readonly ConfigurationFixture fixture;

        public S3ManagerIntegrationTests(ConfigurationFixture fixture)
        {
            this.fixture = fixture;

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
            using (var serviceProvider = serviceCollection.BuildServiceProvider())
            {
                var keyManager = new XmlKeyManager(serviceProvider.GetRequiredService<IOptions<KeyManagementOptions>>(),
                                                   serviceProvider.GetRequiredService<IActivator>());

                var activationDate = new DateTimeOffset(new DateTime(1980, 1, 1));
                var expirationDate = new DateTimeOffset(new DateTime(1980, 6, 1));
                keyManager.CreateNewKey(activationDate, expirationDate);

                IReadOnlyCollection<IKey> keys = keyManager.GetAllKeys();

                Assert.Equal(1, keys.Count);
                Assert.Equal(activationDate, keys.Single().ActivationDate);
                Assert.Equal(expirationDate, keys.Single().ExpirationDate);
                Assert.NotNull(keys.Single().Descriptor);
            }
        }

        [Fact]
        public async Task ExpectFullKeyManagerExplicitAwsStoreRetrieveWithConfigToSucceed()
        {
            var section = fixture.Configuration.GetSection("s3ExplicitAwsTestCase");

            // Just make sure config is what is actually expected - of course normally you'd not access the config like this directly
            Assert.Equal(S3IntegrationTests.BucketName, section["bucket"]);
            Assert.Equal("RealXmlKeyManager2/", section["keyPrefix"]);

            await s3Cleanup.ClearKeys(S3IntegrationTests.BucketName, section["keyPrefix"]);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddDataProtection()
                             .PersistKeysToAwsS3(s3Client, section);
            using (var serviceProvider = serviceCollection.BuildServiceProvider())
            {
                var keyManager = new XmlKeyManager(serviceProvider.GetRequiredService<IOptions<KeyManagementOptions>>(),
                                                   serviceProvider.GetRequiredService<IActivator>());

                var activationDate = new DateTimeOffset(new DateTime(1980, 1, 1));
                var expirationDate = new DateTimeOffset(new DateTime(1980, 6, 1));
                keyManager.CreateNewKey(activationDate, expirationDate);

                IReadOnlyCollection<IKey> keys = keyManager.GetAllKeys();

                Assert.Equal(1, keys.Count);
                Assert.Equal(activationDate, keys.Single().ActivationDate);
                Assert.Equal(expirationDate, keys.Single().ExpirationDate);
                Assert.NotNull(keys.Single().Descriptor);
            }
        }

        [Fact]
        public async Task ExpectFullKeyManagerStoreRetrieveWithConfigToSucceed()
        {
            var section = fixture.Configuration.GetSection("s3ImplicitAwsTestCase");

            // Just make sure config is what is actually expected - of course normally you'd not access the config like this directly
            Assert.Equal(S3IntegrationTests.BucketName, section["bucket"]);
            Assert.Equal("RealXmlKeyManager3/", section["keyPrefix"]);

            await s3Cleanup.ClearKeys(S3IntegrationTests.BucketName, section["keyPrefix"]);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(s3Client);
            serviceCollection.AddDataProtection()
                             .PersistKeysToAwsS3(section);
            using (var serviceProvider = serviceCollection.BuildServiceProvider())
            {
                var keyManager = new XmlKeyManager(serviceProvider.GetRequiredService<IOptions<KeyManagementOptions>>(),
                                                   serviceProvider.GetRequiredService<IActivator>());

                var activationDate = new DateTimeOffset(new DateTime(1980, 1, 1));
                var expirationDate = new DateTimeOffset(new DateTime(1980, 6, 1));
                keyManager.CreateNewKey(activationDate, expirationDate);

                IReadOnlyCollection<IKey> keys = keyManager.GetAllKeys();

                Assert.Equal(1, keys.Count);
                Assert.Equal(activationDate, keys.Single().ActivationDate);
                Assert.Equal(expirationDate, keys.Single().ExpirationDate);
                Assert.NotNull(keys.Single().Descriptor);
            }
        }

        [Fact]
        public async Task ExpectFullKeyManagerStoreRetrieveToSucceed()
        {
            var config = new S3XmlRepositoryConfig(S3IntegrationTests.BucketName) { KeyPrefix = "RealXmlKeyManager4/" };
            await s3Cleanup.ClearKeys(S3IntegrationTests.BucketName, config.KeyPrefix);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(s3Client);
            serviceCollection.AddDataProtection()
                             .PersistKeysToAwsS3(config);
            using (var serviceProvider = serviceCollection.BuildServiceProvider())
            {
                var keyManager = new XmlKeyManager(serviceProvider.GetRequiredService<IOptions<KeyManagementOptions>>(),
                                                   serviceProvider.GetRequiredService<IActivator>());

                var activationDate = new DateTimeOffset(new DateTime(1980, 1, 1));
                var expirationDate = new DateTimeOffset(new DateTime(1980, 6, 1));
                keyManager.CreateNewKey(activationDate, expirationDate);

                IReadOnlyCollection<IKey> keys = keyManager.GetAllKeys();

                Assert.Equal(1, keys.Count);
                Assert.Equal(activationDate, keys.Single().ActivationDate);
                Assert.Equal(expirationDate, keys.Single().ExpirationDate);
                Assert.NotNull(keys.Single().Descriptor);
            }
        }

        [Fact]
        public async Task ExpectProtectRoundTripToSucceed()
        {
            var config = new S3XmlRepositoryConfig(S3IntegrationTests.BucketName) { KeyPrefix = "RealXmlKeyManager5/" };
            await s3Cleanup.ClearKeys(S3IntegrationTests.BucketName, config.KeyPrefix);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(s3Client);
            serviceCollection.AddDataProtection()
                             .PersistKeysToAwsS3(config);
            using (var serviceProvider = serviceCollection.BuildServiceProvider())
            {
                var prov = serviceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("bob");

                var plaintext = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
                var encrypted = prov.Protect(plaintext);
                var decrypted = prov.Unprotect(encrypted);
                Assert.Equal(plaintext, decrypted);
            }
        }

        [Theory]
        [InlineData("test1", "test2", true)]
        [InlineData("test1", "test1", false)]
        public async Task ExpectApplicationIsolationToThrow(string app1, string app2, bool throws)
        {
            var config = new S3XmlRepositoryConfig(S3IntegrationTests.BucketName) { KeyPrefix = "RealXmlKeyManager6/" };
            await s3Cleanup.ClearKeys(S3IntegrationTests.BucketName, config.KeyPrefix);

            var plaintext = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            byte[] encrypted;

            {
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(s3Client);
                serviceCollection.AddDataProtection()
                                 .SetApplicationName(app1)
                                 .PersistKeysToAwsS3(config);
                using (var serviceProvider = serviceCollection.BuildServiceProvider())
                {
                    var prov = serviceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("bob");

                    encrypted = prov.Protect(plaintext);
                }
            }

            {
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(s3Client);
                serviceCollection.AddDataProtection()
                                 .SetApplicationName(app2)
                                 .PersistKeysToAwsS3(config);
                using (var serviceProvider = serviceCollection.BuildServiceProvider())
                {
                    var prov = serviceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("bob");

                    if (throws)
                    {
                        Assert.Throws<CryptographicException>(() => prov.Unprotect(encrypted));
                    }
                    else
                    {
                        Assert.NotNull(prov.Unprotect(encrypted));
                    }
                }
            }
        }
    }
}
