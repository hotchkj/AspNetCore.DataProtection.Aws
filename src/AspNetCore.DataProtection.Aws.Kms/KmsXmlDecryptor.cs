// Copyright(c) 2017 Jeff Hotchkiss
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
    /// An ASP.NET key decryptor using AWS KMS
    /// </summary>
    public sealed class KmsXmlDecryptor : IXmlDecryptor
    {
        private readonly ILogger logger;
        private readonly IAmazonKeyManagementService kmsClient;

        /// <summary>
        /// Creates a <see cref="KmsXmlDecryptor"/> for decrypting ASP.NET keys with a KMS master key
        /// </summary>
        /// <remarks>
        /// DataProtection has a fairly awful way of making the IXmlDecryptor that by default never just does
        /// <see cref="IServiceProvider.GetService"/>, instead calling the IServiceProvider constructor directly.
        /// This means we have to do the resolution of needed objects via IServiceProvider.
        /// </remarks>
        /// <param name="services">A mandatory <see cref="IServiceProvider"/> to provide services</param>
        public KmsXmlDecryptor(IServiceProvider services)
        {
            kmsClient = services?.GetRequiredService<IAmazonKeyManagementService>() ?? throw new ArgumentNullException(nameof(services));
            Config = services.GetRequiredService<IKmsXmlEncryptorConfig>();
            logger = services.GetService<ILoggerFactory>()?.CreateLogger<KmsXmlDecryptor>();
        }

        /// <summary>
        /// The configuration of how KMS will decrypt the XML data
        /// </summary>
        public IKmsXmlEncryptorConfig Config { get; }

        /// <inheritdoc/>
        public XElement Decrypt(XElement encryptedElement)
        {
            // Due to time constraints, Microsoft didn't make the interfaces async
            // https://github.com/aspnet/DataProtection/issues/124
            // so loft the heavy lifting into a thread which enables safe async behaviour with some additional cost
            // Overhead should be acceptable since key management isn't a frequent thing
            return Task.Run(() => DecryptAsync(encryptedElement, CancellationToken.None)).Result;
        }

        /// <summary>
        /// Decrypts a provided XML element.
        /// </summary>
        /// <param name="encryptedElement">Encrypted XML element.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Decrypted XML element.</returns>
        public async Task<XElement> DecryptAsync(XElement encryptedElement, CancellationToken ct)
        {
            logger?.LogDebug("Decrypting ciphertext DataProtection key using AWS key {0}", Config.KeyId);

            using (var memoryStream = new MemoryStream())
            {
                byte[] protectedKey = Convert.FromBase64String((string)encryptedElement.Element("value"));
                await memoryStream.WriteAsync(protectedKey, 0, protectedKey.Length, ct);

                var response = await kmsClient.DecryptAsync(new DecryptRequest
                                                            {
                                                                EncryptionContext = Config.EncryptionContext,
                                                                GrantTokens = Config.GrantTokens,
                                                                CiphertextBlob = memoryStream
                                                            },
                                                            ct)
                                              .ConfigureAwait(false);

                // Help indicates that Plaintext might be empty if the key couldn't be retrieved but
                // testing shows that you always get an exception thrown first
                using (var plaintext = response.Plaintext)
                {
                    // Ignoring all the good reasons mentioned in KmsXmlEncryptor and that the implementation would
                    // be error-prone, hard to test & review, as well as vary between NET Full & NET Core, it's not
                    // actually permitted to access the buffer of response.Plaintext because it was populated in
                    // the SDK from a constructor which disallows any subsequent writing.
                    //
                    // Yet more reasons that this needs to be handled at a framework level, providing clear Secure* primitives.
                    return XElement.Load(plaintext);
                }
            }
        }
    }
}
