// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using AspNetCore.DataProtection.Aws.S3;
using System;
using Xunit;

namespace AspNetCore.DataProtection.Aws.Tests
{
    public class S3XmlRepositoryConfigTests
    {
        private readonly S3XmlRepositoryConfig config;

        public S3XmlRepositoryConfigTests()
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
