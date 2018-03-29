// Copyright(c) 2018 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using AspNetCore.DataProtection.Aws.Kms;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AspNetCore.DataProtection.Aws.Tests
{
    public class KmsXmlEncryptorTests : IDisposable
    {
        private readonly KmsXmlEncryptor encryptor;
        private readonly MockRepository repository;
        private readonly Mock<IAmazonKeyManagementService> kmsClient;
        private readonly Mock<IOptions<KmsXmlEncryptorConfig>> encryptConfig;
        private readonly Mock<IOptions<DataProtectionOptions>> dpOptions;
        private const string KeyId = "keyId";
        private const string ElementName = "name";
        private readonly Dictionary<string, string> encryptionContext = new Dictionary<string, string>();
        private readonly List<string> grantTokens = new List<string>();

        public KmsXmlEncryptorTests()
        {
            repository = new MockRepository(MockBehavior.Strict);
            kmsClient = repository.Create<IAmazonKeyManagementService>();
            encryptConfig = repository.Create<IOptions<KmsXmlEncryptorConfig>>();
            dpOptions = repository.Create<IOptions<DataProtectionOptions>>();

            encryptor = new KmsXmlEncryptor(kmsClient.Object, encryptConfig.Object, dpOptions.Object);
        }

        public void Dispose()
        {
            repository.VerifyAll();
        }

        [Fact]
        public void ExpectValidationOfConfigToThrow()
        {
            var configObject = new KmsXmlEncryptorConfig();
            encryptConfig.Setup(x => x.Value).Returns(configObject);

            var altRepo = new KmsXmlEncryptor(kmsClient.Object, encryptConfig.Object, dpOptions.Object);

            Assert.Throws<ArgumentException>(() => altRepo.ValidateConfig());
        }

        [Theory]
        [InlineData(true, false, null, null)]
        [InlineData(true, false, "appId", "appId")]
        [InlineData(true, true, "appId", "MQbS8HXC/tbfUWH41GswKXp1I8W5LxnEQM/w+rgusJY=")]
        [InlineData(true, true, "bob", "gbY32PzSxtpjWeaWMROhFw3nleS3JbhNHgtM/Z7FjOk=")]
        [InlineData(false, false, "appId", null)]
        [InlineData(false, true, "appId", null)]
        public void ExpectEncryptToSucceed(bool useAppId, bool hashAppId, string appId, string expectedAppId)
        {
            var myInputXml = new XElement(ElementName, "input");
            byte[] myEncryptedData = Encoding.UTF8.GetBytes("encrypted");
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

                var actualConfig = new KmsXmlEncryptorConfig
                {
                    EncryptionContext = encryptionContext,
                    GrantTokens = grantTokens,
                    KeyId = KeyId,
                    DiscriminatorAsContext = useAppId,
                    HashDiscriminatorContext = hashAppId
                };

                var actualOptions = new DataProtectionOptions
                {
                    ApplicationDiscriminator = appId
                };

                encryptConfig.Setup(x => x.Value).Returns(actualConfig);
                dpOptions.Setup(x => x.Value).Returns(actualOptions);

                kmsClient.Setup(x => x.EncryptAsync(It.IsAny<EncryptRequest>(), CancellationToken.None))
                         .ReturnsAsync(encryptResponse)
                         .Callback<EncryptRequest, CancellationToken>((er, ct) =>
                                                                      {
                                                                          if (appId != null && useAppId)
                                                                          {
                                                                              Assert.Contains(KmsConstants.ApplicationEncryptionContextKey, er.EncryptionContext.Keys);
                                                                              Assert.Equal(expectedAppId, er.EncryptionContext[KmsConstants.ApplicationEncryptionContextKey]);
                                                                          }
                                                                          else
                                                                          {
                                                                              Assert.Same(encryptionContext, er.EncryptionContext);
                                                                          }
                                                                          Assert.Same(grantTokens, er.GrantTokens);

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
