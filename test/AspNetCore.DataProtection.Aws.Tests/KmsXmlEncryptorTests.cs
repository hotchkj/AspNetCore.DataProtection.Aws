// Copyright(c) 2016 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using AspNetCore.DataProtection.Aws.Kms;
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
    public class KmsXmlEncryptorTests : IDisposable
    {
        private readonly KmsXmlEncryptor encryptor;
        private readonly MockRepository repository;
        private readonly Mock<IAmazonKeyManagementService> kmsClient;
        private readonly Mock<IKmsXmlEncryptorConfig> encryptConfig;
        private const string KeyId = "keyId";
        private const string ElementName = "name";
        private readonly Dictionary<string, string> EncryptionContext = new Dictionary<string, string>();
        private readonly List<string> GrantTokens = new List<string>();

        public KmsXmlEncryptorTests()
        {
            repository = new MockRepository(MockBehavior.Strict);
            kmsClient = repository.Create<IAmazonKeyManagementService>();
            encryptConfig = repository.Create<IKmsXmlEncryptorConfig>();

            encryptor = new KmsXmlEncryptor(kmsClient.Object, encryptConfig.Object);
        }

        public void Dispose()
        {
            repository.VerifyAll();
        }

        [Fact]
        public void ExpectEncryptToSucceed()
        {
            var myInputXml = new XElement(ElementName, "input");
            var myEncryptedData = Encoding.UTF8.GetBytes("encrypted");
            var myBase64EncryptedData = Convert.ToBase64String(myEncryptedData);

            using (var encryptedResponseStream = new MemoryStream())
            {
                encryptedResponseStream.Write(myEncryptedData, 0, myEncryptedData.Length);
                encryptedResponseStream.Seek(0, SeekOrigin.Begin);

                var encryptResponse = new EncryptResponse
                {
                    KeyId = KeyId,
                    CiphertextBlob = encryptedResponseStream
                };

                encryptConfig.Setup(x => x.EncryptionContext).Returns(EncryptionContext);
                encryptConfig.Setup(x => x.GrantTokens).Returns(GrantTokens);
                encryptConfig.Setup(x => x.KeyId).Returns(KeyId);

                kmsClient.Setup(x => x.EncryptAsync(It.IsAny<EncryptRequest>(), CancellationToken.None))
                         .ReturnsAsync(encryptResponse)
                         .Callback<EncryptRequest, CancellationToken>((er, ct) =>
                         {
                             Assert.Same(EncryptionContext, er.EncryptionContext);
                             Assert.Same(GrantTokens, er.GrantTokens);

                             var body = XElement.Load(er.Plaintext);
                             Assert.True(XNode.DeepEquals(myInputXml, body));
                         });

                var encryptedXml = encryptor.Encrypt(myInputXml);

                Assert.Equal(typeof(KmsXmlDecryptor), encryptedXml.DecryptorType);
                var encryptedBlob = (string)encryptedXml.EncryptedElement.Element("value");
                Assert.Equal(myBase64EncryptedData, encryptedBlob);
            }
        }

        [Fact]
        public void EnsureContextsAreUnaltered()
        {
            Assert.Equal("AspNetCore.DataProtection.Aws.Kms.Xml", KmsConstants.DefaultEncryptionContextKey);
            Assert.Equal("b7b7f5af-d3c3-436d-8792-87dfd65e1cd4", KmsConstants.DefaultEncryptionContextValue);
            Assert.Equal("AspNetCore.DataProtection.Aws.Kms.Xml.ApplicationName", KmsConstants.ApplicationEncryptionContextKey);
        }
    }
}
