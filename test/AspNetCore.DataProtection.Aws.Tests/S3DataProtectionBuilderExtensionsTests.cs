// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.S3;
using Xunit;
using AspNetCore.DataProtection.Aws.S3;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Logging;

namespace AspNetCore.DataProtection.Aws.Tests
{
    public class S3DataProtectionBuilderExtensionsTests
    {
        private readonly Mock<IDataProtectionBuilder> builder;
        private readonly Mock<IServiceCollection> svcCollection;
        private readonly Mock<IAmazonS3> client;
        private readonly Mock<IServiceProvider> provider;
        private readonly Mock<ILoggerFactory> loggerFactory;
        private readonly MockRepository repository;

        public S3DataProtectionBuilderExtensionsTests()
        {
            repository = new MockRepository(MockBehavior.Strict);
            builder = repository.Create<IDataProtectionBuilder>();
            client = repository.Create<IAmazonS3>();
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
            Assert.Equal(1, services.Count(x => x.ServiceType == typeof(IXmlRepository)));

            Assert.Equal(ServiceLifetime.Singleton, services.Single(x => x.ServiceType == typeof(IMockingWrapper)).Lifetime);
            Assert.Equal(ServiceLifetime.Singleton, services.Single(x => x.ServiceType == typeof(IXmlRepository)).Lifetime);

            provider.Setup(x => x.GetService(typeof(ILoggerFactory))).Returns(loggerFactory.Object);
            loggerFactory.Setup(x => x.CreateLogger(typeof(S3XmlRepository).FullName)).Returns(repository.Create<ILogger<S3XmlRepository>>().Object);

            Assert.IsType<S3XmlRepository>(services.Single(x => x.ServiceType == typeof(IXmlRepository)).ImplementationFactory(provider.Object));
        }

        [Fact]
        public void ExpectFailureOnNullBuilder()
        {
            Assert.Throws<ArgumentNullException>(() => S3.DataProtectionBuilderExtensions.PersistKeysToAwsS3(null, new S3XmlRepositoryConfig("bucketName")));
        }

        [Fact]
        public void ExpectFailureOnNullBuilderWithClient()
        {
            Assert.Throws<ArgumentNullException>(() => S3.DataProtectionBuilderExtensions.PersistKeysToAwsS3(null,
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
            Assert.Throws<ArgumentNullException>(() => builder.Object.PersistKeysToAwsS3(null));
        }

        [Fact]
        public void ExpectFailureOnNullConfigWithClient()
        {
            Assert.Throws<ArgumentNullException>(() => builder.Object.PersistKeysToAwsS3(repository.Create<IAmazonS3>().Object, null));
        }
    }
}
