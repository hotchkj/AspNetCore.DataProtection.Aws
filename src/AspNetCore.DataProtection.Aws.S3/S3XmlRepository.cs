// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Amazon.S3;
using Amazon.S3.Model;
using AspNetCore.DataProtection.Aws.S3.Internals;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace AspNetCore.DataProtection.Aws.S3
{
    // ReSharper disable once InheritdocConsiderUsage
    /// <summary>
    /// An XML repository backed by AWS S3.
    /// </summary>
    public sealed class S3XmlRepository : IXmlRepository
    {
        private readonly ILogger logger;
        private readonly IMockingWrapper mockWrapper;
        private readonly IAmazonS3 s3Client;
        private readonly IOptions<S3XmlRepositoryConfig> config;

        /// <summary>
        /// S3 metadata header for the friendly name of the stored XML element.
        /// </summary>
        public const string FriendlyNameMetadata = "xml-friendly-name";

        /// <summary>
        /// Actual value returned responses should contain (assuming AWS don't alter the header prefixes).
        /// </summary>
        public const string FriendlyNameActualMetadataHeader = "x-amz-meta-" + FriendlyNameMetadata;

        /// <summary>
        /// S3 metadata header explicitly storing the MD5 value.
        /// </summary>
        /// <remarks>
        /// The S3 ETag often reflects this, but since multi-part upload can disrupt this, it's just safer to have an explicit record.
        /// </remarks>
        public const string Md5Metadata = "md5-hash";

        /// <summary>
        /// Actual value returned responses should contain (assuming AWS don't alter the header prefixes).
        /// </summary>
        public const string Md5ActualMetadataHeader = "x-amz-meta-" + Md5Metadata;

        // ReSharper disable once InheritdocConsiderUsage
        /// <summary>
        /// Creates a <see cref="S3XmlRepository"/> with keys stored at the given bucket.
        /// </summary>
        /// <param name="s3Client">The S3 client.</param>
        /// <param name="config">The configuration object specifying how to write to S3.</param>
        public S3XmlRepository(IAmazonS3 s3Client, IOptions<S3XmlRepositoryConfig> config)
            : this(s3Client, config, null)
        {
        }

        // ReSharper disable once InheritdocConsiderUsage
        /// <summary>
        /// Creates a <see cref="S3XmlRepository"/> with keys stored at the given bucket &amp; optional key prefix.
        /// </summary>
        /// <param name="s3Client">The S3 client.</param>
        /// <param name="config">The configuration object specifying how to write to S3.</param>
        /// <param name="loggerFactory">An optional <see cref="ILoggerFactory"/> to provide logging infrastructure.</param>
        public S3XmlRepository(IAmazonS3 s3Client, IOptions<S3XmlRepositoryConfig> config, ILoggerFactory loggerFactory)
            : this(s3Client, config, loggerFactory, new MockingWrapper())
        {
        }

        /// <summary>
        /// Creates a <see cref="S3XmlRepository"/> with keys stored at the given bucket &amp; optional key prefix.
        /// </summary>
        /// <param name="s3Client">The S3 client.</param>
        /// <param name="config">The configuration object specifying how to write to S3.</param>
        /// <param name="loggerFactory">An optional <see cref="ILoggerFactory"/> to provide logging infrastructure.</param>
        /// <param name="mockWrapper">Wrapper object to ensure unit testing is feasible.</param>
        public S3XmlRepository(IAmazonS3 s3Client, IOptions<S3XmlRepositoryConfig> config, ILoggerFactory loggerFactory, IMockingWrapper mockWrapper)
        {
            this.s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            logger = loggerFactory?.CreateLogger<S3XmlRepository>();
            this.mockWrapper = mockWrapper;
        }

        /// <summary>
        /// Configuration such as the bucket into which key material will be written.
        /// </summary>
        public IS3XmlRepositoryConfig Config => config.Value;

        /// <summary>
        /// Ensure configuration is valid for usage.
        /// </summary>
        public void ValidateConfig()
        {
            // Microsoft haven't provided for any validation of options as yet, so what was originally a constructor argument must now be validated by hand at runtime (yuck)
            if (string.IsNullOrWhiteSpace(Config.Bucket))
            {
                throw new ArgumentException($"A bucket name is required in {nameof(IS3XmlRepositoryConfig)} for S3 access");
            }
        }

        /// <inheritdoc/>
        public IReadOnlyCollection<XElement> GetAllElements()
        {
            // Due to time constraints, Microsoft didn't make the interfaces async
            // https://github.com/aspnet/DataProtection/issues/124
            // so loft the heavy lifting into a thread which enables safe async behaviour with some additional cost
            // Overhead should be acceptable since key management isn't a frequent thing
            return Task.Run(() => GetAllElementsAsync(CancellationToken.None)).Result;
        }

        /// <summary>
        /// Pulls out all stored XML elements from S3.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Collection of retrieved XML elements.</returns>
        public async Task<IReadOnlyCollection<XElement>> GetAllElementsAsync(CancellationToken ct)
        {
            ValidateConfig();

            var items = new List<S3Object>();
            ListObjectsV2Response response = null;
            do
            {
                response = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
                                                             {
                                                                 BucketName = Config.Bucket,
                                                                 Prefix = Config.KeyPrefix,
                                                                 ContinuationToken = response?.NextContinuationToken
                                                             },
                                                             ct)
                                         .ConfigureAwait(false);

                items.AddRange(response.S3Objects);
            }
            while (response.IsTruncated);

            // ASP.NET docs state:
            //   When the data protection system initializes, it reads the key ring from the underlying repository and caches it in memory.
            //   This cache allows Protect and Unprotect operations to proceed without hitting the backing store. The system will automatically
            //   check the backing store for changes approximately every 24 hours or when the current default key expires, whichever comes first.
            // So there should be no need to do any special ETag or other similar caching at this layer - key manager is already doing it.

            // Limit the number of concurrent requests to required value
            using (var throttler = new SemaphoreSlim(Config.MaxS3QueryConcurrency))
            {
                var queries = new List<Task<XElement>>();
                foreach (var item in items)
                {
                    queries.Add(GetElementFromKey(item, throttler, ct));
                }

                await Task.WhenAll(queries).ConfigureAwait(false);

                return new ReadOnlyCollection<XElement>(queries.Select(x => x.Result).Where(x => x != null).ToList());
            }
        }

        private async Task<XElement> GetElementFromKey(S3Object item, SemaphoreSlim throttler, CancellationToken ct)
        {
            await throttler.WaitAsync(ct);

            try
            {
                logger?.LogDebug("Retrieving DataProtection key at S3 location {0} in bucket {1}", item.Key, Config.Bucket);

                var gr = new GetObjectRequest
                {
                    BucketName = Config.Bucket,
                    Key = item.Key,
                    ServerSideEncryptionCustomerMethod = ServerSideEncryptionCustomerMethod.None
                };
                if (Config.ServerSideEncryptionCustomerMethod != null &&
                    Config.ServerSideEncryptionCustomerMethod != ServerSideEncryptionCustomerMethod.None)
                {
                    gr.ServerSideEncryptionCustomerMethod = Config.ServerSideEncryptionCustomerMethod;
                    gr.ServerSideEncryptionCustomerProvidedKey = Config.ServerSideEncryptionCustomerProvidedKey;
                    gr.ServerSideEncryptionCustomerProvidedKeyMD5 = Config.ServerSideEncryptionCustomerProvidedKeyMd5;
                }

                using (var response = await s3Client.GetObjectAsync(gr, ct).ConfigureAwait(false))
                {
                    // Skip empty folder keys
                    if (item.Key.EndsWith("/") && response.ContentLength == 0)
                    {
                        return null;
                    }

                    // Look for checksum. If it's being checked and the ETag is suitable, trust that most of all (since S3 calculates this post-upload and
                    // upload content is checked by Md5Digest), otherwise use MD5 metadata if configured. AWS SDK does the ETag check for us at time of writing, so usually
                    // ValidateETag isn't needed.
                    string headerChecksum = null;
                    bool testChecksum = Config.ValidateETag && GetETagChecksum(response, out headerChecksum) ||
                                        Config.ValidateMd5Metadata && GetHeaderChecksum(response, out headerChecksum);

                    using (var md5 = testChecksum ? MD5.Create() : null)
                    {
                        XElement elementToReturn;
                        using (var hashStream = testChecksum ? new CryptoStream(response.ResponseStream, md5, CryptoStreamMode.Read) : response.ResponseStream)
                        {
                            // Stream returned from AWS SDK does not automatically uncompress even with Content-Encoding set
                            // Not that surprising considering that S3 treats the data as just N bytes; that it was compressed
                            // client-side doesn't really matter.
                            //
                            // Compatibility: If we set compress=true but load something without gzip encoding then skip and
                            // load as uncompressed. If we set compress=false but load something with gzip encoding, load as
                            // compressed otherwise loading won't work.
                            if (response.Headers.ContentEncoding == "gzip")
                            {
                                using (var responseStream = new GZipStream(hashStream, CompressionMode.Decompress))
                                {
                                    elementToReturn = XElement.Load(responseStream);
                                }
                            }
                            else
                            {
                                elementToReturn = XElement.Load(hashStream);
                            }
                        }

                        if (testChecksum)
                        {
                            var md5Value = BitConverter.ToString(md5.Hash).Replace("-", "").ToLowerInvariant();
                            if (md5Value != headerChecksum)
                            {
                                throw new InvalidOperationException($"Streamed S3 data has MD5 of {md5Value} which does not match provided MD5 metadata {headerChecksum} - corruption in transit");
                            }
                        }

                        return elementToReturn;
                    }
                }
            }
            finally
            {
                throttler.Release();
            }
        }

        private bool GetHeaderChecksum(GetObjectResponse s3Response, out string checksum)
        {
            if (s3Response.Metadata.Keys.Contains(Md5ActualMetadataHeader))
            {
                checksum = s3Response.Metadata[Md5ActualMetadataHeader].ToLowerInvariant();
                return true;
            }
            checksum = null;
            return false;
        }

        private bool GetETagChecksum(GetObjectResponse s3Response, out string checksum)
        {
            // AWS SDK already should check the ETag, but leaving the code available for sanity checking.
            // ETag does not match for KMS or customer-encrypted, presumably since there is an upload-then-encrypt workflow applied.
            // ETag isn't a useful checksum in the case of multi-part upload, since you have to know the part size.
            if (s3Response.ServerSideEncryptionMethod != ServerSideEncryptionMethod.AWSKMS &&
                s3Response.ServerSideEncryptionCustomerMethod == ServerSideEncryptionCustomerMethod.None &&
                !string.IsNullOrEmpty(s3Response.ETag) &&
                s3Response.ETag.Length == 34 &&
                !s3Response.ETag.Contains("-"))
            {
                checksum = s3Response.ETag.Substring(1, 32).ToLowerInvariant();
                return true;
            }
            checksum = null;
            return false;
        }

        /// <inheritdoc/>
        public void StoreElement(XElement element, string friendlyName)
        {
            Task.Run(() => StoreElementAsync(element, friendlyName, CancellationToken.None)).Wait();
        }

        /// <summary>
        /// Stores the provided XML element into S3.
        /// </summary>
        /// <param name="element">XML to store.</param>
        /// <param name="friendlyName">Friendly name of the XML (usually, ironically, a GUID).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns></returns>
        public async Task StoreElementAsync(XElement element, string friendlyName, CancellationToken ct)
        {
            ValidateConfig();

            var key = Config.KeyPrefix + mockWrapper.GetNewGuid() + ".xml";
            logger?.LogDebug("Storing DataProtection key at S3 location {0} in bucket {1}, friendly name of {2} as metadata", key, Config.Bucket, friendlyName);

            var expectedMetadata = await PutElement(key, element, friendlyName, ct).ConfigureAwait(false);

            await CheckElement(key, expectedMetadata, ct).ConfigureAwait(false);
        }

        private async Task<string> PutElement(string key, XElement element, string friendlyName, CancellationToken ct)
        {
            var pr = new PutObjectRequest
            {
                BucketName = Config.Bucket,
                Key = key,
                ServerSideEncryptionMethod = Config.ServerSideEncryptionMethod != null ? Config.ServerSideEncryptionMethod : ServerSideEncryptionMethod.AES256,
                ServerSideEncryptionCustomerMethod = ServerSideEncryptionCustomerMethod.None,
                AutoResetStreamPosition = false,
                AutoCloseStream = true,
                ContentType = "text/xml",
                StorageClass = Config.StorageClass
            };
            pr.Metadata.Add(FriendlyNameMetadata, friendlyName);
            pr.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = friendlyName + ".xml", FileNameStar = friendlyName + ".xml" }.ToString();

            if (Config.ServerSideEncryptionMethod == ServerSideEncryptionMethod.AWSKMS)
            {
                pr.ServerSideEncryptionKeyManagementServiceKeyId = Config.ServerSideEncryptionKeyManagementServiceKeyId;
            }
            else if (Config.ServerSideEncryptionCustomerMethod != null &&
                     Config.ServerSideEncryptionCustomerMethod != ServerSideEncryptionCustomerMethod.None)
            {
                pr.ServerSideEncryptionMethod = ServerSideEncryptionMethod.None;
                pr.ServerSideEncryptionCustomerMethod = Config.ServerSideEncryptionCustomerMethod;
                pr.ServerSideEncryptionCustomerProvidedKey = Config.ServerSideEncryptionCustomerProvidedKey;
                pr.ServerSideEncryptionCustomerProvidedKeyMD5 = Config.ServerSideEncryptionCustomerProvidedKeyMd5;
            }

            using (var outputStream = new MemoryStream())
            {
                if (Config.ClientSideCompression)
                {
                    // Enable S3 to serve the content so that it automatically unzips in browser
                    // Note that this doesn't apply to the streams AWS SDK returns!
                    // Also provides a very convenient discriminator for whether the key is compressed
                    pr.Headers.ContentEncoding = "gzip";
                    using (var inputStream = new MemoryStream())
                    {
                        using (var gZippedstream = new GZipStream(inputStream, CompressionMode.Compress, true))
                        {
                            element.Save(gZippedstream);
                        }
                        byte[] inputArray = inputStream.ToArray();
                        await outputStream.WriteAsync(inputArray, 0, inputArray.Length, ct);
                    }
                }
                else
                {
                    element.Save(outputStream);
                }

                string md5Hex;
                outputStream.Seek(0, SeekOrigin.Begin);
                using (var hasher = MD5.Create())
                {
                    var md5Bytes = hasher.ComputeHash(outputStream);
                    var md5Base64 = Convert.ToBase64String(md5Bytes);
                    md5Hex = BitConverter.ToString(md5Bytes).Replace("-", "").ToLowerInvariant(); // Not necessarily the most optimal but will do for now
                    pr.MD5Digest = md5Base64;
                    pr.Metadata.Add(Md5Metadata, md5Hex);
                }

                outputStream.Seek(0, SeekOrigin.Begin);
                pr.InputStream = outputStream;

                await s3Client.PutObjectAsync(pr, ct).ConfigureAwait(false);

                return md5Hex;
            }
        }

        private async Task CheckElement(string key, string expectedMetadata, CancellationToken ct)
        {
            var gomr = new GetObjectMetadataRequest
            {
                BucketName = Config.Bucket,
                Key = key,
                ServerSideEncryptionCustomerMethod = ServerSideEncryptionCustomerMethod.None
            };

            if (Config.ServerSideEncryptionCustomerMethod != null &&
                Config.ServerSideEncryptionCustomerMethod != ServerSideEncryptionCustomerMethod.None)
            {
                gomr.ServerSideEncryptionCustomerMethod = Config.ServerSideEncryptionCustomerMethod;
                gomr.ServerSideEncryptionCustomerProvidedKey = Config.ServerSideEncryptionCustomerProvidedKey;
                gomr.ServerSideEncryptionCustomerProvidedKeyMD5 = Config.ServerSideEncryptionCustomerProvidedKeyMd5;
            }

            var response = await s3Client.GetObjectMetadataAsync(gomr, ct).ConfigureAwait(false);

            if (response.Metadata[Md5ActualMetadataHeader] != expectedMetadata)
            {
                throw new InvalidOperationException("Metadata returned by HEAD is not as expected from PUT; potential corruption in transit");
            }
        }
    }
}
