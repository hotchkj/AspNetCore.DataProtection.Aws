using System;

namespace AspNetCore.DataProtection.S3
{
    public class S3XmlRepositoryConfig
    {
        public S3XmlRepositoryConfig(string bucketName)
        {
            Bucket = bucketName;
            KeyPrefix = "DataProtection-Keys/";
            MaxS3QueryConcurrency = 10;
        }

        public string Bucket { get; set; }
        public int MaxS3QueryConcurrency { get; set; }
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
