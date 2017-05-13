// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using AspNetCore.DataProtection.Aws.Kms;
using Xunit;

namespace AspNetCore.DataProtection.Aws.Tests
{
    public class KmsXmlEncryptorConfigTests
    {
        [Fact]
        public void ExpectConstructorRoundTrip()
        {
            var config = new KmsXmlEncryptorConfig("appId", "keyId");

            Assert.Equal("keyId", config.KeyId);
            Assert.Equal("appId", config.EncryptionContext[KmsConstants.ApplicationEncryptionContextKey]);

            // strictly a List & Dictionary test, but validates the config doesn't really care about these entries as long as the user sets them up how they want
            config.GrantTokens.Add("token");
            Assert.Contains("token", config.GrantTokens);

            config.EncryptionContext.Add("myContext", "myValue");
            Assert.Contains("myContext", config.EncryptionContext.Keys);
            Assert.Contains("myValue", config.EncryptionContext.Values);
        }
    }
}
