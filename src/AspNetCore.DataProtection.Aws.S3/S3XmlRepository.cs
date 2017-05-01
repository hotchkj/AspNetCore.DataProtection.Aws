// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Collections.ObjectModel;
using System.IO.Compression;

namespace AspNetCore.DataProtection.Aws.S3
{
    /// <summary>
    /// An XML repository backed by AWS S3.
    /// </summary>
    public sealed class S3XmlRepository : IXmlRepository
    {
        private readonly ILogger logger;
        private readonly IMockingWrapper mockWrapper;
        private readonly IAmazonS3 s3Client;

        /// <summary>
        /// S3 metadata header for the friendly name of the stored XML element.
        /// </summary>
        public const string FriendlyNameMetadata = "xml-friendly-name";

        /// <summary>
        /// Creates a <see cref="S3XmlRepository"/> with keys stored at the given bucket.
        /// </summary>
        /// <param name="s3Client">The S3 client.</param>
        /// <param name="config">The configuration object specifying how to write to S3.</param>
        public S3XmlRepository(IAmazonS3 s3Client, IS3XmlRepositoryConfig config)
            : this(s3Client, config, null)
        {
        }

        /// <summary>
        /// Creates a <see cref="S3XmlRepository"/> with keys stored at the given bucket &amp; optional key prefix.
        /// </summary>
        /// <param name="s3Client">The S3 client.</param>
        /// <param name="config">The configuration object specifying how to write to S3.</param>
        /// <param name="services">An optional <see cref="IServiceProvider"/> to provide ancillary services.</param>
        public S3XmlRepository(IAmazonS3 s3Client, IS3XmlRepositoryConfig config, IServiceProvider services)
            : this(s3Client, config, services, new MockingWrapper())
        {
        }

        /// <summary>
        /// Creates a <see cref="S3XmlRepository"/> with keys stored at the given bucket &amp; optional key prefix.
        /// </summary>
        /// <param name="s3Client">The S3 client.</param>
        /// <param name="config">The configuration object specifying how to write to S3.</param>
        /// <param name="services">An optional <see cref="IServiceProvider"/> to provide ancillary services.</param>
        /// <param name="mockWrapper">Wrapper object to ensure unit testing is feasible.</param>
        public S3XmlRepository(IAmazonS3 s3Client, IS3XmlRepositoryConfig config, IServiceProvider services, IMockingWrapper mockWrapper)
        {
            this.s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
            Config = config ?? throw new ArgumentNullException(nameof(config));
            logger = services?.GetService<ILoggerFactory>()?.CreateLogger<S3XmlRepository>();
            this.mockWrapper = mockWrapper;
        }

        /// <summary>
        /// The bucket into which key material will be written.
        /// </summary>
        public IS3XmlRepositoryConfig Config { get; }

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
                if (Config.ServerSideEncryptionCustomerMethod != ServerSideEncryptionCustomerMethod.None)
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
                    // Stream returned from AWS SDK does not automatically uncompress even with Content-Encoding set
                    // Not that surprising considering that S3 treats the data as just N bytes; that it was compressed
                    // client-side doesn't really matter.
                    //
                    // Compatibility: If we set compress=true but load something without gzip encoding then skip and
                    // load as uncompressed. If we set compress=false but load something with gzip encoding, load as
                    // compressed otherwise loading won't work.
                    if (response.Headers.ContentEncoding == "gzip")
                    {
                        using (var responseStream = new GZipStream(response.ResponseStream, CompressionMode.Decompress))
                        {
                            return XElement.Load(responseStream);
                        }
                    }
                    else
                    {
                        return XElement.Load(response.ResponseStream);
                    }
                }
            }
            finally
            {
                throttler.Release();
            }
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
            var key = Config.KeyPrefix + mockWrapper.GetNewGuid() + ".xml";
            logger?.LogDebug("Storing DataProtection key at S3 location {0} in bucket {1}, friendly name of {2} as metadata", key, Config.Bucket, friendlyName);

            var pr = new PutObjectRequest
            {
                BucketName = Config.Bucket,
                Key = key,
                ServerSideEncryptionMethod = Config.ServerSideEncryptionMethod,
                ServerSideEncryptionCustomerMethod = ServerSideEncryptionCustomerMethod.None,
                AutoResetStreamPosition = false,
                AutoCloseStream = true,
                ContentType = "text/xml",
                StorageClass = Config.StorageClass
            };
            pr.Metadata.Add(FriendlyNameMetadata, friendlyName);
            pr.Headers.ContentDisposition = "attachment; filename=" + friendlyName + ".xml";

            if (Config.ServerSideEncryptionMethod == ServerSideEncryptionMethod.AWSKMS)
            {
                pr.ServerSideEncryptionKeyManagementServiceKeyId = Config.ServerSideEncryptionKeyManagementServiceKeyId;
            }
            else if (Config.ServerSideEncryptionCustomerMethod != ServerSideEncryptionCustomerMethod.None)
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

                outputStream.Seek(0, SeekOrigin.Begin);
                using (var hasher = MD5.Create())
                {
                    pr.MD5Digest = Convert.ToBase64String(hasher.ComputeHash(outputStream));
                }

                outputStream.Seek(0, SeekOrigin.Begin);
                pr.InputStream = outputStream;

                await s3Client.PutObjectAsync(pr, ct).ConfigureAwait(false);
            }
        }
    }
}
