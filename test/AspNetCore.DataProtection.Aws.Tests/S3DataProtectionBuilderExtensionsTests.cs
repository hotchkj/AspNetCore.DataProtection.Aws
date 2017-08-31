// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.S3;
using AspNetCore.DataProtection.Aws.S3;
using AspNetCore.DataProtection.Aws.S3.Internals;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using DataProtectionBuilderExtensions = AspNetCore.DataProtection.Aws.S3.DataProtectionBuilderExtensions;

namespace AspNetCore.DataProtection.Aws.Tests
{
    public class S3DataProtectionBuilderExtensionsTests
    {
        private readonly Mock<IDataProtectionBuilder> builder;
        private readonly Mock<IServiceCollection> svcCollection;
        private readonly Mock<IAmazonS3> client;
        private readonly Mock<IServiceProvider> provider;
        private readonly Mock<ILoggerFactory> loggerFactory;
        private readonly Mock<IOptions<S3XmlRepositoryConfig>> snapshot;
        private readonly MockRepository repository;

        public S3DataProtectionBuilderExtensionsTests()
        {
            repository = new MockRepository(MockBehavior.Strict);
            builder = repository.Create<IDataProtectionBuilder>();
            client = repository.Create<IAmazonS3>();
            svcCollection = repository.Create<IServiceCollection>();
            provider = repository.Create<IServiceProvider>();
            loggerFactory = repository.Create<ILoggerFactory>();
            snapshot = repository.Create<IOptions<S3XmlRepositoryConfig>>();
        }

        public void Dispose()
        {
            repository.VerifyAll();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ExpectBuilderAdditionsRaw(bool withClient)
        {
            var services = new List<ServiceDescriptor>();

            builder.Setup(x => x.Services).Returns(svcCollection.Object);
            svcCollection.Setup(x => x.GetEnumerator()).Returns(() => services.GetEnumerator());
            svcCollection.Setup(x => x.Add(It.IsAny<ServiceDescriptor>()))
                         .Callback<ServiceDescriptor>(sd => { services.Add(sd); });

            var config = new S3XmlRepositoryConfig("bucket");

            // Repeat call to ensure cumulative calls work
            if (withClient)
            {
                builder.Object.PersistKeysToAwsS3(client.Object, config);
                builder.Object.PersistKeysToAwsS3(client.Object, config);
            }
            else
            {
                builder.Object.PersistKeysToAwsS3(config);
                builder.Object.PersistKeysToAwsS3(config);
                provider.Setup(x => x.GetService(typeof(IAmazonS3))).Returns(client.Object);
            }

            Assert.Equal(1, services.Count(x => x.ServiceType == typeof(IMockingWrapper)));

            // IConfigureOptions is designed & expected to be present multiple times, so expect two after two calls
            Assert.Equal(2, services.Count(x => x.ServiceType == typeof(IConfigureOptions<KeyManagementOptions>)));
            Assert.Equal(2, services.Count(x => x.ServiceType == typeof(IConfigureOptions<S3XmlRepositoryConfig>)));

            Assert.Equal(ServiceLifetime.Singleton, services.Single(x => x.ServiceType == typeof(IMockingWrapper)).Lifetime);
            Assert.Equal(ServiceLifetime.Singleton, services.First(x => x.ServiceType == typeof(IConfigureOptions<KeyManagementOptions>)).Lifetime);
            Assert.Equal(ServiceLifetime.Singleton, services.First(x => x.ServiceType == typeof(IConfigureOptions<S3XmlRepositoryConfig>)).Lifetime);

            // Ensure we run equivalent config for the actual configuration object
            var configureObject = services.First(x => x.ServiceType == typeof(IConfigureOptions<S3XmlRepositoryConfig>)).ImplementationInstance;
            var optionsObject = new S3XmlRepositoryConfig();
            ((IConfigureOptions<S3XmlRepositoryConfig>)configureObject).Configure(optionsObject);

            provider.Setup(x => x.GetService(typeof(ILoggerFactory))).Returns(loggerFactory.Object);
            provider.Setup(x => x.GetService(typeof(IOptions<S3XmlRepositoryConfig>))).Returns(snapshot.Object);
            loggerFactory.Setup(x => x.CreateLogger(typeof(S3XmlRepository).FullName)).Returns(repository.Create<ILogger<S3XmlRepository>>().Object);
            snapshot.Setup(x => x.Value).Returns(optionsObject);

            var configure = services.First(x => x.ServiceType == typeof(IConfigureOptions<KeyManagementOptions>)).ImplementationFactory(provider.Object);
            var options = new KeyManagementOptions();
            ((IConfigureOptions<KeyManagementOptions>)configure).Configure(options);
            Assert.IsType<S3XmlRepository>(options.XmlRepository);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ExpectBuilderAdditionsConfig(bool withClient)
        {
            var services = new List<ServiceDescriptor>();

            builder.Setup(x => x.Services).Returns(svcCollection.Object);
            svcCollection.Setup(x => x.GetEnumerator()).Returns(() => services.GetEnumerator());
            svcCollection.Setup(x => x.Add(It.IsAny<ServiceDescriptor>()))
                         .Callback<ServiceDescriptor>(sd => { services.Add(sd); });

            // An empty collection seems to be enough to run what is eventually ConfigurationBinder.Bind, since there is no way to mock the options configure call
            // ReSharper disable once CollectionNeverUpdated.Local
            var configChildren = new List<IConfigurationSection>();
            Mock<IConfiguration> configMock = repository.Create<IConfiguration>();
            configMock.Setup(x => x.GetChildren()).Returns(configChildren);

            // Repeat call to ensure cumulative calls work
            if (withClient)
            {
                builder.Object.PersistKeysToAwsS3(client.Object, configMock.Object);
                builder.Object.PersistKeysToAwsS3(client.Object, configMock.Object);
            }
            else
            {
                builder.Object.PersistKeysToAwsS3(configMock.Object);
                builder.Object.PersistKeysToAwsS3(configMock.Object);
                provider.Setup(x => x.GetService(typeof(IAmazonS3))).Returns(client.Object);
            }

            Assert.Equal(1, services.Count(x => x.ServiceType == typeof(IMockingWrapper)));

            // IConfigureOptions is designed & expected to be present multiple times, so expect two after two calls
            Assert.Equal(2, services.Count(x => x.ServiceType == typeof(IConfigureOptions<KeyManagementOptions>)));
            Assert.Equal(2, services.Count(x => x.ServiceType == typeof(IConfigureOptions<S3XmlRepositoryConfig>)));

            Assert.Equal(ServiceLifetime.Singleton, services.Single(x => x.ServiceType == typeof(IMockingWrapper)).Lifetime);
            Assert.Equal(ServiceLifetime.Singleton, services.First(x => x.ServiceType == typeof(IConfigureOptions<KeyManagementOptions>)).Lifetime);
            Assert.Equal(ServiceLifetime.Singleton, services.First(x => x.ServiceType == typeof(IConfigureOptions<S3XmlRepositoryConfig>)).Lifetime);

            // Ensure we run equivalent config for the actual configuration object
            var configureObject = services.First(x => x.ServiceType == typeof(IConfigureOptions<S3XmlRepositoryConfig>)).ImplementationInstance;
            var optionsObject = new S3XmlRepositoryConfig();
            ((IConfigureOptions<S3XmlRepositoryConfig>)configureObject).Configure(optionsObject);

            provider.Setup(x => x.GetService(typeof(ILoggerFactory))).Returns(loggerFactory.Object);
            provider.Setup(x => x.GetService(typeof(IOptions<S3XmlRepositoryConfig>))).Returns(snapshot.Object);
            loggerFactory.Setup(x => x.CreateLogger(typeof(S3XmlRepository).FullName)).Returns(repository.Create<ILogger<S3XmlRepository>>().Object);
            snapshot.Setup(x => x.Value).Returns(optionsObject);

            var configure = services.First(x => x.ServiceType == typeof(IConfigureOptions<KeyManagementOptions>)).ImplementationFactory(provider.Object);
            var options = new KeyManagementOptions();
            ((IConfigureOptions<KeyManagementOptions>)configure).Configure(options);
            Assert.IsType<S3XmlRepository>(options.XmlRepository);
        }

        [Fact]
        public void ExpectFailureOnNullBuilder()
        {
            Assert.Throws<ArgumentNullException>(() => DataProtectionBuilderExtensions.PersistKeysToAwsS3(null, new S3XmlRepositoryConfig("bucketName")));
        }

        [Fact]
        public void ExpectFailureOnNullBuilderWithClient()
        {
            Assert.Throws<ArgumentNullException>(() => DataProtectionBuilderExtensions.PersistKeysToAwsS3(null,
                                                                                                          repository.Create<IAmazonS3>().Object,
                                                                                                          new S3XmlRepositoryConfig("bucket")));
        }

        [Fact]
        public void ExpectFailureOnNullClient()
        {
            Assert.Throws<ArgumentNullException>(() => builder.Object.PersistKeysToAwsS3(null, new S3XmlRepositoryConfig("bucket")));
        }

        [Fact]
        public void ExpectFailureOnNullConfig()
        {
            Assert.Throws<ArgumentNullException>(() => builder.Object.PersistKeysToAwsS3(null as IConfiguration));
        }

        [Fact]
        public void ExpectFailureOnNullConfigWithClient()
        {
            Assert.Throws<ArgumentNullException>(() => builder.Object.PersistKeysToAwsS3(repository.Create<IAmazonS3>().Object, null as IConfiguration));
        }

        [Fact]
        public void ExpectFailureOnNullConfigObject()
        {
            Assert.Throws<ArgumentNullException>(() => builder.Object.PersistKeysToAwsS3(null as IS3XmlRepositoryConfig));
        }

        [Fact]
        public void ExpectFailureOnNullConfigObjectWithClient()
        {
            Assert.Throws<ArgumentNullException>(() => builder.Object.PersistKeysToAwsS3(repository.Create<IAmazonS3>().Object, null as IS3XmlRepositoryConfig));
        }
    }
}
