// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System;
using System.ComponentModel;
using Amazon.S3;
using AspNetCore.DataProtection.Aws.S3.Internals;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        /// <param name="builder">The <see cref="IDataProtectionBuilder"/>.</param>
        /// <param name="s3Client">S3 client configured with appropriate credentials.</param>
        /// <param name="config">The configuration object specifying how to write to S3</param>
        /// <returns>A reference to the <see cref="IDataProtectionBuilder" /> after this operation has completed.</returns>
        public static IDataProtectionBuilder PersistKeysToAwsS3(this IDataProtectionBuilder builder, IAmazonS3 s3Client, IS3XmlRepositoryConfig config)
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

            return builder.PersistKeysToAwsS3Raw(s3Client, config);
        }

        /// <summary>
        /// Configures the data protection system to persist keys to a specified S3 bucket.
        /// </summary>
        /// <param name="builder">The <see cref="IDataProtectionBuilder"/>.</param>
        /// <param name="config">The configuration object specifying how to write to S3</param>
        /// <returns>A reference to the <see cref="IDataProtectionBuilder" /> after this operation has completed.</returns>
        public static IDataProtectionBuilder PersistKeysToAwsS3(this IDataProtectionBuilder builder, IS3XmlRepositoryConfig config)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            
            return builder.PersistKeysToAwsS3Raw(null, config);
        }

        /// <summary>
        /// Configures the data protection system to persist keys to a specified S3 bucket.
        /// </summary>
        /// <param name="builder">The <see cref="IDataProtectionBuilder"/>.</param>
        /// <param name="s3Client">S3 client configured with appropriate credentials.</param>
        /// <param name="config">Provide the configuration object specifying how to write to S3 e.g. from configuration section.</param>
        /// <returns>A reference to the <see cref="IDataProtectionBuilder" /> after this operation has completed.</returns>
        public static IDataProtectionBuilder PersistKeysToAwsS3(this IDataProtectionBuilder builder, IAmazonS3 s3Client, IConfiguration config)
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

            return builder.PersistKeysToAwsS3Config(s3Client, config);
        }

        /// <summary>
        /// Configures the data protection system to persist keys to a specified S3 bucket.
        /// </summary>
        /// <param name="builder">The <see cref="IDataProtectionBuilder"/>.</param>
        /// <param name="config">Provide the configuration object specifying how to write to S3 e.g. from configuration section.</param>
        /// <returns>A reference to the <see cref="IDataProtectionBuilder" /> after this operation has completed.</returns>
        public static IDataProtectionBuilder PersistKeysToAwsS3(this IDataProtectionBuilder builder, IConfiguration config)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            return builder.PersistKeysToAwsS3Config(null, config);
        }

        private static IDataProtectionBuilder PersistKeysToAwsS3Raw(this IDataProtectionBuilder builder, IAmazonS3 s3Client, IS3XmlRepositoryConfig config)
        {
            builder.Services.AddSingleton<IConfigureOptions<S3XmlRepositoryConfig>>(new DirectConfigure(config));
            return builder.PersistKeysToAwsS3Impl(s3Client, sp => sp.GetRequiredService<IOptions<S3XmlRepositoryConfig>>());
        }

        private static IDataProtectionBuilder PersistKeysToAwsS3Config(this IDataProtectionBuilder builder, IAmazonS3 s3Client, IConfiguration config)
        {
            builder.Services.Configure<S3XmlRepositoryConfig>(config);
            return builder.PersistKeysToAwsS3Impl(s3Client, sp => sp.GetRequiredService<IOptions<S3XmlRepositoryConfig>>());
        }

        private static IDataProtectionBuilder PersistKeysToAwsS3Impl(this IDataProtectionBuilder builder, IAmazonS3 s3Client, Func<IServiceProvider, IOptions<S3XmlRepositoryConfig>> getOptions)
        {
            builder.Services.AddOptions();
            builder.Services.TryAddSingleton<IMockingWrapper, MockingWrapper>();
            builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(serviceProvider =>
                                                                                   {
                                                                                       // Hacky workaround for configuration binding being non-configurable (the irony)
                                                                                       // Needs to occur before binding actually happens - which should be fine here, since the first occasion _should_ be requesting the options snapshot
                                                                                       TypeDescriptor.AddAttributes(typeof(S3StorageClass), new TypeConverterAttribute(typeof(AmazonConstantClassConverter<S3StorageClass>)));
                                                                                       TypeDescriptor.AddAttributes(typeof(ServerSideEncryptionCustomerMethod), new TypeConverterAttribute(typeof(AmazonConstantClassConverter<ServerSideEncryptionCustomerMethod>)));
                                                                                       TypeDescriptor.AddAttributes(typeof(ServerSideEncryptionMethod), new TypeConverterAttribute(typeof(AmazonConstantClassConverter<ServerSideEncryptionMethod>)));

                                                                                       var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
                                                                                       if (s3Client == null)
                                                                                       {
                                                                                           s3Client = serviceProvider.GetRequiredService<IAmazonS3>();
                                                                                       }
                                                                                       var boundOptions = getOptions(serviceProvider);
                                                                                       return new ConfigureOptions<KeyManagementOptions>(options =>
                                                                                                                                         {
                                                                                                                                             options.XmlRepository = loggerFactory != null ? new S3XmlRepository(s3Client, boundOptions, loggerFactory) : new S3XmlRepository(s3Client, boundOptions);
                                                                                                                                         });
                                                                                   });
            return builder;
        }

        private class DirectConfigure : IConfigureOptions<S3XmlRepositoryConfig>
        {
            private readonly IS3XmlRepositoryConfig input;

            public DirectConfigure(IS3XmlRepositoryConfig input)
            {
                this.input = input ?? throw new ArgumentNullException(nameof(input));
            }

            public void Configure(S3XmlRepositoryConfig options)
            {
                options.CopyFrom(input);
            }
        }
    }
}
