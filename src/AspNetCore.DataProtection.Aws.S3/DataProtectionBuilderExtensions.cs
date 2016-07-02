// Copyright(c) 2016 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Amazon.S3;

namespace AspNetCore.DataProtection.Aws.S3
{
    /// <summary>
    /// Extensions for configuring data protection using an <see cref="IDataProtectionBuilder"/>.
    /// </summary>
    public static class DataProtectionBuilderExtensions
    {
        /// <summary>
        /// Configures the data protection system to persist keys to a specified S3 bucket.
        /// </summary>
        /// <param name="builder">The <see cref="DataProtectionConfiguration"/>.</param>
        /// <param name="s3Client">S3 client configured with appropriate credentials.</param>
        /// <param name="config">The configuration object specifying how to write to S3.</param>
        /// <returns>A reference to the <see cref="DataProtectionConfiguration" /> after this operation has completed.</returns>
        public static IDataProtectionBuilder PersistKeysToAwsS3(this IDataProtectionBuilder builder, IAmazonS3 s3Client, S3XmlRepositoryConfig config)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (s3Client == null)
            {
                throw new ArgumentNullException(nameof(s3Client));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            Use(builder.Services, ServiceDescriptor.Singleton<IMockingWrapper, MockingWrapper>());
            Use(builder.Services, ServiceDescriptor.Singleton<IXmlRepository>(services => new S3XmlRepository(s3Client, config, services)));
            return builder;
        }

        /// <summary>
        /// Configures the data protection system to persist keys to a specified S3 bucket.
        /// </summary>
        /// <param name="builder">The <see cref="DataProtectionConfiguration"/>.</param>
        /// <param name="config">The configuration object specifying how to write to S3.</param>
        /// <returns>A reference to the <see cref="DataProtectionConfiguration" /> after this operation has completed.</returns>
        public static IDataProtectionBuilder PersistKeysToAwsS3(this IDataProtectionBuilder builder, S3XmlRepositoryConfig config)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            Use(builder.Services, ServiceDescriptor.Singleton<IMockingWrapper, MockingWrapper>());
            Use(builder.Services, ServiceDescriptor.Singleton<IXmlRepository>(services => new S3XmlRepository(services.GetRequiredService<IAmazonS3>(), config, services)));
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
