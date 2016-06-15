// Copyright(c) 2016 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Amazon;
using Amazon.KeyManagementService;
using AspNetCore.DataProtection.Aws.Kms;
using Microsoft.AspNet.DataProtection.AuthenticatedEncryption;
using Microsoft.AspNet.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.AspNet.DataProtection.KeyManagement;
using Microsoft.AspNet.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
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
        public void ExpectFullKeyManagerStoreRetrieveToSucceed()
        {
            var config = new KmsXmlEncryptorConfig(KmsIntegrationTests.ApplicationName, KmsIntegrationTests.KmsTestingKey);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddInstance(kmsClient);
            serviceCollection.AddInstance<IAuthenticatedEncryptorConfiguration>(new AuthenticatedEncryptorConfiguration(new AuthenticatedEncryptionOptions()));
            serviceCollection.AddDataProtection();
            serviceCollection.ConfigureDataProtection(configure =>
            {
                configure.ProtectKeysWithAwsKms(config);
            });
            serviceCollection.AddInstance<IXmlRepository>(new EphemeralXmlRepository());
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
