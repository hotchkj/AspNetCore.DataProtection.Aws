// Copyright(c) 2017 Jeff Hotchkiss
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AspNetCore.DataProtection.Aws.Tests
{
    public class KmsXmlDecryptorTests : IDisposable
    {
        private readonly KmsXmlDecryptor decryptor;
        private readonly MockRepository repository;
        private readonly Mock<IAmazonKeyManagementService> kmsClient;
        private readonly Mock<IOptions<KmsXmlEncryptorConfig>> encryptConfig;
        private readonly Mock<IOptions<DataProtectionOptions>> dpOptions;
        private const string KeyId = "keyId";
        private const string ElementName = "name";
        private readonly Dictionary<string, string> encryptionContext = new Dictionary<string, string>();
        private readonly List<string> grantTokens = new List<string>();

        public KmsXmlDecryptorTests()
        {
            repository = new MockRepository(MockBehavior.Strict);
            kmsClient = repository.Create<IAmazonKeyManagementService>();
            encryptConfig = repository.Create<IOptions<KmsXmlEncryptorConfig>>();
            dpOptions = repository.Create<IOptions<DataProtectionOptions>>();
            var serviceProvider = repository.Create<IServiceProvider>();

            serviceProvider.Setup(x => x.GetService(typeof(IOptions<KmsXmlEncryptorConfig>)))
                           .Returns(encryptConfig.Object);
            serviceProvider.Setup(x => x.GetService(typeof(IOptions<DataProtectionOptions>)))
                           .Returns(dpOptions.Object);
            serviceProvider.Setup(x => x.GetService(typeof(IAmazonKeyManagementService)))
                           .Returns(kmsClient.Object);
            serviceProvider.Setup(x => x.GetService(typeof(ILoggerFactory)))
                           .Returns(null as ILoggerFactory);

            decryptor = new KmsXmlDecryptor(serviceProvider.Object);
        }

        public void Dispose()
        {
            repository.VerifyAll();
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

                kmsClient.Setup(x => x.DecryptAsync(It.IsAny<DecryptRequest>(), CancellationToken.None))
                         .ReturnsAsync(decryptResponse)
                         .Callback<DecryptRequest, CancellationToken>((dr, ct) =>
                                                                      {
                                                                          if (appId != null && useAppId)
                                                                          {
                                                                              Assert.Contains(KmsConstants.ApplicationEncryptionContextKey, dr.EncryptionContext.Keys);
                                                                              Assert.Equal(expectedAppId, dr.EncryptionContext[KmsConstants.ApplicationEncryptionContextKey]);
                                                                          }
                                                                          else
                                                                          {
                                                                              Assert.Same(encryptionContext, dr.EncryptionContext);
                                                                          }
                                                                          Assert.Same(grantTokens, dr.GrantTokens);

                                                                          Assert.Equal(myEncryptedString, Encoding.UTF8.GetString(dr.CiphertextBlob.ToArray()));
                                                                      });

                var plaintextXml = decryptor.Decrypt(myEncryptedXml);
                Assert.True(XNode.DeepEquals(myOutputXml, plaintextXml));
            }
        }
    }
}
