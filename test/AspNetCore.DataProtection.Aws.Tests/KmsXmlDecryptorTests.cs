// Copyright(c) 2016 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using AspNetCore.DataProtection.Aws.Kms;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Xunit;

namespace AspNetCore.DataProtection.Aws.Tests
{
    public class KmsXmlDecryptorTests : IDisposable
    {
        private readonly KmsXmlDecryptor decryptor;
        private readonly MockRepository repository;
        private readonly Mock<IAmazonKeyManagementService> kmsClient;
        private readonly Mock<IKmsXmlEncryptorConfig> encryptConfig;
        private const string KeyId = "keyId";
        private const string ElementName = "name";
        private readonly Dictionary<string, string> EncryptionContext = new Dictionary<string, string>();
        private readonly List<string> GrantTokens = new List<string>();

        public KmsXmlDecryptorTests()
        {
            repository = new MockRepository(MockBehavior.Strict);
            kmsClient = repository.Create<IAmazonKeyManagementService>();
            encryptConfig = repository.Create<IKmsXmlEncryptorConfig>();
            var serviceProvider = repository.Create<IServiceProvider>();
            
            serviceProvider.Setup(x => x.GetService(typeof(IKmsXmlEncryptorConfig)))
                           .Returns(encryptConfig.Object);
            serviceProvider.Setup(x => x.GetService(typeof(IAmazonKeyManagementService)))
                           .Returns(kmsClient.Object);
            serviceProvider.Setup(x => x.GetService(typeof(ILoggerFactory)))
                           .Returns(null);

            decryptor = new KmsXmlDecryptor(serviceProvider.Object);
        }

        public void Dispose()
        {
            repository.VerifyAll();
        }
        
        [Fact]
        public void ExpectDecryptToSucceed()
        {
            var myEncryptedString = "encrypted";
            var myBase64EncryptedData = Convert.ToBase64String(Encoding.UTF8.GetBytes(myEncryptedString));
            var myEncryptedXml = new XElement(ElementName, new XElement("value", myBase64EncryptedData));
            var myOutputXml = new XElement(ElementName, "output");

            using (var decryptedResponseStream = new MemoryStream())
            {
                myOutputXml.Save(decryptedResponseStream);
                decryptedResponseStream.Seek(0, SeekOrigin.Begin);

                var decryptResponse = new DecryptResponse
                {
                    KeyId = KeyId,
                    Plaintext = decryptedResponseStream
                };

                encryptConfig.Setup(x => x.EncryptionContext).Returns(EncryptionContext);
                encryptConfig.Setup(x => x.GrantTokens).Returns(GrantTokens);

                kmsClient.Setup(x => x.DecryptAsync(It.IsAny<DecryptRequest>(), CancellationToken.None))
                         .ReturnsAsync(decryptResponse)
                         .Callback<DecryptRequest, CancellationToken>((dr, ct) =>
                         {
                             Assert.Same(EncryptionContext, dr.EncryptionContext);
                             Assert.Same(GrantTokens, dr.GrantTokens);

                             Assert.Equal(myEncryptedString, Encoding.UTF8.GetString(dr.CiphertextBlob.ToArray()));
                         });

                var plaintextXml = decryptor.Decrypt(myEncryptedXml);
                Assert.True(XNode.DeepEquals(myOutputXml, plaintextXml));
            }
        }
    }
}
