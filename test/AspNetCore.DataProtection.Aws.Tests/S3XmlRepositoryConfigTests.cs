// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System;
using Amazon.S3;
using AspNetCore.DataProtection.Aws.S3;
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

        [Fact]
        public void ExpectSuccessfulCopy()
        {
            config.Bucket = "somebucket";
            config.KeyPrefix = "keypref";
            config.MaxS3QueryConcurrency = 4;
            config.StorageClass = S3StorageClass.Glacier;
            config.ServerSideEncryptionMethod = ServerSideEncryptionMethod.AWSKMS;
            config.ServerSideEncryptionCustomerMethod = ServerSideEncryptionCustomerMethod.AES256;
            config.ServerSideEncryptionCustomerProvidedKey = "key";
            config.ServerSideEncryptionCustomerProvidedKeyMd5 = "md5";
            config.ServerSideEncryptionKeyManagementServiceKeyId = "keyid";
            config.ClientSideCompression = !config.ClientSideCompression;

            var copy = new S3XmlRepositoryConfig();
            copy.CopyFrom(config);

            Assert.Equal(config.Bucket, copy.Bucket);
            Assert.Equal(config.KeyPrefix, copy.KeyPrefix);
            Assert.Equal(config.MaxS3QueryConcurrency, copy.MaxS3QueryConcurrency);
            Assert.Equal(config.StorageClass, copy.StorageClass);
            Assert.Equal(config.ServerSideEncryptionMethod, copy.ServerSideEncryptionMethod);
            Assert.Equal(config.ServerSideEncryptionCustomerMethod, copy.ServerSideEncryptionCustomerMethod);
            Assert.Equal(config.ServerSideEncryptionCustomerProvidedKey, copy.ServerSideEncryptionCustomerProvidedKey);
            Assert.Equal(config.ServerSideEncryptionCustomerProvidedKeyMd5, copy.ServerSideEncryptionCustomerProvidedKeyMd5);
            Assert.Equal(config.ServerSideEncryptionKeyManagementServiceKeyId, copy.ServerSideEncryptionKeyManagementServiceKeyId);
            Assert.Equal(config.ClientSideCompression, copy.ClientSideCompression);
        }
    }
}
