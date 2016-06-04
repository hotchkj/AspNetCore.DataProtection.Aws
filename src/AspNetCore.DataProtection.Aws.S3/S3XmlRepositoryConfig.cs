// Copyright(c) 2016 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Amazon.S3;
using System;

namespace AspNetCore.DataProtection.Aws.S3
{
    public class S3XmlRepositoryConfig
    {
        public S3XmlRepositoryConfig(string bucketName)
        {
            Bucket = bucketName;
            KeyPrefix = "DataProtection-Keys/";
            MaxS3QueryConcurrency = 10;
            StorageClass = S3StorageClass.Standard;
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
                    throw new ArgumentException("Specified key prefix is not considered a safe S3 name", "value");
                }
                _keyPrefix = value;
            }
        }

        private string _keyPrefix;
    }
}
