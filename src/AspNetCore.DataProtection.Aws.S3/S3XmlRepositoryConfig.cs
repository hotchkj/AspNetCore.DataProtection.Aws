// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System;
using System.Linq;
using Amazon.S3;

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
        string ServerSideEncryptionCustomerProvidedKeyMd5 { get; }

        /// <summary>
        /// Server side KMS encryption ID passed to Put Object
        /// </summary>
        string ServerSideEncryptionKeyManagementServiceKeyId { get; }

        /// <summary>
        /// Whether the key will be compressed with gzip prior to storage and stored with Content-Encoding: gzip
        /// </summary>
        bool ClientSideCompression { get; }
    }

    /// <inheritdoc/>
    public class S3XmlRepositoryConfig : IS3XmlRepositoryConfig
    {
        /// <summary>
        /// Constructs S3 XML repository configuration. Bucket name <b>must</b> be configured e.g. via options binding.
        /// </summary>
        /// <remarks>
        /// See <see cref="CopyFrom"/> as to why this is needed.
        /// </remarks>
        public S3XmlRepositoryConfig()
        {
            SetToDefaults();
        }

        // ReSharper disable once InheritdocConsiderUsage
        /// <summary>
        /// Constructs S3 XML repository configuration.
        /// </summary>
        /// <param name="bucketName">Name of S3 bucket to use for storage.</param>
        public S3XmlRepositoryConfig(string bucketName)
            : this()
        {
            Bucket = bucketName;
        }

        /// <summary>
        /// Copies settings from another settings object.
        /// </summary>
        /// <remarks>
        /// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/> requires a parameterless constructor, so we end up with nasty hackery like this for handling programmatic options specification.
        /// </remarks>
        /// <param name="input">Input from which to copy configuration.</param>
        public void CopyFrom(IS3XmlRepositoryConfig input)
        {
            Bucket = input.Bucket;
            KeyPrefix = input.KeyPrefix;
            MaxS3QueryConcurrency = input.MaxS3QueryConcurrency;
            StorageClass = input.StorageClass;
            ServerSideEncryptionMethod = input.ServerSideEncryptionMethod;
            ServerSideEncryptionCustomerMethod = input.ServerSideEncryptionCustomerMethod;
            ServerSideEncryptionCustomerProvidedKey = input.ServerSideEncryptionCustomerProvidedKey;
            ServerSideEncryptionCustomerProvidedKeyMd5 = input.ServerSideEncryptionCustomerProvidedKeyMd5;
            ServerSideEncryptionKeyManagementServiceKeyId = input.ServerSideEncryptionKeyManagementServiceKeyId;
            ClientSideCompression = input.ClientSideCompression;
        }

        /// <summary>
        /// Sets the configuration to default values.
        /// </summary>
        public void SetToDefaults()
        {
            KeyPrefix = "DataProtection-Keys/";
            MaxS3QueryConcurrency = 10;
            StorageClass = S3StorageClass.Standard;
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256;
            ServerSideEncryptionCustomerMethod = ServerSideEncryptionCustomerMethod.None;
            ServerSideEncryptionCustomerProvidedKey = null;
            ServerSideEncryptionCustomerProvidedKeyMd5 = null;
            ServerSideEncryptionKeyManagementServiceKeyId = null;
            ClientSideCompression = true;
        }

        /// <inheritdoc/>
        public string Bucket { get; set; }

        /// <inheritdoc/>
        public int MaxS3QueryConcurrency { get; set; }

        /// <inheritdoc/>
        public S3StorageClass StorageClass { get; set; }

        /// <inheritdoc/>
        public string KeyPrefix
        {
            get => keyPrefix;
            set
            {
                if (!IsSafeS3Key(value))
                {
                    throw new ArgumentException($"Specified key prefix {value} is not considered a safe S3 name", nameof(value));
                }
                keyPrefix = value;
            }
        }

        /// <inheritdoc/>
        public ServerSideEncryptionMethod ServerSideEncryptionMethod { get; set; }

        /// <inheritdoc/>
        public ServerSideEncryptionCustomerMethod ServerSideEncryptionCustomerMethod { get; set; }

        /// <inheritdoc/>
        public string ServerSideEncryptionCustomerProvidedKey { get; set; }

        /// <inheritdoc/>
        public string ServerSideEncryptionCustomerProvidedKeyMd5 { get; set; }

        /// <inheritdoc/>
        public string ServerSideEncryptionKeyManagementServiceKeyId { get; set; }

        /// <inheritdoc/>
        public bool ClientSideCompression { get; set; }

        private string keyPrefix;

        internal static bool IsSafeS3Key(string key)
        {
            // From S3 docs:
            // The following character sets are generally safe for use in key names:
            // Alphanumeric characters[0 - 9a - zA - Z]
            // Special characters !, -, _, ., *, ', (, and )
            // Singular entry of the folder delimiter is considered ill advised
            return !string.IsNullOrEmpty(key) &&
                   key.All(c =>
                               c == '!' ||
                               c == '-' ||
                               c == '_' ||
                               c == '.' ||
                               c == '*' ||
                               c == '\'' ||
                               c == '(' ||
                               c == ')' ||
                               c == '/' ||
                               '0' <= c && c <= '9' ||
                               'A' <= c && c <= 'Z' ||
                               'a' <= c && c <= 'z') &&
                   !key.StartsWith("/");
        }
    }
}
