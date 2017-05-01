// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System.Collections.Generic;

namespace AspNetCore.DataProtection.Aws.Kms
{
    /// <summary>
    /// Enables configuration of KMS encryption options applied to the ASP.NET key material
    /// </summary>
    public interface IKmsXmlEncryptorConfig
    {
        /// <summary>
        /// A set of encryption contexts passed to KMS during encryption &amp; decryption
        /// </summary>
        Dictionary<string, string> EncryptionContext { get; }

        /// <summary>
        /// Any KMS grant tokens needed for the encryption
        /// </summary>
        List<string> GrantTokens { get; }

        /// <summary>
        /// The KMS key in whichever form is most suitable for the client's usage
        /// </summary>
        string KeyId { get; }
    }

    /// <inheritdoc/>
    public class KmsXmlEncryptorConfig : IKmsXmlEncryptorConfig
    {
        /// <summary>
        /// Constructs encryptor configuration.
        /// </summary>
        /// <param name="applicationName">Name of the application, used in KMS context.</param>
        /// <param name="keyId">KMS key ID or alias.</param>
        public KmsXmlEncryptorConfig(string applicationName, string keyId)
        {
            EncryptionContext = new Dictionary<string, string>();
            GrantTokens = new List<string>();
            KeyId = keyId; // TODO Can we obtain DataProtectionOptions.ApplicationDiscriminator for this?

            EncryptionContext.Add(KmsConstants.DefaultEncryptionContextKey, KmsConstants.DefaultEncryptionContextValue);
            EncryptionContext.Add(KmsConstants.ApplicationEncryptionContextKey, applicationName);
        }

        /// <inheritdoc/>
        public Dictionary<string, string> EncryptionContext { get; }

        /// <inheritdoc/>
        public List<string> GrantTokens { get; }

        /// <inheritdoc/>
        public string KeyId { get; }
    }
}
