// Copyright(c) 2016 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.

namespace AspNetCore.DataProtection.Aws.Kms
{
    public class KmsXmlDecryptorConfig : KmsBaseConfig
    {
        public KmsXmlDecryptorConfig(string applicationName)
            : base(applicationName)
        {
        }
    }
}
