// Copyright(c) 2016 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System.Collections.Generic;

namespace AspNetCore.DataProtection.Aws.Kms
{
    public abstract class KmsBaseConfig
    {
        // TODO Can we obtain DataProtectionOptions.ApplicationDiscriminator for this?
        protected KmsBaseConfig(string applicationName)
        {
            EncryptionContext = new Dictionary<string, string>();
            GrantTokens = new List<string>();

            EncryptionContext.Add(KmsConstants.DefaultEncryptionContextKey, KmsConstants.DefaultEncryptionContextValue);
            EncryptionContext.Add(KmsConstants.ApplicationEncryptionContextKey, applicationName);
        }

        public Dictionary<string, string> EncryptionContext { get; }
        public List<string> GrantTokens { get; }
    }
}
