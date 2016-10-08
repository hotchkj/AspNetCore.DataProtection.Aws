// Copyright(c) 2016 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Amazon.S3;
using System;
using System.Linq;

namespace AspNetCore.DataProtection.Aws.S3
{
    /// <summary>
    /// Enables configuration of S3 storage options applied to the ASP.NET key material
    /// </summary>
    public interface IS3XmlRepositoryConfig
    {
        /// <summary>
        /// The bucket to store the keys in
        /// </summary>
        string Bucket { get; }
        /// <summary>
        /// The maximum number of S3 SDK Get Object calls made concurrently to fetch keys when listing all of them
        /// </summary>
        int MaxS3QueryConcurrency { get; }
        /// <summary>
        /// S3 storage class passed to Put Object
        /// </summary>
        S3StorageClass StorageClass { get; }
        /// <summary>
        /// Prefix appended to keys in S3 to enable key partitions, folders etc.
        /// </summary>
        /// <remarks>
        /// This is restricted to characters S3 documentation guarantees are safe:
        /// 
        ///   Alphanumeric characters[0 - 9a - zA - Z]
        ///   Special characters !, -, _, ., *, ', (, and )
        ///   
        /// In addition the prefix may not start with / as this can cause issues in S3 URLs.
        /// </remarks>
        string KeyPrefix { get; }
        /// <summary>
        /// Server side encryption passed to Put Object
        /// </summary>
        ServerSideEncryptionMethod ServerSideEncryptionMethod { get; }
        /// <summary>
        /// Server side customer encryption method passed to Put Object
        /// </summary>
        ServerSideEncryptionCustomerMethod ServerSideEncryptionCustomerMethod { get; }
        /// <summary>
        /// Server side customer encryption key passed to Put Object
        /// </summary>
        string ServerSideEncryptionCustomerProvidedKey { get; }
        /// <summary>
        /// Server side customer encryption key's MD5 passed to Put Object
        /// </summary>
        string ServerSideEncryptionCustomerProvidedKeyMD5 { get; }
        /// <summary>
        /// Server side KMS encryption ID passed to Put Object
        /// </summary>
        string ServerSideEncryptionKeyManagementServiceKeyId { get; }
        /// <summary>
        /// Whether the key will be compressed with gzip prior to storage and stored with Content-Encoding: gzip
        /// </summary>
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
