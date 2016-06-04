// Copyright(c) 2016 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System.Collections.Generic;

namespace AspNetCore.DataProtection.Aws.Kms
{
    public class KmsXmlEncryptorConfig
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
