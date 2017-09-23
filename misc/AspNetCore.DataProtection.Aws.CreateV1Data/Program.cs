// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System;
using Amazon;
using Amazon.KeyManagementService;
using Amazon.S3;
using AspNetCore.DataProtection.Aws.Kms;
using AspNetCore.DataProtection.Aws.S3;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;

namespace AspNetCore.DataProtection.Aws.CreateV1Data
{
    internal static class Program
    {
        public const string EndInputValue = "***END OF INPUT***";
        public const string ConfigType = "Control";
        public const string ConfigTypeS3 = "S3";
        public const string ConfigTypeKms = "S3&Kms";
        public const string DataToProtect = "ProtectData";
        public const string ProtectorKey = "Protector";
        public const string ApplicationNameKey = "AppName";
        public const string KmsApplicationNameKey = "KmsAppName";

        // ReSharper disable once UnusedParameter.Local
        private static int Main(string[] args)
        {
            try
            {
                var dataMigration = new CreateMigrationData();

                string consoleRead;
                do
                {
                    // Read configuration from each input line
                    consoleRead = Console.ReadLine();

                    if (consoleRead != EndInputValue)
                    {
                        var configData = ToOldConfig(JObject.Parse(consoleRead));

                        // Dispatch to migration creation based on requested type
                        switch (configData.ControlValue)
                        {
                            case ConfigTypeS3:
                                ReturnBytes(dataMigration.CreateS3(configData.ToEncrypt, configData.Protector, configData.S3Config));
                                break;
                            case ConfigTypeKms:
                                ReturnBytes(dataMigration.CreateS3AndKms(configData.ToEncrypt, configData.Protector, configData.ApplicationName, configData.S3Config, configData.KmsConfig));
                                break;
                            default:
                                throw new NotImplementedException($"Unexpected control value {configData.ControlValue}");
                        }
                    }
                }
                while (consoleRead != EndInputValue);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return 1;
            }

            return 0;
        }

        private static void ReturnBytes(byte[] encrypted)
        {
            Console.WriteLine(Convert.ToBase64String(encrypted));
        }

#pragma warning disable S3242 // JObject is more descriptive than 'more general' IDictionary
        private static (string ControlValue, byte[] ToEncrypt, string Protector, string ApplicationName, S3XmlRepositoryConfig S3Config, KmsXmlEncryptorConfig KmsConfig) ToOldConfig(JObject config)
#pragma warning restore S3242
        {
            var controlValue = config[ConfigType].Value<string>();
            var protectData = Convert.FromBase64String(config[DataToProtect].Value<string>());
            var protectorValue = config[ProtectorKey].Value<string>();

            var old = new S3XmlRepositoryConfig(config[nameof(S3XmlRepositoryConfig.Bucket)].Value<string>());

            if (config.TryGetValue(nameof(S3XmlRepositoryConfig.KeyPrefix), out JToken keyprefix))
            {
                old.KeyPrefix = keyprefix.Value<string>();
            }

            if (config.TryGetValue(nameof(S3XmlRepositoryConfig.MaxS3QueryConcurrency), out JToken concurrency))
            {
                old.MaxS3QueryConcurrency = concurrency.Value<int>();
            }

            if (config.TryGetValue(nameof(S3XmlRepositoryConfig.StorageClass), out JToken storageClass))
            {
                old.StorageClass = S3StorageClass.FindValue(storageClass.Value<string>());
            }

            if (config.TryGetValue(nameof(S3XmlRepositoryConfig.ServerSideEncryptionMethod), out JToken serverSideEncryptionMethod))
            {
                old.ServerSideEncryptionMethod = ServerSideEncryptionMethod.FindValue(serverSideEncryptionMethod.Value<string>());
            }

            if (config.TryGetValue(nameof(S3XmlRepositoryConfig.ServerSideEncryptionCustomerMethod), out JToken serverSideEncryptionCustomerMethod))
            {
                old.ServerSideEncryptionCustomerMethod = ServerSideEncryptionCustomerMethod.FindValue(serverSideEncryptionCustomerMethod.Value<string>());
            }

            if (config.TryGetValue(nameof(S3XmlRepositoryConfig.ServerSideEncryptionCustomerProvidedKey), out JToken serverSideEncryptionCustomerProvidedKey))
            {
                old.ServerSideEncryptionCustomerProvidedKey = serverSideEncryptionCustomerProvidedKey.Value<string>();
            }

            if (config.TryGetValue(nameof(S3XmlRepositoryConfig.ServerSideEncryptionCustomerProvidedKeyMd5), out JToken serverSideEncryptionCustomerProvidedKeyMd5))
            {
                old.ServerSideEncryptionCustomerProvidedKeyMd5 = serverSideEncryptionCustomerProvidedKeyMd5.Value<string>();
            }

            if (config.TryGetValue(nameof(S3XmlRepositoryConfig.ServerSideEncryptionKeyManagementServiceKeyId), out JToken serverSideEncryptionKeyManagementServiceKeyId))
            {
                old.ServerSideEncryptionKeyManagementServiceKeyId = serverSideEncryptionKeyManagementServiceKeyId.Value<string>();
            }

            if (config.TryGetValue(nameof(S3XmlRepositoryConfig.ClientSideCompression), out JToken clientSideCompression))
            {
                old.ClientSideCompression = clientSideCompression.Value<bool>();
            }

            string applicationName = null;
            if (config.TryGetValue(ApplicationNameKey, out JToken appName))
            {
                applicationName = appName.Value<string>();
            }

            string kmsApplicationName = null;
            if (config.TryGetValue(KmsApplicationNameKey, out JToken kmsAppName))
            {
                kmsApplicationName = kmsAppName.Value<string>();
            }

            string keyIdentifier = null;
            if (config.TryGetValue(nameof(KmsXmlEncryptorConfig.KeyId), out JToken keyId))
            {
                keyIdentifier = keyId.Value<string>();
            }

            KmsXmlEncryptorConfig kmsConfig = null;
            if (!string.IsNullOrEmpty(kmsApplicationName) && !string.IsNullOrEmpty(keyIdentifier))
            {
                kmsConfig = new KmsXmlEncryptorConfig(kmsApplicationName, keyIdentifier);
            }

            return (controlValue, protectData, protectorValue, applicationName, old, kmsConfig);
        }
    }

    /// <summary>
    /// Creates migration data using V1 assembly
    /// Assumption that S3 cleanup has already run
    /// </summary>
    public class CreateMigrationData
    {
        private readonly IAmazonS3 s3Client;
        private readonly IAmazonKeyManagementService kmsClient;

        public CreateMigrationData()
        {
            // Expectation that local SDK has been configured correctly, whether via VS Tools or user config files
            s3Client = new AmazonS3Client(RegionEndpoint.EUWest1);
            kmsClient = new AmazonKeyManagementServiceClient(RegionEndpoint.EUWest1);
        }

        public byte[] CreateS3(byte[] toEncrypt, string protector, S3XmlRepositoryConfig config)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddDataProtection()
                             .PersistKeysToAwsS3(s3Client, config);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using (serviceProvider as IDisposable)
            {
                var prov = serviceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector(protector);

                return prov.Protect(toEncrypt);
            }
        }

        public byte[] CreateS3AndKms(byte[] toEncrypt, string protector, string applicationName, S3XmlRepositoryConfig s3Config, KmsXmlEncryptorConfig kmsConfig)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddDataProtection()
                             .SetApplicationName(applicationName)
                             .ProtectKeysWithAwsKms(kmsClient, kmsConfig)
                             .PersistKeysToAwsS3(s3Client, s3Config);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using (serviceProvider as IDisposable)
            {
                var prov = serviceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector(protector);

                return prov.Protect(toEncrypt);
            }
        }
    }
}
