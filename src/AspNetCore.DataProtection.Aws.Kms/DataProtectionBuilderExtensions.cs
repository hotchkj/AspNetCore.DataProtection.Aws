// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System;
using Amazon.KeyManagementService;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AspNetCore.DataProtection.Aws.Kms
{
    /// <summary>
    /// Extensions for configuring data protection using an <see cref="IDataProtectionBuilder"/>.
    /// </summary>
    public static class DataProtectionBuilderExtensions
    {
        /// <summary>
        /// Configures the data protection system to encrypt keys using AWS Key Management Service master keys
        /// </summary>
        /// <param name="builder">The <see cref="IDataProtectionBuilder"/>.</param>
        /// <param name="kmsClient">KMS client configured with appropriate credentials.</param>
        /// <param name="config">The configuration object specifying how to use KMS keys.</param>
        /// <returns>A reference to the <see cref="IDataProtectionBuilder" /> after this operation has completed.</returns>
        public static IDataProtectionBuilder ProtectKeysWithAwsKms(this IDataProtectionBuilder builder, IAmazonKeyManagementService kmsClient, IKmsXmlEncryptorConfig config)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (kmsClient == null)
            {
                throw new ArgumentNullException(nameof(kmsClient));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            return builder.ProtectKeysWithAwsKmsRaw(kmsClient, config);
        }

        /// <summary>
        /// Configures the data protection system to encrypt keys using AWS Key Management Service master keys
        /// </summary>
        /// <param name="builder">The <see cref="IDataProtectionBuilder"/>.</param>
        /// <param name="config">The configuration object specifying how to use KMS keys.</param>
        /// <returns>A reference to the <see cref="IDataProtectionBuilder" /> after this operation has completed.</returns>
        public static IDataProtectionBuilder ProtectKeysWithAwsKms(this IDataProtectionBuilder builder, IKmsXmlEncryptorConfig config)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            return builder.ProtectKeysWithAwsKmsRaw(null, config);
        }

        /// <summary>
        /// Configures the data protection system to encrypt keys using AWS Key Management Service master keys
        /// </summary>
        /// <param name="builder">The <see cref="IDataProtectionBuilder"/>.</param>
        /// <param name="kmsClient">KMS client configured with appropriate credentials.</param>
        /// <param name="config">Provide the configuration object specifying how to use KMS keys e.g. from configuration section.</param>
        /// <returns>A reference to the <see cref="IDataProtectionBuilder" /> after this operation has completed.</returns>
        public static IDataProtectionBuilder ProtectKeysWithAwsKms(this IDataProtectionBuilder builder, IAmazonKeyManagementService kmsClient, IConfiguration config)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (kmsClient == null)
            {
                throw new ArgumentNullException(nameof(kmsClient));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            return builder.ProtectKeysWithAwsKmsConfig(kmsClient, config);
        }

        /// <summary>
        /// Configures the data protection system to encrypt keys using AWS Key Management Service master keys
        /// </summary>
        /// <param name="builder">The <see cref="IDataProtectionBuilder"/>.</param>
        /// <param name="config">Provide the configuration object specifying how to use KMS keys e.g. from configuration section.</param>
        /// <returns>A reference to the <see cref="IDataProtectionBuilder" /> after this operation has completed.</returns>
        public static IDataProtectionBuilder ProtectKeysWithAwsKms(this IDataProtectionBuilder builder, IConfiguration config)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            return builder.ProtectKeysWithAwsKmsConfig(null, config);
        }

        private static IDataProtectionBuilder ProtectKeysWithAwsKmsRaw(this IDataProtectionBuilder builder, IAmazonKeyManagementService kmsClient, IKmsXmlEncryptorConfig config)
        {
            builder.Services.AddSingleton<IConfigureOptions<KmsXmlEncryptorConfig>>(new DirectConfigure(config));
            return builder.ProtectKeysWithAwsKmsImpl(kmsClient, sp => sp.GetRequiredService<IOptionsSnapshot<KmsXmlEncryptorConfig>>());
        }

        private static IDataProtectionBuilder ProtectKeysWithAwsKmsConfig(this IDataProtectionBuilder builder, IAmazonKeyManagementService kmsClient, IConfiguration config)
        {
            builder.Services.Configure<KmsXmlEncryptorConfig>(config);
            return builder.ProtectKeysWithAwsKmsImpl(kmsClient, sp => sp.GetRequiredService<IOptionsSnapshot<KmsXmlEncryptorConfig>>());
        }

        private static IDataProtectionBuilder ProtectKeysWithAwsKmsImpl(this IDataProtectionBuilder builder, IAmazonKeyManagementService kmsClient, Func<IServiceProvider, IOptionsSnapshot<KmsXmlEncryptorConfig>> getOptions)
        {
            builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(serviceProvider =>
                                                                                   {
                                                                                       var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
                                                                                       if (kmsClient == null)
                                                                                       {
                                                                                           kmsClient = serviceProvider.GetRequiredService<IAmazonKeyManagementService>();
                                                                                       }
                                                                                       var boundOptions = getOptions(serviceProvider);
                                                                                       var dpOptions = serviceProvider.GetRequiredService<IOptionsSnapshot<DataProtectionOptions>>();
                                                                                       return new ConfigureOptions<KeyManagementOptions>(options =>
                                                                                                                                         {
                                                                                                                                             options.XmlEncryptor = loggerFactory != null ? new KmsXmlEncryptor(kmsClient, boundOptions, dpOptions, loggerFactory) : new KmsXmlEncryptor(kmsClient, boundOptions, dpOptions);
                                                                                                                                         });
                                                                                   });

            // Need to ensure KmsXmlDecryptor can actually be constructed
            if (kmsClient != null)
            {
                builder.Services.TryAddSingleton(kmsClient);
            }
            return builder;
        }

        private class DirectConfigure : IConfigureOptions<KmsXmlEncryptorConfig>
        {
            private readonly IKmsXmlEncryptorConfig input;

            public DirectConfigure(IKmsXmlEncryptorConfig input)
            {
                this.input = input ?? throw new ArgumentNullException(nameof(input));
            }

            public void Configure(KmsXmlEncryptorConfig options)
            {
                options.CopyFrom(input);
            }
        }
    }
}
