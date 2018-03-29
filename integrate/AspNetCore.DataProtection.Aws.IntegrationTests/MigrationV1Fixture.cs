// Copyright(c) 2018 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.KeyManagementService;
using Amazon.S3;
using AspNetCore.DataProtection.Aws.Kms;
using AspNetCore.DataProtection.Aws.S3;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;

namespace AspNetCore.DataProtection.Aws.IntegrationTests
{
    public sealed class MigrationV1Fixture
    {
#if DEBUG
        private const string BuildPath = "Debug";
#else
        private const string BuildPath = "Release";
#endif
        public IAmazonS3 S3Client { get; }
        public IAmazonKeyManagementService KmsClient { get; }
        private readonly ICleanupS3 s3Cleanup;
        private readonly AsyncLazy<bool> ensureDataPopulated;

        public const string EndInputValue = "***END OF INPUT***";
        public const string ConfigType = "Control";
        public const string ConfigTypeS3 = "S3";
        public const string ConfigTypeKms = "S3&Kms";
        public const string DataToProtect = "ProtectData";
        public const string EncryptedData = "EncryptedData";
        public const string ProtectorKey = "Protector";
        public const string ProtectorValue = "bob";
        public const string ApplicationNameKey = "AppName";
        public const string KmsApplicationNameKey = "KmsAppName";
        public const string ApplicationNameValue = "fred";

        public readonly byte[] Plaintext = { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        public enum TestName
        {
            DefaultNoKms,
            NoCompressionNoKms,
            KmsServerSideNoKms,
            CustomServerSideNoKms,
            DefaultWithKms,
            DefaultWithKmsDifferentAppNames
        }

        public readonly Dictionary<TestName, JObject> Configurations = new Dictionary<TestName, JObject>
        {
            {
                TestName.DefaultNoKms, new JObject
                {
                    { ConfigType, ConfigTypeS3 },
                    { nameof(S3XmlRepositoryConfig.Bucket), S3IntegrationTests.BucketName },
                    { nameof(S3XmlRepositoryConfig.KeyPrefix), "Migration/V1/DefaultNoKms/" }
                }
            },
            {
                TestName.NoCompressionNoKms, new JObject
                {
                    { ConfigType, ConfigTypeS3 },
                    { nameof(S3XmlRepositoryConfig.Bucket), S3IntegrationTests.BucketName },
                    { nameof(S3XmlRepositoryConfig.KeyPrefix), "Migration/V1/NoCompressionNoKms/" },
                    { nameof(S3XmlRepositoryConfig.ClientSideCompression), false }
                }
            },
            {
                TestName.KmsServerSideNoKms, new JObject
                {
                    { ConfigType, ConfigTypeS3 },
                    { nameof(S3XmlRepositoryConfig.Bucket), S3IntegrationTests.BucketName },
                    { nameof(S3XmlRepositoryConfig.KeyPrefix), "Migration/V1/KmsServerSideNoKms/" },
                    { nameof(S3XmlRepositoryConfig.ServerSideEncryptionKeyManagementServiceKeyId), KmsIntegrationTests.KmsTestingKey },
                    { nameof(S3XmlRepositoryConfig.ServerSideEncryptionMethod), ServerSideEncryptionMethod.AWSKMS.Value }
                }
            },
            {
                TestName.CustomServerSideNoKms, new JObject
                {
                    { ConfigType, ConfigTypeS3 },
                    { nameof(S3XmlRepositoryConfig.Bucket), S3IntegrationTests.BucketName },
                    { nameof(S3XmlRepositoryConfig.KeyPrefix), "Migration/V1/CustomServerSideNoKms/" },
                    { nameof(S3XmlRepositoryConfig.ServerSideEncryptionCustomerMethod), ServerSideEncryptionCustomerMethod.AES256.Value },
                    { nameof(S3XmlRepositoryConfig.ServerSideEncryptionCustomerProvidedKey), "x+AmYqxeD//Ky4vt0HmXxSVGll7TgEkJK6iTPGqFJbk=" }
                }
            },
            {
                TestName.DefaultWithKms, new JObject
                {
                    { ConfigType, ConfigTypeKms },
                    { nameof(S3XmlRepositoryConfig.Bucket), S3IntegrationTests.BucketName },
                    { nameof(S3XmlRepositoryConfig.KeyPrefix), "Migration/V1/DefaultNoKms/" },
                    { ApplicationNameKey, ApplicationNameValue },
                    { KmsApplicationNameKey, ApplicationNameValue },
                    { nameof(KmsXmlEncryptorConfig.KeyId), KmsIntegrationTests.KmsTestingKey }
                }
            },
            {
                TestName.DefaultWithKmsDifferentAppNames, new JObject
                {
                    { ConfigType, ConfigTypeKms },
                    { nameof(S3XmlRepositoryConfig.Bucket), S3IntegrationTests.BucketName },
                    { nameof(S3XmlRepositoryConfig.KeyPrefix), "Migration/V1/DefaultNoKms/" },
                    { ApplicationNameKey, ApplicationNameValue },
                    { KmsApplicationNameKey, "something" },
                    { nameof(KmsXmlEncryptorConfig.KeyId), KmsIntegrationTests.KmsTestingKey }
                }
            }
        };

        public MigrationV1Fixture()
        {
            // Expectation that local SDK has been configured correctly, whether via VS Tools or user config files
            S3Client = new AmazonS3Client(RegionEndpoint.EUWest1);
            KmsClient = new AmazonKeyManagementServiceClient(RegionEndpoint.EUWest1);
            s3Cleanup = new CleanupS3(S3Client);
            ensureDataPopulated = new AsyncLazy<bool>(RunMigrationProcess);
        }

        public async Task EnsureDataPopulated()
        {
            await ensureDataPopulated.Task;
        }

        private async Task<bool> RunMigrationProcess()
        {
            await s3Cleanup.ClearKeys(S3IntegrationTests.BucketName, "Migration/V1/");
            
            // Since we can't load multiple versions of the same assembly in the same context, we need to run migration from outside as another process
            // Fairly horrible code...

            var migrationCreationPath = Path.Combine("..", "..", "..", "..", "..", "misc", "AspNetCore.DataProtection.Aws.CreateV1Data", "bin", BuildPath, "netcoreapp1.1", "AspNetCore.DataProtection.Aws.CreateV1Data.dll");
            
            // dotnet.exe will complain, but this gives a more informative error message
            var absPath = Path.GetFullPath(migrationCreationPath);
            if (!File.Exists(absPath))
            {
                throw new InvalidOperationException($"Process path requested does not exist: {absPath}");
            }

            var migrationArgs = $"\"{absPath}\"";

            var startInfo = new ProcessStartInfo("dotnet", migrationArgs)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false
            };
            string output;
            using (var process = Process.Start(startInfo))
            {
                // As per Jon Skeet's suggestion, run the STDOUT and STDERR buffering on separate threads to allow the process to read and write throughout startup and shutdown
                var outputTask = Task.Run(process.StandardOutput.ReadToEndAsync);
                var errorTask = Task.Run(process.StandardError.ReadToEndAsync);

                try
                {
                    // Send actual configs over, migration will convert JSON to meaningful V1 config
                    foreach (var config in Configurations.Values)
                    {
                        await SendConfig(process.StandardInput, config).ConfigureAwait(false);
                    }
                    await process.StandardInput.WriteLineAsync(EndInputValue).ConfigureAwait(false);

                    if (!process.WaitForExit((int)TimeSpan.FromSeconds(30).TotalMilliseconds))
                    {
                        process.Close();
                    }

                    if (!process.WaitForExit((int)TimeSpan.FromSeconds(3).TotalMilliseconds))
                    {
                        process.Kill();
                    }
                }
                catch (Exception) when (KillProcess(process))
                {
                }

                process.WaitForExit();

                output = await outputTask.ConfigureAwait(false);
                var error = await errorTask.ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Migration process hasn't terminated cleanly, code: {process.ExitCode}{Environment.NewLine}Output:{Environment.NewLine}{output}{Environment.NewLine}Error:{Environment.NewLine}{error}");
                }
            }

            using (var reader = new StringReader(output))
            {
                foreach (var config in Configurations.Values)
                {
                    config[EncryptedData] = await reader.ReadLineAsync().ConfigureAwait(false);
                }
            }

            return true;
        }

        private bool KillProcess(Process process)
        {
            // Nothing we can do if this throws, and it would just fail the filter anyway
            process.Kill();
            return false;
        }

        private Task SendConfig(StreamWriter writer, JObject config)
        {
            config[DataToProtect] = Convert.ToBase64String(Plaintext);
            config[ProtectorKey] = ProtectorValue;
            return writer.WriteLineAsync(config.ToString(Formatting.None));
        }
    }
}
