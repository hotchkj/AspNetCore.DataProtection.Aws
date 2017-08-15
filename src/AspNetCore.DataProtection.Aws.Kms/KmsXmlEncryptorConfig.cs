// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System.Collections.Generic;
using System.Linq;

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

        /// <summary>
        /// Whether or not to include the <see cref="Microsoft.AspNetCore.DataProtection.DataProtectionOptions.ApplicationDiscriminator"/> as an encryption context.
        /// </summary>
        bool DiscriminatorAsContext { get; }

        /// <summary>
        /// Certain <see cref="Microsoft.AspNetCore.DataProtection.DataProtectionOptions.ApplicationDiscriminator"/> values contain sensitive information. Hash the context value if <c>true</c>.
        /// </summary>
        /// <remarks>
        /// Cookie tokens are another example of this hashing.
        /// </remarks>
        bool HashDiscriminatorContext { get; }
    }

    /// <inheritdoc/>
    public class KmsXmlEncryptorConfig : IKmsXmlEncryptorConfig
    {
        /// <summary>
        /// Constructs encryptor configuration. Bucket name <b>must</b> be configured e.g. via options binding.
        /// </summary>
        /// <remarks>
        /// See <see cref="CopyFrom"/> as to why this is needed.
        /// </remarks>
        public KmsXmlEncryptorConfig()
        {
            EncryptionContext = new Dictionary<string, string> { { KmsConstants.DefaultEncryptionContextKey, KmsConstants.DefaultEncryptionContextValue } };
            GrantTokens = new List<string>();
            DiscriminatorAsContext = true;
            HashDiscriminatorContext = true;
        }

        // ReSharper disable once InheritdocConsiderUsage
        /// <summary>
        /// Constructs encryptor configuration.
        /// </summary>
        /// <param name="keyId">KMS key ID or alias.</param>
        public KmsXmlEncryptorConfig(string keyId)
            : this()
        {
            KeyId = keyId;
        }

        /// <summary>
        /// Copies settings from another settings object.
        /// </summary>
        /// <remarks>
        /// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/> requires a parameterless constructor, so we end up with nasty hackery like this for handling programmatic options specification.
        /// </remarks>
        /// <param name="input">Input from which to copy configuration.</param>
        public void CopyFrom(IKmsXmlEncryptorConfig input)
        {
            KeyId = input.KeyId;
            EncryptionContext = input.EncryptionContext.ToDictionary(x => x.Key, x => x.Value);
            GrantTokens = input.GrantTokens.ToList();
            DiscriminatorAsContext = input.DiscriminatorAsContext;
            HashDiscriminatorContext = input.HashDiscriminatorContext;
        }

        /// <inheritdoc/>
        public Dictionary<string, string> EncryptionContext { get; set; }

        /// <inheritdoc/>
        public List<string> GrantTokens { get; set; }

        /// <inheritdoc/>
        public string KeyId { get; set; }

        /// <inheritdoc/>
        public bool DiscriminatorAsContext { get; set; }

        /// <inheritdoc/>
        public bool HashDiscriminatorContext { get; set; }
    }
}
