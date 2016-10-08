// Copyright(c) 2016 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AspNetCore.DataProtection.Aws.Kms
{
    /// <summary>
    /// An ASP.NET key encryptor using AWS KMS
    /// </summary>
    public class KmsXmlEncryptor : IXmlEncryptor
    {
        private readonly ILogger logger;

        /// <summary>
        /// Creates a <see cref="KmsXmlEncryptor"/> for encrypting ASP.NET keys with a KMS master key
        /// </summary>
        /// <param name="kmsClient">The KMS client</param>
        /// <param name="config">The configuration object specifying which key data in KMS to use</param>
        public KmsXmlEncryptor(IAmazonKeyManagementService kmsClient, IKmsXmlEncryptorConfig config)
            : this(kmsClient, config, services: null)
        {
        }

        /// <summary>
        /// Creates a <see cref="KmsXmlEncryptor"/> for encrypting ASP.NET keys with a KMS master key
        /// </summary>
        /// <param name="kmsClient">The KMS client</param>
        /// <param name="config">The configuration object specifying which key data in KMS to use</param>
        /// <param name="services">An optional <see cref="IServiceProvider"/> to provide ancillary services</param>
        public KmsXmlEncryptor(IAmazonKeyManagementService kmsClient, IKmsXmlEncryptorConfig config, IServiceProvider services)
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
            logger = services?.GetService<ILoggerFactory>()?.CreateLogger<KmsXmlEncryptor>();
        }

        /// <summary>
        /// The configuration of how KMS will encrypt the XML data
        /// </summary>
        public IKmsXmlEncryptorConfig Config { get; }

        /// <summary>
        /// The <see cref="IServiceProvider"/> provided to the constructor
        /// </summary>
        protected IServiceProvider Services { get; }

        /// <summary>
        /// The <see cref="IAmazonKeyManagementService"/> provided to the constructor
        /// </summary>
        protected IAmazonKeyManagementService KmsClient { get; }

        public EncryptedXmlInfo Encrypt(XElement plaintextElement)
        {
            // Due to time constraints, Microsoft didn't make the interfaces async
            // https://github.com/aspnet/DataProtection/issues/124
            // so loft the heavy lifting into a thread which enables safe async behaviour with some additional cost
            // Overhead should be acceptable since key management isn't a frequent thing
            return Task.Run(() => EncryptAsync(plaintextElement, CancellationToken.None)).Result;
        }

        public async Task<EncryptedXmlInfo> EncryptAsync(XElement plaintextElement, CancellationToken ct)
        {
            logger?.LogDebug("Encrypting plaintext DataProtection key using AWS key {0}", Config.KeyId);

            // Some implementations of this e.g. DpapiXmlEncryptor go to some lengths to create a memory
            // stream, use unsafe code to pin & zero it, and so on.
            //
            // Currently not doing any such zeroing here, as this neglects that the XElement above is in memory,
            // is a managed construct containing ultimately a System.String, and therefore the plaintext is
            // already at risk of compromise, copying during GC, paging to disk etc. If we'd been starting with SecureString,
            // there'd be a good pre-existing case for handling the subsequent memory copies carefully (and it'd
            // essentially be forced as you can't copy or stream a SecureString without unsafe code).
            //
            // Even ignoring that, the subsequent code sending a MemoryStream out over the web to AWS calls ToArray inside
            // the SDK and then stores the result as a System.String, twice, as part of outgoing JSON, and that's
            // before considering HTTP-layer buffering...
            //
            // Since the AWS code eventually just gets UTF8 byte[] for request content, the ideal would be that
            // instead of a memory stream and a standard JSON handler, the AWS code prepares all the usual JSON and then
            // gets a specific UTF8 byte[] entry of the base64 plaintext which a caller can pin and erase (and the SDK could
            // do the same with its own request content byte[]).
            //
            // It doesn't.
            //
            // Even then the HttpClient usage would buffer the plaintext enroute.
            //
            // In conclusion pinning & zeroing this particular stream seems to be complex & error-prone overkill for
            // handling data that is already exposed in memory - not that I wouldn't be thrilled to see
            // a properly reviewed SecureMemoryStream and SecureHttpStreamContent in the framework...
            //
            // To at least reduce stream allocation churn & thus copying, pre-allocate a reasonable capacity.
            using (var memoryStream = new MemoryStream(4096))
            {
                plaintextElement.Save(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);

                var response = await KmsClient.EncryptAsync(new EncryptRequest
                {
                    EncryptionContext = Config.EncryptionContext,
                    GrantTokens = Config.GrantTokens,
                    KeyId = Config.KeyId,
                    Plaintext = memoryStream
                }, ct).ConfigureAwait(false);
                
                using (var cipherText = response.CiphertextBlob)
                {
                    var element = new XElement("encryptedKey",
                        new XComment(" This key is encrypted with AWS Key Management Service. "),
                        new XElement("value", Convert.ToBase64String(cipherText.ToArray())));

                    return new EncryptedXmlInfo(element, typeof(KmsXmlDecryptor));
                }
            }
        }
    }
}
