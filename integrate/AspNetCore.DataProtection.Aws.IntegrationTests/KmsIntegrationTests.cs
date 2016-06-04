// Copyright(c) 2016 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Amazon;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using AspNetCore.DataProtection.Aws.Kms;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace AspNetCore.DataProtection.Aws.IntegrationTests
{
    public class KmsIntegrationTests
    {
        private readonly KmsXmlEncryptor encryptor;
        private readonly KmsXmlDecryptor decryptor;
        private readonly IAmazonKeyManagementService kmsClient;
        private readonly KmsXmlEncryptorConfig encryptConfig;
        private readonly KmsXmlDecryptorConfig decryptConfig;
        private const string ApplicationName = "hotchkj-test-app";
        private const string ElementName = "name";
        private const string ElementContent = "test";

        public KmsIntegrationTests()
        {
            // Expectation that local SDK has been configured correctly, whether via VS Tools or user config files
            kmsClient = new AmazonKeyManagementServiceClient(RegionEndpoint.EUWest1);
            // Expectation that whatever key is in use has this alias
            encryptConfig = new KmsXmlEncryptorConfig(ApplicationName, "alias/KmsIntegrationTesting");
            decryptConfig = new KmsXmlDecryptorConfig(ApplicationName);

            encryptor = new KmsXmlEncryptor(kmsClient, encryptConfig);
            decryptor = new KmsXmlDecryptor(kmsClient, decryptConfig);
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

            try
            {
                decryptor.Config.EncryptionContext[KmsConstants.ApplicationEncryptionContextKey] = "wrong";
                await Assert.ThrowsAsync<InvalidCiphertextException>(async () => await decryptor.DecryptAsync(encrypted.EncryptedElement, CancellationToken.None));
            }
            finally
            {
                decryptor.Config.EncryptionContext[KmsConstants.ApplicationEncryptionContextKey] = ApplicationName;
            }
        }
    }
}
