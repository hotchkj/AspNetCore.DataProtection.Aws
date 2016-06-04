// Copyright(c) 2016 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Microsoft.AspNet.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AspNetCore.DataProtection.Aws.Kms
{
    public class KmsXmlDecryptor : IXmlDecryptor
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Creates a <see cref="KmsXmlDecryptor"/> for decrypting ASP.NET keys with a KMS master key
        /// </summary>
        /// <param name="kmsClient">The KMS client.</param>
        /// <param name="config">The configuration object specifying which key data in KMS to use.</param>
        public KmsXmlDecryptor(IAmazonKeyManagementService kmsClient, KmsXmlDecryptorConfig config)
            : this(kmsClient, config, services: null)
        {
        }

        /// <summary>
        /// Creates a <see cref="KmsXmlDecryptor"/> for decrypting ASP.NET keys with a KMS master key
        /// </summary>
        /// <param name="kmsClient">The KMS client.</param>
        /// <param name="config">The configuration object specifying which key data in KMS to use.</param>
        /// <param name="services">An optional <see cref="IServiceProvider"/> to provide ancillary services.</param>
        public KmsXmlDecryptor(IAmazonKeyManagementService kmsClient, KmsXmlDecryptorConfig config, IServiceProvider services)
        {
            if (kmsClient == null)
            {
                throw new ArgumentNullException(nameof(kmsClient));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            KmsClient = kmsClient;
            Config = config;
            Services = services;
            _logger = services?.GetService<ILoggerFactory>()?.CreateLogger<KmsXmlDecryptor>();
        }

        /// <summary>
        /// The configuration of how KMS will decrypt the XML data.
        /// </summary>
        public KmsXmlDecryptorConfig Config { get; }

        /// <summary>
        /// The <see cref="IServiceProvider"/> provided to the constructor.
        /// </summary>
        protected IServiceProvider Services { get; }

        /// <summary>
        /// The <see cref="IAmazonKeyManagementService"/> provided to the constructor.
        /// </summary>
        protected IAmazonKeyManagementService KmsClient { get; }

        public XElement Decrypt(XElement encryptedElement)
        {
            // Due to time constraints, Microsoft didn't make the interfaces async
            // https://github.com/aspnet/DataProtection/issues/124
            // so loft the heavy lifting into a thread which enables safe async behaviour with some additional cost
            // Overhead should be acceptable since key management isn't a frequent thing
            return Task.Run(() => DecryptAsync(encryptedElement, CancellationToken.None)).Result;
        }

        public async Task<XElement> DecryptAsync(XElement encryptedElement, CancellationToken ct)
        {
            using (var memoryStream = new MemoryStream())
            {
                var protectedKey = Convert.FromBase64String((string)encryptedElement.Element("value"));
                await memoryStream.WriteAsync(protectedKey, 0, protectedKey.Length);

                var response = await KmsClient.DecryptAsync(new DecryptRequest
                {
                    EncryptionContext = Config.EncryptionContext,
                    GrantTokens = Config.GrantTokens,
                    CiphertextBlob = memoryStream
                }, ct).ConfigureAwait(false);

                return XElement.Load(response.Plaintext);
            }
        }
    }
}
