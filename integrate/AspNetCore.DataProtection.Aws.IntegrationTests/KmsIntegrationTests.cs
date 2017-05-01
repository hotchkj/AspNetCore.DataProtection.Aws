// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Amazon;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using AspNetCore.DataProtection.Aws.Kms;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace AspNetCore.DataProtection.Aws.IntegrationTests
{
    public class KmsIntegrationTests : IDisposable
    {
        private readonly KmsXmlEncryptor encryptor;
        private readonly KmsXmlDecryptor decryptor;
        private readonly IAmazonKeyManagementService kmsClient;
        internal const string ApplicationName = "hotchkj-test-app";
        private const string ElementName = "name";
        private const string ElementContent = "test";
        // Expectation that whatever key is in use has this alias
        internal const string KmsTestingKey = "alias/KmsIntegrationTesting";

        public KmsIntegrationTests()
        {
            // Expectation that local SDK has been configured correctly, whether via VS Tools or user config files
            kmsClient = new AmazonKeyManagementServiceClient(RegionEndpoint.EUWest1);
            var encryptConfig = new KmsXmlEncryptorConfig(ApplicationName, KmsTestingKey);

            var svcCollection = new ServiceCollection();
            svcCollection.AddSingleton<IKmsXmlEncryptorConfig>(sp => encryptConfig);
            svcCollection.AddSingleton(sp => kmsClient);
            var svcProvider = svcCollection.BuildServiceProvider();

            encryptor = new KmsXmlEncryptor(kmsClient, encryptConfig, svcProvider);

            decryptor = new KmsXmlDecryptor(svcProvider);
        }

        public void Dispose()
        {
            kmsClient.Dispose();
        }

        [Fact]
        public async Task ExpectRoundTripToSucceed()
        {
            var myXml = new XElement(ElementName, ElementContent);

            var encrypted = await encryptor.EncryptAsync(myXml, CancellationToken.None);

            var decrypted = await decryptor.DecryptAsync(encrypted.EncryptedElement, CancellationToken.None);

            Assert.True(XNode.DeepEquals(myXml, decrypted));
        }

        [Fact]
        public async Task ExpectDifferentContextsToFail()
        {
            var myXml = new XElement(ElementName, ElementContent);

            var encrypted = await encryptor.EncryptAsync(myXml, CancellationToken.None);

            decryptor.Config.EncryptionContext[KmsConstants.ApplicationEncryptionContextKey] = "wrong";
            await Assert.ThrowsAsync<InvalidCiphertextException>(async () => await decryptor.DecryptAsync(encrypted.EncryptedElement, CancellationToken.None));
        }
    }
}
