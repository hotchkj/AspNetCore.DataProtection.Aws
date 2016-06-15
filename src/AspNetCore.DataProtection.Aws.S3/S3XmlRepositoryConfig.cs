// Copyright(c) 2016 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Amazon.S3;
using System;

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
                if (!S3XmlRepository.IsSafeS3Key(value))
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
    }
}
