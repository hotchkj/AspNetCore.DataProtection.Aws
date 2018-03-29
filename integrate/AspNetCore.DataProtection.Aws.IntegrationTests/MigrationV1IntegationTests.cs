// Copyright(c) 2018 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System;
using System.Threading.Tasks;
using Amazon.S3;
using AspNetCore.DataProtection.Aws.Kms;
using AspNetCore.DataProtection.Aws.S3;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AspNetCore.DataProtection.Aws.IntegrationTests
{
    public class MigrationV1IntegationTests : IClassFixture<MigrationV1Fixture>
    {
        private readonly MigrationV1Fixture fixture;

        public MigrationV1IntegationTests(MigrationV1Fixture fixture)
        {
            this.fixture = fixture;
        }

        [Theory]
        [InlineData(MigrationV1Fixture.TestName.DefaultNoKms)]
        [InlineData(MigrationV1Fixture.TestName.NoCompressionNoKms)]
        [InlineData(MigrationV1Fixture.TestName.KmsServerSideNoKms)]
        [InlineData(MigrationV1Fixture.TestName.CustomServerSideNoKms)]
        public Task ExpectPureS3MigrationToSucceed(MigrationV1Fixture.TestName lookupKey)
        {
            var config = fixture.Configurations[lookupKey];

            Assert.Equal(MigrationV1Fixture.ConfigTypeS3, config[MigrationV1Fixture.ConfigType]);

            return ExpectS3StorageSuccess(config);
        }

        [Theory]
        [InlineData(MigrationV1Fixture.TestName.DefaultWithKms)]
        public Task ExpectS3AndKmsMigrationToSucceed(MigrationV1Fixture.TestName lookupKey)
        {
            var config = fixture.Configurations[lookupKey];

            Assert.Equal(MigrationV1Fixture.ConfigTypeKms, config[MigrationV1Fixture.ConfigType]);

            return ExpectS3AndKmsStorageSuccess(config);
        }

        private async Task ExpectS3StorageSuccess(JObject config)
        {
            await fixture.EnsureDataPopulated();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(fixture.S3Client);
            serviceCollection.AddDataProtection()
                             .PersistKeysToAwsS3(ToNewS3Config(config));

            using (var serviceProvider = serviceCollection.BuildServiceProvider())
            {
                var dataToDecrypt = Convert.FromBase64String(config[MigrationV1Fixture.EncryptedData].Value<string>());
                var prov = serviceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector(config[MigrationV1Fixture.ProtectorKey].Value<string>());

                Assert.Equal(fixture.Plaintext, prov.Unprotect(dataToDecrypt));
            }
        }

        private async Task ExpectS3AndKmsStorageSuccess(JObject config)
        {
            await fixture.EnsureDataPopulated();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(fixture.S3Client);
            serviceCollection.AddSingleton(fixture.KmsClient);
            serviceCollection.AddDataProtection()
                             .SetApplicationName(config[MigrationV1Fixture.ApplicationNameKey].Value<string>())
                             .PersistKeysToAwsS3(ToNewS3Config(config))
                             .ProtectKeysWithAwsKms(ToNewKmsConfig(config));

            using (var serviceProvider = serviceCollection.BuildServiceProvider())
            {
                var dataToDecrypt = Convert.FromBase64String(config[MigrationV1Fixture.EncryptedData].Value<string>());
                var prov = serviceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector(config[MigrationV1Fixture.ProtectorKey].Value<string>());

                Assert.Equal(fixture.Plaintext, prov.Unprotect(dataToDecrypt));
            }
        }

        private static S3XmlRepositoryConfig ToNewS3Config(JObject config)
        {
            var newConfig = new S3XmlRepositoryConfig(config[nameof(S3XmlRepositoryConfig.Bucket)].Value<string>());

            if (config.TryGetValue(nameof(S3XmlRepositoryConfig.KeyPrefix), out JToken keyprefix))
            {
                newConfig.KeyPrefix = keyprefix.Value<string>();
            }

            if (config.TryGetValue(nameof(S3XmlRepositoryConfig.MaxS3QueryConcurrency), out JToken concurrency))
            {
                newConfig.MaxS3QueryConcurrency = concurrency.Value<int>();
            }

            if (config.TryGetValue(nameof(S3XmlRepositoryConfig.StorageClass), out JToken storageClass))
            {
                newConfig.StorageClass = S3StorageClass.FindValue(storageClass.Value<string>());
            }

            if (config.TryGetValue(nameof(S3XmlRepositoryConfig.ServerSideEncryptionMethod), out JToken serverSideEncryptionMethod))
            {
                newConfig.ServerSideEncryptionMethod = ServerSideEncryptionMethod.FindValue(serverSideEncryptionMethod.Value<string>());
            }

            if (config.TryGetValue(nameof(S3XmlRepositoryConfig.ServerSideEncryptionCustomerMethod), out JToken serverSideEncryptionCustomerMethod))
            {
                newConfig.ServerSideEncryptionCustomerMethod = ServerSideEncryptionCustomerMethod.FindValue(serverSideEncryptionCustomerMethod.Value<string>());
            }

            if (config.TryGetValue(nameof(S3XmlRepositoryConfig.ServerSideEncryptionCustomerProvidedKey), out JToken serverSideEncryptionCustomerProvidedKey))
            {
                newConfig.ServerSideEncryptionCustomerProvidedKey = serverSideEncryptionCustomerProvidedKey.Value<string>();
            }

            if (config.TryGetValue(nameof(S3XmlRepositoryConfig.ServerSideEncryptionCustomerProvidedKeyMd5), out JToken serverSideEncryptionCustomerProvidedKeyMd5))
            {
                newConfig.ServerSideEncryptionCustomerProvidedKeyMd5 = serverSideEncryptionCustomerProvidedKeyMd5.Value<string>();
            }

            if (config.TryGetValue(nameof(S3XmlRepositoryConfig.ServerSideEncryptionKeyManagementServiceKeyId), out JToken serverSideEncryptionKeyManagementServiceKeyId))
            {
                newConfig.ServerSideEncryptionKeyManagementServiceKeyId = serverSideEncryptionKeyManagementServiceKeyId.Value<string>();
            }

            if (config.TryGetValue(nameof(S3XmlRepositoryConfig.ClientSideCompression), out JToken clientSideCompression))
            {
                newConfig.ClientSideCompression = clientSideCompression.Value<bool>();
            }

            return newConfig;
        }

        private static KmsXmlEncryptorConfig ToNewKmsConfig(JObject config)
        {
            var newConfig = new KmsXmlEncryptorConfig(config[nameof(KmsXmlEncryptorConfig.KeyId)].Value<string>());

            var appName = config[MigrationV1Fixture.ApplicationNameKey].Value<string>();
            var kmsAppName = config[MigrationV1Fixture.KmsApplicationNameKey].Value<string>();

            if(appName == kmsAppName)
            {
                // Normal migration requires the following settings
                newConfig.DiscriminatorAsContext = true;
                newConfig.HashDiscriminatorContext = false;
            }
            else
            {
                // If the app names differed, migration requires special contexts
                newConfig.DiscriminatorAsContext = false;
                newConfig.HashDiscriminatorContext = false;
                newConfig.EncryptionContext[KmsConstants.ApplicationEncryptionContextKey] = kmsAppName;
            }

            return newConfig;
        }
    }
}
