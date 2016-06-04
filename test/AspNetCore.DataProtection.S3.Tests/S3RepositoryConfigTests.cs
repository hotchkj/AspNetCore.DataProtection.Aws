using System;
using Xunit;

namespace AspNetCore.DataProtection.S3.IntegrationTests
{
    public class S3RepositoryConfigTests
    {
        private readonly S3XmlRepositoryConfig config;

        public S3RepositoryConfigTests()
        {
            config = new S3XmlRepositoryConfig("somebucket");
        }

        [Theory]
        [InlineData("~")]
        [InlineData("`")]
        [InlineData("|")]
        [InlineData("#")]
        [InlineData("<")]
        [InlineData(">")]
        [InlineData("[")]
        [InlineData("]")]
        [InlineData("{")]
        [InlineData("}")]
        [InlineData("%")]
        [InlineData("^")]
        [InlineData("\\")]
        [InlineData("\n")]
        [InlineData("/")]
        [InlineData("&")]
        [InlineData("$")]
        [InlineData("@")]
        [InlineData("=")]
        [InlineData(";")]
        [InlineData(":")]
        [InlineData("+")]
        [InlineData(" ")]
        [InlineData(",")]
        [InlineData("?")]
        public void ExpectUnsafePrefixesToThrow(string prefix)
        {
            Assert.Throws<ArgumentException>(() => config.KeyPrefix = prefix);
        }

        [Theory]
        [InlineData("1")]
        [InlineData("A")]
        [InlineData("1/")]
        [InlineData("A/")]
        public void ExpectSafePrefixesToSucceed(string prefix)
        {
            config.KeyPrefix = prefix;
            Assert.Equal(prefix, config.KeyPrefix);
        }
    }
}
