// Copyright(c) 2016 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Amazon.KeyManagementService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNet.DataProtection;
using Microsoft.AspNet.DataProtection.XmlEncryption;
using System;

namespace AspNetCore.DataProtection.Aws.Kms
{
    /// <summary>
    /// Extensions for configuring data protection using an <see cref="IDataProtectionBuilder"/>.
    /// </summary>
    /// <remarks>
    /// Taken almost verbatim from https://github.com/aspnet/DataProtection/blob/release/src/Microsoft.AspNetCore.DataProtection/DataProtectionBuilderExtensions.cs
    /// </remarks>
    public static class DataProtectionBuilderExtensions
    {
        /// <summary>
        /// Configures the data protection system to encrypt keys using AWS Key Management Service master keys
        /// </summary>
        /// <param name="builder">The <see cref="DataProtectionConfiguration"/>.</param>
        /// <param name="kmsClient">KMS client configured with appropriate credentials.</param>
        /// <param name="config">The configuration object specifying how use KMS keys.</param>
        /// <returns>A reference to the <see cref="DataProtectionConfiguration" /> after this operation has completed.</returns>
        public static DataProtectionConfiguration ProtectKeysWithAwsKms(this DataProtectionConfiguration builder, IAmazonKeyManagementService kmsClient, KmsXmlEncryptorConfig config)
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

            Use(builder.Services, ServiceDescriptor.Singleton<IXmlEncryptor>(services => new KmsXmlEncryptor(kmsClient, config, services)));
            return builder;
        }

        /// <summary>
        /// Configures the data protection system to encrypt keys using AWS Key Management Service master keys
        /// </summary>
        /// <param name="builder">The <see cref="DataProtectionConfiguration"/>.</param>
        /// <param name="config">The configuration object specifying how use KMS keys.</param>
        /// <returns>A reference to the <see cref="DataProtectionConfiguration" /> after this operation has completed.</returns>
        public static DataProtectionConfiguration ProtectKeysWithAwsKms(this DataProtectionConfiguration builder, KmsXmlEncryptorConfig config)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            Use(builder.Services, ServiceDescriptor.Singleton<IXmlEncryptor>(services => new KmsXmlEncryptor(services.GetRequiredService<IAmazonKeyManagementService>(), config, services)));
            return builder;
        }

        private static void RemoveAllServicesOfType(IServiceCollection services, Type serviceType)
        {
            // We go backward since we're modifying the collection in-place.
            for (int i = services.Count - 1; i >= 0; i--)
            {
                if (services[i]?.ServiceType == serviceType)
                {
                    services.RemoveAt(i);
                }
            }
        }

        private static void Use(IServiceCollection services, ServiceDescriptor descriptor)
        {
            RemoveAllServicesOfType(services, descriptor.ServiceType);
            services.Add(descriptor);
        }
    }
}
