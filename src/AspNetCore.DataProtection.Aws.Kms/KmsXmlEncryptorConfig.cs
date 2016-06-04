// Copyright(c) 2016 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
namespace AspNetCore.DataProtection.Aws.Kms
{
    public class KmsXmlEncryptorConfig : KmsBaseConfig
    {
        public KmsXmlEncryptorConfig(string applicationName, string keyId)
            : base(applicationName)
        {
            KeyId = keyId;
        }

        public string KeyId { get; }
    }
}
