using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNet.DataProtection.Repositories;
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

namespace AspNetCore.DataProtection.S3
{
    // <summary>
    /// An XML repository backed by AWS S3.
    /// </summary>
    public class S3XmlRepository : IXmlRepository
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Creates a <see cref="S3XmlRepository"/> with keys stored at the given bucket.
        /// </summary>
        /// <param name="s3client">The S3 client.</param>
        /// <param name="config">The configuration object specifying how to write to S3.</param>
        public S3XmlRepository(IAmazonS3 s3client, S3XmlRepositoryConfig config)
            : this(s3client, config, services: null)
        {
        }

        /// <summary>
        /// Creates a <see cref="S3XmlRepository"/> with keys stored at the given bucket & optional key prefix.
        /// </summary>
        /// <param name="s3client">The S3 client.</param>
		/// <param name="config">The configuration object specifying how to write to S3.</param>
        /// <param name="services">An optional <see cref="IServiceProvider"/> to provide ancillary services.</param>
        public S3XmlRepository(IAmazonS3 s3client, S3XmlRepositoryConfig config, IServiceProvider services)
        {
            if (s3client == null)
            {
                throw new ArgumentNullException(nameof(s3client));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            S3Client = s3client;
            Config = config;
            Services = services;
            _logger = services?.GetService<ILoggerFactory>()?.CreateLogger<S3XmlRepository>();
        }

        /// <summary>
        /// The bucket into which key material will be written.
        /// </summary>
        public S3XmlRepositoryConfig Config { get; }

        /// <summary>
        /// The <see cref="IServiceProvider"/> provided to the constructor.
        /// </summary>
        protected IServiceProvider Services { get; }

        /// <summary>
        /// The <see cref="IAmazonS3"/> provided to the constructor.
        /// </summary>
        protected IAmazonS3 S3Client { get; }

        public IReadOnlyCollection<XElement> GetAllElements()
        {
            // Due to time constraints, Microsoft didn't make the interfaces async
            // https://github.com/aspnet/DataProtection/issues/124
            // so loft the heavy lifting into a thread which enables safe async behaviour with some additional cost
            // Overhead should be acceptable since key management isn't a frequent thing
            return Task.Run(()=> GetAllElementsAsync(CancellationToken.None)).Result;
        }

        // Not part of the IXmlRepository interface
        public async Task<IReadOnlyCollection<XElement>> GetAllElementsAsync(CancellationToken ct)
        {
            var items = new List<S3Object>();
            ListObjectsV2Response response = null;
            do
            {
                response = await S3Client.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = Config.Bucket,
                    Prefix = Config.KeyPrefix,
                    ContinuationToken = response?.NextContinuationToken
                },
                ct);

                items.AddRange(response.S3Objects);
            }
            while (response.IsTruncated);

            // TODO Could use ETag to cache and avoid S3 queries

            // Limit the number of concurrent requests to required value
            var throttler = new SemaphoreSlim(initialCount: Config.MaxS3QueryConcurrency);
            var queries = new List<Task<XElement>>();
            foreach (var item in items)
            {
                queries.Add(GetElementFromKey(item, throttler, ct));
            }

            await Task.WhenAll(queries);

            return new ReadOnlyCollection<XElement>(queries.Select(x => x.Result).ToList());
        }

        private async Task<XElement> GetElementFromKey(S3Object item, SemaphoreSlim throttler, CancellationToken ct)
        {
            await throttler.WaitAsync(ct);

            try
            {
                using (var response = await S3Client.GetObjectAsync(new GetObjectRequest
                {
                    BucketName = Config.Bucket,
                    Key = item.Key
                }, ct).ConfigureAwait(false))
                {
                    return XElement.Load(response.ResponseStream);
                }
            }
            finally
            {
                throttler.Release();
            }
        }

        public void StoreElement(XElement element, string friendlyName)
        {
            Task.Run(() => StoreElementAsync(element, friendlyName, CancellationToken.None)).Wait();
        }

        // Not part of the IXmlRepository interface
        public async Task StoreElementAsync(XElement element, string friendlyName, CancellationToken ct)
        {
            string key;
            if (!IsSafeS3Key(friendlyName))
            {
                key = Config.KeyPrefix + Guid.NewGuid() + ".xml";
                _logger?.LogWarning("DataProtection key friendly name {0} is not safe for S3, ignoring and using key {1}", friendlyName, key);
            }
            else
            {
                key = Config.KeyPrefix + friendlyName + ".xml";
            }

            using (var stream = new MemoryStream())
            {
                element.Save(stream);
                stream.Seek(0, SeekOrigin.Begin);

                var hasher = MD5.Create();
                var md5 = Convert.ToBase64String(hasher.ComputeHash(stream));

                await S3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = Config.Bucket,
                    Key = key,
                    InputStream = stream,
                    ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
                    AutoResetStreamPosition = true,
                    AutoCloseStream = true,
                    MD5Digest = md5,
                    ContentType = "text/xml"
                }, ct).ConfigureAwait(false);
            }
        }

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
