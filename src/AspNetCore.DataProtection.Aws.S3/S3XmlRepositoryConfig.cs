// Copyright(c) 2016 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Amazon.S3;
using System;
using System.Linq;

namespace AspNetCore.DataProtection.Aws.S3
{
    public interface IS3XmlRepositoryConfig
    {
        string Bucket { get; }
        int MaxS3QueryConcurrency { get; }
        S3StorageClass StorageClass { get; }
        string KeyPrefix { get; }
        ServerSideEncryptionMethod ServerSideEncryptionMethod { get; }
        ServerSideEncryptionCustomerMethod ServerSideEncryptionCustomerMethod { get; }
        string ServerSideEncryptionCustomerProvidedKey { get; }
        string ServerSideEncryptionCustomerProvidedKeyMD5 { get; }
        string ServerSideEncryptionKeyManagementServiceKeyId { get; }
        bool ClientSideCompression { get; }
    }

    public class S3XmlRepositoryConfig : IS3XmlRepositoryConfig
    {
        public S3XmlRepositoryConfig(string bucketName)
        {
            Bucket = bucketName;
            SetToDefaults();
        }

        public void SetToDefaults()
        {
            KeyPrefix = "DataProtection-Keys/";
            MaxS3QueryConcurrency = 10;
            StorageClass = S3StorageClass.Standard;
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256;
            ServerSideEncryptionCustomerMethod = ServerSideEncryptionCustomerMethod.None;
            ServerSideEncryptionCustomerProvidedKey = null;
            ServerSideEncryptionCustomerProvidedKeyMD5 = null;
            ServerSideEncryptionKeyManagementServiceKeyId = null;
            ClientSideCompression = true;
        }

        public string Bucket { get; set; }
        public int MaxS3QueryConcurrency { get; set; }
        public S3StorageClass StorageClass { get; set; }
        public string KeyPrefix
        {
            get
            {
                return _keyPrefix;
            }
            set
            {
                if (!IsSafeS3Key(value))
                {
                    throw new ArgumentException($"Specified key prefix {value} is not considered a safe S3 name", "value");
                }
                _keyPrefix = value;
            }
        }
        public ServerSideEncryptionMethod ServerSideEncryptionMethod { get; set; }
        public ServerSideEncryptionCustomerMethod ServerSideEncryptionCustomerMethod { get; set; }
        public string ServerSideEncryptionCustomerProvidedKey { get; set; }
        public string ServerSideEncryptionCustomerProvidedKeyMD5 { get; set; }
        public string ServerSideEncryptionKeyManagementServiceKeyId { get; set; }
        public bool ClientSideCompression { get; set; }

        private string _keyPrefix;

        internal static bool IsSafeS3Key(string key)
        {
            // From S3 docs:
            // The following character sets are generally safe for use in key names:
            // Alphanumeric characters[0 - 9a - zA - Z]
            // Special characters !, -, _, ., *, ', (, and )
            // Singular entry of the folder delimiter is considered ill advised
            return (!string.IsNullOrEmpty(key) && key.All(c =>
                c == '!'
                || c == '-'
                || c == '_'
                || c == '.'
                || c == '*'
                || c == '\''
                || c == '('
                || c == ')'
                || c == '/'
                || ('0' <= c && c <= '9')
                || ('A' <= c && c <= 'Z')
                || ('a' <= c && c <= 'z')) &&
                !key.StartsWith("/"));
        }
    }
}
