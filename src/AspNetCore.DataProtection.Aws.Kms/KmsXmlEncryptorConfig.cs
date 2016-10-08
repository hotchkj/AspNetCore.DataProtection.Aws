// Copyright(c) 2016 Jeff Hotchkiss
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

    public class KmsXmlEncryptorConfig : IKmsXmlEncryptorConfig
    {
        // TODO Can we obtain DataProtectionOptions.ApplicationDiscriminator for this?
        public KmsXmlEncryptorConfig(string applicationName, string keyId)
        {
            EncryptionContext = new Dictionary<string, string>();
            GrantTokens = new List<string>();
            KeyId = keyId;

            EncryptionContext.Add(KmsConstants.DefaultEncryptionContextKey, KmsConstants.DefaultEncryptionContextValue);
            EncryptionContext.Add(KmsConstants.ApplicationEncryptionContextKey, applicationName);
        }

        public Dictionary<string, string> EncryptionContext { get; }
        public List<string> GrantTokens { get; }
        public string KeyId { get; }
    }
}
