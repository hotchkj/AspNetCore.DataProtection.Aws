// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.KeyManagementService;
using AspNetCore.DataProtection.Aws.Kms;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using DataProtectionBuilderExtensions = AspNetCore.DataProtection.Aws.Kms.DataProtectionBuilderExtensions;

namespace AspNetCore.DataProtection.Aws.Tests
{
    public class KmsDataProtectionBuilderExtensionsTests
    {
        private readonly Mock<IDataProtectionBuilder> builder;
        private readonly Mock<IServiceCollection> svcCollection;
        private readonly Mock<IAmazonKeyManagementService> client;
        private readonly Mock<IServiceProvider> provider;
        private readonly Mock<ILoggerFactory> loggerFactory;
        private readonly Mock<IOptions<KmsXmlEncryptorConfig>> snapshot;
        private readonly Mock<IOptions<DataProtectionOptions>> dpSnapshot;
        private readonly MockRepository repository;

        public KmsDataProtectionBuilderExtensionsTests()
        {
            repository = new MockRepository(MockBehavior.Strict);
            builder = repository.Create<IDataProtectionBuilder>();
            client = repository.Create<IAmazonKeyManagementService>();
            svcCollection = repository.Create<IServiceCollection>();
            provider = repository.Create<IServiceProvider>();
            loggerFactory = repository.Create<ILoggerFactory>();
            snapshot = repository.Create<IOptions<KmsXmlEncryptorConfig>>();
            dpSnapshot = repository.Create<IOptions<DataProtectionOptions>>();
        }

        public void Dispose()
        {
            repository.VerifyAll();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ExpectBuilderAdditions(bool withClient)
        {
            var services = new List<ServiceDescriptor>();

            builder.Setup(x => x.Services).Returns(svcCollection.Object);
            svcCollection.Setup(x => x.GetEnumerator()).Returns(() => services.GetEnumerator());
            svcCollection.Setup(x => x.Add(It.IsAny<ServiceDescriptor>()))
                         .Callback<ServiceDescriptor>(sd => { services.Add(sd); });

            var config = new KmsXmlEncryptorConfig("keyId");

            // Repeat call to ensure cumulative calls work
            if (withClient)
            {
                builder.Object.ProtectKeysWithAwsKms(client.Object, config);
                builder.Object.ProtectKeysWithAwsKms(client.Object, config);
            }
            else
            {
                builder.Object.ProtectKeysWithAwsKms(config);
                builder.Object.ProtectKeysWithAwsKms(config);
            }

            Assert.Equal(withClient ? 1 : 0, services.Count(x => x.ServiceType == typeof(IAmazonKeyManagementService)));

            // IConfigureOptions is designed & expected to be present multiple times, so expect two after two calls
            Assert.Equal(2, services.Count(x => x.ServiceType == typeof(IConfigureOptions<KeyManagementOptions>)));
            Assert.Equal(2, services.Count(x => x.ServiceType == typeof(IConfigureOptions<KmsXmlEncryptorConfig>)));

            Assert.Equal(ServiceLifetime.Singleton, services.First(x => x.ServiceType == typeof(IConfigureOptions<KeyManagementOptions>)).Lifetime);
            Assert.Equal(ServiceLifetime.Singleton, services.First(x => x.ServiceType == typeof(IConfigureOptions<KmsXmlEncryptorConfig>)).Lifetime);

            provider.Setup(x => x.GetService(typeof(IAmazonKeyManagementService))).Returns(client.Object);
            if (withClient)
            {
                Assert.Equal(ServiceLifetime.Singleton, services.Single(x => x.ServiceType == typeof(IAmazonKeyManagementService)).Lifetime);
                Assert.Same(client.Object, services.Single(x => x.ServiceType == typeof(IAmazonKeyManagementService)).ImplementationInstance);
            }
            // Ensure we run equivalent config for the actual configuration object
            var configureObject = services.First(x => x.ServiceType == typeof(IConfigureOptions<KmsXmlEncryptorConfig>)).ImplementationInstance;
            var optionsObject = new KmsXmlEncryptorConfig();
            ((IConfigureOptions<KmsXmlEncryptorConfig>)configureObject).Configure(optionsObject);

            provider.Setup(x => x.GetService(typeof(ILoggerFactory))).Returns(loggerFactory.Object);
            provider.Setup(x => x.GetService(typeof(IOptions<KmsXmlEncryptorConfig>))).Returns(snapshot.Object);
            provider.Setup(x => x.GetService(typeof(IOptions<DataProtectionOptions>))).Returns(dpSnapshot.Object);
            loggerFactory.Setup(x => x.CreateLogger(typeof(KmsXmlEncryptor).FullName)).Returns(repository.Create<ILogger<KmsXmlEncryptor>>().Object);
            snapshot.Setup(x => x.Value).Returns(optionsObject);
            var dbOptions = new DataProtectionOptions();
            dpSnapshot.Setup(x => x.Value).Returns(dbOptions);

            var configure = services.First(x => x.ServiceType == typeof(IConfigureOptions<KeyManagementOptions>)).ImplementationFactory(provider.Object);
            var options = new KeyManagementOptions();
            ((IConfigureOptions<KeyManagementOptions>)configure).Configure(options);
            Assert.IsType<KmsXmlEncryptor>(options.XmlEncryptor);
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
                builder.Object.ProtectKeysWithAwsKms(client.Object, configMock.Object);
                builder.Object.ProtectKeysWithAwsKms(client.Object, configMock.Object);
            }
            else
            {
                builder.Object.ProtectKeysWithAwsKms(configMock.Object);
                builder.Object.ProtectKeysWithAwsKms(configMock.Object);
            }

            Assert.Equal(withClient ? 1 : 0, services.Count(x => x.ServiceType == typeof(IAmazonKeyManagementService)));

            // IConfigureOptions is designed & expected to be present multiple times, so expect two after two calls
            Assert.Equal(2, services.Count(x => x.ServiceType == typeof(IConfigureOptions<KeyManagementOptions>)));
            Assert.Equal(2, services.Count(x => x.ServiceType == typeof(IConfigureOptions<KmsXmlEncryptorConfig>)));

            Assert.Equal(ServiceLifetime.Singleton, services.First(x => x.ServiceType == typeof(IConfigureOptions<KeyManagementOptions>)).Lifetime);
            Assert.Equal(ServiceLifetime.Singleton, services.First(x => x.ServiceType == typeof(IConfigureOptions<KmsXmlEncryptorConfig>)).Lifetime);

            provider.Setup(x => x.GetService(typeof(IAmazonKeyManagementService))).Returns(client.Object);
            if (withClient)
            {
                Assert.Equal(ServiceLifetime.Singleton, services.Single(x => x.ServiceType == typeof(IAmazonKeyManagementService)).Lifetime);
                Assert.Same(client.Object, services.Single(x => x.ServiceType == typeof(IAmazonKeyManagementService)).ImplementationInstance);
            }
            // Ensure we run equivalent config for the actual configuration object
            var configureObject = services.First(x => x.ServiceType == typeof(IConfigureOptions<KmsXmlEncryptorConfig>)).ImplementationInstance;
            var optionsObject = new KmsXmlEncryptorConfig();
            ((IConfigureOptions<KmsXmlEncryptorConfig>)configureObject).Configure(optionsObject);

            provider.Setup(x => x.GetService(typeof(ILoggerFactory))).Returns(loggerFactory.Object);
            provider.Setup(x => x.GetService(typeof(IOptions<KmsXmlEncryptorConfig>))).Returns(snapshot.Object);
            provider.Setup(x => x.GetService(typeof(IOptions<DataProtectionOptions>))).Returns(dpSnapshot.Object);
            loggerFactory.Setup(x => x.CreateLogger(typeof(KmsXmlEncryptor).FullName)).Returns(repository.Create<ILogger<KmsXmlEncryptor>>().Object);
            snapshot.Setup(x => x.Value).Returns(optionsObject);
            var dbOptions = new DataProtectionOptions();
            dpSnapshot.Setup(x => x.Value).Returns(dbOptions);

            var configure = services.First(x => x.ServiceType == typeof(IConfigureOptions<KeyManagementOptions>)).ImplementationFactory(provider.Object);
            var options = new KeyManagementOptions();
            ((IConfigureOptions<KeyManagementOptions>)configure).Configure(options);
            Assert.IsType<KmsXmlEncryptor>(options.XmlEncryptor);
        }

        [Fact]
        public void ExpectFailureOnNullBuilder()
        {
            Assert.Throws<ArgumentNullException>(() => DataProtectionBuilderExtensions.ProtectKeysWithAwsKms(null, new KmsXmlEncryptorConfig("keyId")));
        }

        [Fact]
        public void ExpectFailureOnNullBuilderWithClient()
        {
            Assert.Throws<ArgumentNullException>(() => DataProtectionBuilderExtensions.ProtectKeysWithAwsKms(null,
                                                                                                             repository.Create<IAmazonKeyManagementService>().Object,
                                                                                                             new KmsXmlEncryptorConfig("keyId")));
        }

        [Fact]
        public void ExpectFailureOnNullClient()
        {
            Assert.Throws<ArgumentNullException>(() => builder.Object.ProtectKeysWithAwsKms(null, new KmsXmlEncryptorConfig("keyId")));
        }

        [Fact]
        public void ExpectFailureOnNullConfig()
        {
            Assert.Throws<ArgumentNullException>(() => builder.Object.ProtectKeysWithAwsKms(null as IConfiguration));
        }

        [Fact]
        public void ExpectFailureOnNullConfigWithClient()
        {
            Assert.Throws<ArgumentNullException>(() => builder.Object.ProtectKeysWithAwsKms(repository.Create<IAmazonKeyManagementService>().Object, null as IConfiguration));
        }

        [Fact]
        public void ExpectFailureOnNullConfigObject()
        {
            Assert.Throws<ArgumentNullException>(() => builder.Object.ProtectKeysWithAwsKms(null as IKmsXmlEncryptorConfig));
        }

        [Fact]
        public void ExpectFailureOnNullConfigObjectWithClient()
        {
            Assert.Throws<ArgumentNullException>(() => builder.Object.ProtectKeysWithAwsKms(repository.Create<IAmazonKeyManagementService>().Object, null as IKmsXmlEncryptorConfig));
        }
    }
}
