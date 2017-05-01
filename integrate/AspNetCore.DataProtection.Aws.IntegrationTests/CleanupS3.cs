// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Amazon.S3;
using Amazon.S3.Model;
using System.Threading.Tasks;

namespace AspNetCore.DataProtection.Aws.IntegrationTests
{
    public interface ICleanupS3
    {
        Task ClearKeys(string bucket, string prefix);
    }

    public class CleanupS3 : ICleanupS3
    {
        private readonly IAmazonS3 s3Client;

        public CleanupS3(IAmazonS3 s3Client)
        {
            this.s3Client = s3Client;
        }

        public async Task ClearKeys(string bucket, string prefix)
        {
            // XmlKeyManager uses a GUID for the naming so we cannot overwrite the same entry in the test
            // Thus we must first clear out any keys that old tests put in

            var listed = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = bucket,
                Prefix = prefix
            });

            // In sequence as we do not expect more than one or two of these assuming the tests work properly
            foreach (var s3Obj in listed.S3Objects)
            {
                await s3Client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = bucket,
                    Key = s3Obj.Key
                });
            }
        }
    }
}
