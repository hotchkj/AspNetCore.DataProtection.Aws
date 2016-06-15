// Copyright(c) 2016 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using AspNetCore.DataProtection.Aws.S3;
using Microsoft.AspNet.DataProtection.AuthenticatedEncryption;
using Microsoft.AspNet.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.AspNet.DataProtection.KeyManagement;
using Microsoft.AspNet.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AspNetCore.DataProtection.Aws.IntegrationTests
{
    public sealed class S3ManagerIntegrationTests : IDisposable
    {
        private readonly IAmazonS3 s3client;

        public S3ManagerIntegrationTests()
        {
            // Expectation that local SDK has been configured correctly, whether via VS Tools or user config files
            s3client = new AmazonS3Client(RegionEndpoint.EUWest1);
        }

        public void Dispose()
        {
            s3client.Dispose();
        }
        
        [Fact]
        public async Task ExpectFullKeyManagerStoreRetrieveToSucceed()
        {
            var config = new S3XmlRepositoryConfig(S3IntegrationTests.BucketName);
            config.KeyPrefix = "RealXmlKeyManager/";
            await ClearKeys(config.KeyPrefix);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddInstance(s3client);
            serviceCollection.AddInstance<IAuthenticatedEncryptorConfiguration>(new AuthenticatedEncryptorConfiguration(new AuthenticatedEncryptionOptions()));
            serviceCollection.AddDataProtection();
            serviceCollection.ConfigureDataProtection(configure =>
            {
                configure.PersistKeysToAwsS3(config);
            });
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var keyManager = new XmlKeyManager(serviceProvider.GetRequiredService<IXmlRepository>(),
                                               serviceProvider.GetRequiredService<IAuthenticatedEncryptorConfiguration>(),
                                               serviceProvider);

            var activationDate = new DateTimeOffset(new DateTime(1980, 1, 1));
            var expirationDate = new DateTimeOffset(new DateTime(1980, 6, 1));
            keyManager.CreateNewKey(activationDate, expirationDate);

            var keys = keyManager.GetAllKeys();

            Assert.Equal(1, keys.Count);
            Assert.Equal(activationDate, keys.Single().ActivationDate);
            Assert.Equal(expirationDate, keys.Single().ExpirationDate);
        }

        private async Task ClearKeys(string prefix)
        {
            // XmlKeyManager uses a GUID for the naming so we cannot overwrite the same entry in the test
            // Thus we must first clear out any keys that old tests put in

            var listed = await s3client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = S3IntegrationTests.BucketName,
                Prefix = prefix
            });

            // In sequence as we do not expect more than one or two of these assuming the tests work properly
            foreach (var s3Obj in listed.S3Objects)
            {
                await s3client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = S3IntegrationTests.BucketName,
                    Key = s3Obj.Key
                });
            }
        }
    }
}
