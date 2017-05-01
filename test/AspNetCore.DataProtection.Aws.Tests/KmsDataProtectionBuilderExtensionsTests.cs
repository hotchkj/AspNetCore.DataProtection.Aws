// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.KeyManagementService;
using AspNetCore.DataProtection.Aws.Kms;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        private readonly MockRepository repository;

        public KmsDataProtectionBuilderExtensionsTests()
        {
            repository = new MockRepository(MockBehavior.Strict);
            builder = repository.Create<IDataProtectionBuilder>();
            client = repository.Create<IAmazonKeyManagementService>();
            svcCollection = repository.Create<IServiceCollection>();
            provider = repository.Create<IServiceProvider>();
            loggerFactory = repository.Create<ILoggerFactory>();
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
            svcCollection.Setup(x => x.Count).Returns(() => services.Count);
            svcCollection.Setup(x => x.Add(It.IsAny<ServiceDescriptor>())).Callback<ServiceDescriptor>(sd => services.Add(sd));
            svcCollection.Setup(x => x[It.IsAny<int>()]).Returns<int>(index => services[index]);
            svcCollection.Setup(x => x.RemoveAt(It.IsAny<int>())).Callback<int>(index => services.RemoveAt(index));

            var config = new KmsXmlEncryptorConfig("appName", "keyId");

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

            Assert.Equal(withClient ? 6 : 5, services.Count);
            Assert.Equal(1, services.Count(x => x.ServiceType == typeof(IXmlEncryptor)));
            Assert.Equal(1, services.Count(x => x.ServiceType == typeof(IXmlDecryptor)));
            Assert.Equal(withClient ? 1 : 0, services.Count(x => x.ServiceType == typeof(IAmazonKeyManagementService)));
            Assert.Equal(1, services.Count(x => x.ServiceType == typeof(KmsXmlEncryptorConfig)));
            Assert.Equal(1, services.Count(x => x.ServiceType == typeof(IKmsXmlEncryptorConfig)));
            Assert.Equal(1, services.Count(x => x.ServiceType == typeof(KmsXmlDecryptor)));

            Assert.Equal(ServiceLifetime.Singleton, services.Single(x => x.ServiceType == typeof(IXmlEncryptor)).Lifetime);
            Assert.Equal(ServiceLifetime.Singleton, services.Single(x => x.ServiceType == typeof(IXmlDecryptor)).Lifetime);
            Assert.Equal(ServiceLifetime.Singleton, services.Single(x => x.ServiceType == typeof(KmsXmlEncryptorConfig)).Lifetime);
            Assert.Equal(ServiceLifetime.Singleton, services.Single(x => x.ServiceType == typeof(IKmsXmlEncryptorConfig)).Lifetime);
            Assert.Equal(ServiceLifetime.Singleton, services.Single(x => x.ServiceType == typeof(KmsXmlDecryptor)).Lifetime);

            provider.Setup(x => x.GetService(typeof(IAmazonKeyManagementService))).Returns(client.Object);
            if (withClient)
            {
                Assert.Equal(ServiceLifetime.Singleton, services.Single(x => x.ServiceType == typeof(IAmazonKeyManagementService)).Lifetime);
                Assert.Same(client.Object, services.Single(x => x.ServiceType == typeof(IAmazonKeyManagementService)).ImplementationInstance);
            }
            Assert.Same(config, services.Single(x => x.ServiceType == typeof(KmsXmlEncryptorConfig)).ImplementationInstance);
            Assert.Same(config, services.Single(x => x.ServiceType == typeof(IKmsXmlEncryptorConfig)).ImplementationInstance);

            provider.Setup(x => x.GetService(typeof(ILoggerFactory))).Returns(loggerFactory.Object);
            loggerFactory.Setup(x => x.CreateLogger(typeof(KmsXmlEncryptor).FullName)).Returns(repository.Create<ILogger<KmsXmlEncryptor>>().Object);
            loggerFactory.Setup(x => x.CreateLogger(typeof(KmsXmlDecryptor).FullName)).Returns(repository.Create<ILogger<KmsXmlDecryptor>>().Object);
            provider.Setup(x => x.GetService(typeof(IKmsXmlEncryptorConfig))).Returns(config);

            var decryptor = services.Single(x => x.ServiceType == typeof(KmsXmlDecryptor)).ImplementationFactory(provider.Object);
            provider.Setup(x => x.GetService(typeof(KmsXmlDecryptor))).Returns(decryptor);

            Assert.IsType<KmsXmlDecryptor>(decryptor);
            Assert.IsType<KmsXmlEncryptor>(services.Single(x => x.ServiceType == typeof(IXmlEncryptor)).ImplementationFactory(provider.Object));
            Assert.Same(decryptor, services.Single(x => x.ServiceType == typeof(IXmlDecryptor)).ImplementationFactory(provider.Object));
        }

        [Fact]
        public void ExpectFailureOnNullBuilder()
        {
            Assert.Throws<ArgumentNullException>(() => DataProtectionBuilderExtensions.ProtectKeysWithAwsKms(null, new KmsXmlEncryptorConfig("appId", "keyId")));
        }

        [Fact]
        public void ExpectFailureOnNullBuilderWithClient()
        {
            Assert.Throws<ArgumentNullException>(() => DataProtectionBuilderExtensions.ProtectKeysWithAwsKms(null,
                                                                                                             repository.Create<IAmazonKeyManagementService>().Object,
                                                                                                             new KmsXmlEncryptorConfig("appId", "keyId")));
        }

        [Fact]
        public void ExpectFailureOnNullClient()
        {
            Assert.Throws<ArgumentNullException>(() => builder.Object.ProtectKeysWithAwsKms(null, new KmsXmlEncryptorConfig("appId", "keyId")));
        }

        [Fact]
        public void ExpectFailureOnNullConfig()
        {
            Assert.Throws<ArgumentNullException>(() => builder.Object.ProtectKeysWithAwsKms(null));
        }

        [Fact]
        public void ExpectFailureOnNullConfigWithClient()
        {
            Assert.Throws<ArgumentNullException>(() => builder.Object.ProtectKeysWithAwsKms(repository.Create<IAmazonKeyManagementService>().Object, null));
        }
    }
}
