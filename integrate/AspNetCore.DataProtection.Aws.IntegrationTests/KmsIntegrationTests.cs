// Copyright(c) 2018 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Amazon;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using AspNetCore.DataProtection.Aws.Kms;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AspNetCore.DataProtection.Aws.IntegrationTests
{
    public class KmsIntegrationTests : IDisposable
    {
        private readonly KmsXmlEncryptor encryptor;
        private readonly KmsXmlDecryptor decryptor;
        private readonly IAmazonKeyManagementService kmsClient;
        private readonly IServiceProvider svcProvider;
        internal const string ApplicationName = "hotchkj-test-app";
        private const string ElementName = "name";
        private const string ElementContent = "test";
        // Expectation that whatever key is in use has this alias
        internal const string KmsTestingKey = "alias/KmsIntegrationTesting";
        private readonly DataProtectionOptions dpOptions;

        public KmsIntegrationTests()
        {
            // Expectation that local SDK has been configured correctly, whether via VS Tools or user config files
            kmsClient = new AmazonKeyManagementServiceClient(RegionEndpoint.EUWest1);
            var encryptConfig = new KmsXmlEncryptorConfig(KmsTestingKey);
            dpOptions = new DataProtectionOptions { ApplicationDiscriminator = ApplicationName };
            var encryptSnapshot = new DirectOptions<KmsXmlEncryptorConfig>(encryptConfig);
            var dpSnapshot = new DirectOptions<DataProtectionOptions>(dpOptions);

            var svcCollection = new ServiceCollection();
            svcCollection.AddSingleton<IOptions<KmsXmlEncryptorConfig>>(sp => encryptSnapshot);
            svcCollection.AddSingleton<IOptions<DataProtectionOptions>>(sp => dpSnapshot);
            svcCollection.AddSingleton(sp => kmsClient);
            svcProvider = svcCollection.BuildServiceProvider();

            encryptor = new KmsXmlEncryptor(kmsClient, encryptSnapshot, dpSnapshot);

            decryptor = new KmsXmlDecryptor(svcProvider);
        }

        public void Dispose()
        {
            kmsClient.Dispose();
            (svcProvider as IDisposable)?.Dispose();
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

            dpOptions.ApplicationDiscriminator = "wrong";
            await Assert.ThrowsAsync<InvalidCiphertextException>(async () => await decryptor.DecryptAsync(encrypted.EncryptedElement, CancellationToken.None));
        }
    }
}
