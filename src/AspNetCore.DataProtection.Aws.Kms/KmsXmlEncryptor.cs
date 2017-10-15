// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AspNetCore.DataProtection.Aws.Kms
{
    // ReSharper disable once InheritdocConsiderUsage
    /// <summary>
    /// An ASP.NET key encryptor using AWS KMS
    /// </summary>
    public sealed class KmsXmlEncryptor : IXmlEncryptor
    {
        private readonly ILogger logger;
        private readonly IAmazonKeyManagementService kmsClient;
        private readonly IOptions<KmsXmlEncryptorConfig> config;
        private readonly IOptions<DataProtectionOptions> dpOptions;

        // ReSharper disable once InheritdocConsiderUsage
        /// <summary>
        /// Creates a <see cref="KmsXmlEncryptor"/> for encrypting ASP.NET keys with a KMS master key
        /// </summary>
        /// <param name="kmsClient">The KMS client</param>
        /// <param name="config">The configuration object specifying which key data in KMS to use</param>
        /// <param name="dpOptions">Main data protection options</param>
        public KmsXmlEncryptor(IAmazonKeyManagementService kmsClient, IOptions<KmsXmlEncryptorConfig> config, IOptions<DataProtectionOptions> dpOptions)
            : this(kmsClient, config, dpOptions, null)
        {
        }

        /// <summary>
        /// Creates a <see cref="KmsXmlEncryptor"/> for encrypting ASP.NET keys with a KMS master key
        /// </summary>
        /// <param name="kmsClient">The KMS client</param>
        /// <param name="config">The configuration object specifying which key data in KMS to use</param>
        /// <param name="dpOptions">Main data protection options</param>
        /// <param name="logger">An optional <see cref="ILogger"/> to provide logging.</param>
        public KmsXmlEncryptor(IAmazonKeyManagementService kmsClient,
                               IOptions<KmsXmlEncryptorConfig> config,
                               IOptions<DataProtectionOptions> dpOptions,
                               ILogger<KmsXmlEncryptor> logger)
        {
            this.kmsClient = kmsClient ?? throw new ArgumentNullException(nameof(kmsClient));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.dpOptions = dpOptions ?? throw new ArgumentNullException(nameof(dpOptions));
            this.logger = logger;
        }

        /// <summary>
        /// Configuration of how KMS will encrypt the XML data
        /// </summary>
        public IKmsXmlEncryptorConfig Config => config.Value;

        /// <summary>
        /// Ensure configuration is valid for usage.
        /// </summary>
        public void ValidateConfig()
        {
            // Microsoft haven't provided for any validation of options as yet, so what was originally a constructor argument must now be validated by hand at runtime (yuck)
            if (string.IsNullOrWhiteSpace(Config.KeyId))
            {
                throw new ArgumentException($"A key id is required in {nameof(IKmsXmlEncryptorConfig)} for KMS operation");
            }
        }

        /// <inheritdoc/>
        public EncryptedXmlInfo Encrypt(XElement plaintextElement)
        {
            // Due to time constraints, Microsoft didn't make the interfaces async
            // https://github.com/aspnet/DataProtection/issues/124
            // so loft the heavy lifting into a thread which enables safe async behaviour with some additional cost
            // Overhead should be acceptable since key management isn't a frequent thing
            return Task.Run(() => EncryptAsync(plaintextElement, CancellationToken.None)).Result;
        }

        /// <summary>
        /// Encrypts the provided XML element.
        /// </summary>
        /// <param name="plaintextElement">XML element to encrypt.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Encrypted XML data.</returns>
#pragma warning disable S3242 // Not altering Microsoft interface definition
        public async Task<EncryptedXmlInfo> EncryptAsync(XElement plaintextElement, CancellationToken ct)
#pragma warning restore S3242
        {
            ValidateConfig();

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

                var response = await kmsClient.EncryptAsync(new EncryptRequest
                                                            {
                                                                EncryptionContext = ContextUpdater.GetEncryptionContext(Config, dpOptions.Value),
                                                                GrantTokens = Config.GrantTokens,
                                                                KeyId = Config.KeyId,
                                                                Plaintext = memoryStream
                                                            },
                                                            ct)
                                              .ConfigureAwait(false);

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
