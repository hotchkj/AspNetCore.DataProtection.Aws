// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Amazon;
using Amazon.KeyManagementService;
using AspNetCore.DataProtection.Aws.Kms;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.Internal;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AspNetCore.DataProtection.Aws.IntegrationTests
{
    public class KmsManagerIntegrationTests : IDisposable
    {
        private readonly IAmazonKeyManagementService kmsClient;

        public KmsManagerIntegrationTests()
        {
            // Expectation that local SDK has been configured correctly, whether via VS Tools or user config files
            kmsClient = new AmazonKeyManagementServiceClient(RegionEndpoint.EUWest1);
        }

        public void Dispose()
        {
            kmsClient.Dispose();
        }

        [Fact]
        public void ExpectFullKeyManagerExplicitAwsStoreRetrieveToSucceed()
        {
            var config = new KmsXmlEncryptorConfig(KmsIntegrationTests.KmsTestingKey);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddDataProtection()
                             .PersistKeysToEphemeral()
                             .ProtectKeysWithAwsKms(kmsClient, config);
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
        public void ExpectFullKeyManagerStoreRetrieveToSucceed()
        {
            var config = new KmsXmlEncryptorConfig(KmsIntegrationTests.KmsTestingKey);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(kmsClient);
            serviceCollection.AddDataProtection()
                             .PersistKeysToEphemeral()
                             .ProtectKeysWithAwsKms(config);
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
        public void ExpectProtectRoundTripToSucceed()
        {
            var config = new KmsXmlEncryptorConfig(KmsIntegrationTests.KmsTestingKey);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(kmsClient);
            serviceCollection.AddDataProtection()
                             .PersistKeysToEphemeral()
                             .ProtectKeysWithAwsKms(config);
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
        public void ExpectApplicationIsolationToThrow(string app1, string app2, bool throws)
        {
            var config = new KmsXmlEncryptorConfig(KmsIntegrationTests.KmsTestingKey);

            var sharedStorage = new EphemeralXmlRepository();
            var plaintext = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            byte[] encrypted;

            {
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(kmsClient);
                serviceCollection.AddDataProtection()
                                 .SetApplicationName(app1)
                                 .PersistKeysToEphemeral(sharedStorage)
                                 .ProtectKeysWithAwsKms(config);
                using (var serviceProvider = serviceCollection.BuildServiceProvider())
                {
                    var prov = serviceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("bob");

                    encrypted = prov.Protect(plaintext);
                }
            }

            {
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(kmsClient);
                serviceCollection.AddDataProtection()
                                 .SetApplicationName(app2)
                                 .PersistKeysToEphemeral(sharedStorage)
                                 .ProtectKeysWithAwsKms(config);
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
