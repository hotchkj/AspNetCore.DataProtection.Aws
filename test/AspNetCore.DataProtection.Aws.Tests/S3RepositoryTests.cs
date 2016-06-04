using Amazon.S3;
using Amazon.S3.Model;
using AspNetCore.DataProtection.Aws.S3;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Xunit;

namespace AspNetCore.DataProtection.Aws.IntegrationTests
{
    public sealed class S3RespositoryTests : IDisposable
    {
        private readonly S3XmlRepository xmlRepository;
        private readonly S3XmlRepositoryConfig config;
        private readonly MockRepository repository;
        private readonly Mock<IAmazonS3> s3Client;
        private const string ElementName = "name";
        private const string ElementContent = "test";
        private const string Bucket = "bucket";
        private const string Prefix = "prefix";

        public S3RespositoryTests()
        {
            repository = new MockRepository(MockBehavior.Strict);
            s3Client = repository.Create<IAmazonS3>();
            config = new S3XmlRepositoryConfig(Bucket);
            config.KeyPrefix = Prefix;
            xmlRepository = new S3XmlRepository(s3Client.Object, config);
        }

        public void Dispose()
        {
            repository.VerifyAll();
        }

        [Fact]
        public void ExpectStoreToSucceed()
        {
            var myXml = new XElement(ElementName, ElementContent);
            var myTestName = "friendly";

            // Response isn't queried, so can be default arguments
            var response = new PutObjectResponse();

            s3Client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), CancellationToken.None))
                    .ReturnsAsync(response)
                    .Callback<PutObjectRequest, CancellationToken>((pr, ct) =>
                    {
                        Assert.Equal(Bucket, pr.BucketName);
                        Assert.Equal(ServerSideEncryptionMethod.AES256, pr.ServerSideEncryptionMethod);
                        Assert.Equal(Prefix + myTestName + ".xml", pr.Key);

                        // Stream is written to and positioned at the end - we require that S3 resets it
                        // and must do so here in our test
                        Assert.True(pr.AutoResetStreamPosition);
                        pr.InputStream.Seek(0, SeekOrigin.Begin);

                        var body = XElement.Load(pr.InputStream);
                        Assert.True(XNode.DeepEquals(myXml, body));
                    });

            xmlRepository.StoreElement(myXml, myTestName);
        }

        [Fact]
        public void ExpectEmptyQueryToSucceed()
        {
            var listResponse = new ListObjectsV2Response
            {
                Name = Bucket,
                Prefix = Prefix
            };
            s3Client.Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), CancellationToken.None))
                    .ReturnsAsync(listResponse)
                    .Callback<ListObjectsV2Request, CancellationToken>((lr, ct) =>
                    {
                        Assert.Equal(Bucket, lr.BucketName);
                        Assert.Equal(Prefix, lr.Prefix);
                        Assert.Null(lr.ContinuationToken);
                    });

            var list = xmlRepository.GetAllElements();

            Assert.Equal(0, list.Count);
        }

        [Fact]
        public void ExpectSingleQueryToSucceed()
        {
            var key = "key";
            var etag = "etag";

            var listResponse = new ListObjectsV2Response
            {
                Name = Bucket,
                Prefix = Prefix,
                S3Objects = new List<S3Object>
                {
                    new S3Object
                    {
                        Key = key,
                        ETag = etag
                    }
                },
                IsTruncated = false
            };
            s3Client.Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), CancellationToken.None))
                    .ReturnsAsync(listResponse)
                    .Callback<ListObjectsV2Request, CancellationToken>((lr, ct) =>
                    {
                        Assert.Equal(Bucket, lr.BucketName);
                        Assert.Equal(Prefix, lr.Prefix);
                        Assert.Null(lr.ContinuationToken);
                    });

            using (var returnedStream = new MemoryStream())
            {
                var myXml = new XElement(ElementName, ElementContent);
                myXml.Save(returnedStream);
                returnedStream.Seek(0, SeekOrigin.Begin);

                var getResponse = new GetObjectResponse
                {
                    BucketName = Bucket,
                    ETag = etag,
                    Key = key,
                    ResponseStream = returnedStream
                };
                s3Client.Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), CancellationToken.None))
                        .ReturnsAsync(getResponse)
                        .Callback<GetObjectRequest, CancellationToken>((gr, ct) =>
                        {
                            Assert.Equal(Bucket, gr.BucketName);
                            Assert.Equal(key, gr.Key);
                        });

                var list = xmlRepository.GetAllElements();

                Assert.Equal(1, list.Count);

                Assert.True(XNode.DeepEquals(myXml, list.First()));
            }
        }

        [Fact]
        public void ExpectMultiQueryToSucceed()
        {
            var key1 = "key1";
            var etag1 = "etag1";
            var key2 = "key2";
            var etag2 = "etag2";
            var nextToken = "next";

            var listResponse1 = new ListObjectsV2Response
            {
                Name = Bucket,
                Prefix = Prefix,
                S3Objects = new List<S3Object>
                {
                    new S3Object
                    {
                        Key = key1,
                        ETag = etag1
                    }
                },
                IsTruncated = true,
                NextContinuationToken = nextToken
            };
            var listResponse2 = new ListObjectsV2Response
            {
                Name = Bucket,
                Prefix = Prefix,
                S3Objects = new List<S3Object>
                {
                    new S3Object
                    {
                        Key = key2,
                        ETag = etag2
                    }
                },
                IsTruncated = false
            };
            s3Client.Setup(x => x.ListObjectsV2Async(It.Is<ListObjectsV2Request>(lr => lr.ContinuationToken == null), CancellationToken.None))
                    .ReturnsAsync(listResponse1)
                    .Callback<ListObjectsV2Request, CancellationToken>((lr, ct) =>
                    {
                        Assert.Equal(Bucket, lr.BucketName);
                        Assert.Equal(Prefix, lr.Prefix);
                    });
            s3Client.Setup(x => x.ListObjectsV2Async(It.Is<ListObjectsV2Request>(lr => lr.ContinuationToken != null), CancellationToken.None))
                    .ReturnsAsync(listResponse2)
                    .Callback<ListObjectsV2Request, CancellationToken>((lr, ct) =>
                    {
                        Assert.Equal(Bucket, lr.BucketName);
                        Assert.Equal(Prefix, lr.Prefix);
                        Assert.Equal(nextToken, lr.ContinuationToken);
                    });

            using (var returnedStream1 = new MemoryStream())
            using (var returnedStream2 = new MemoryStream())
            {
                var myXml1 = new XElement(ElementName, ElementContent + "1");
                var myXml2 = new XElement(ElementName, ElementContent + "2");
                myXml1.Save(returnedStream1);
                returnedStream1.Seek(0, SeekOrigin.Begin);
                myXml2.Save(returnedStream2);
                returnedStream2.Seek(0, SeekOrigin.Begin);

                var getResponse1 = new GetObjectResponse
                {
                    BucketName = Bucket,
                    ETag = etag1,
                    Key = key1,
                    ResponseStream = returnedStream1
                };
                var getResponse2 = new GetObjectResponse
                {
                    BucketName = Bucket,
                    ETag = etag2,
                    Key = key2,
                    ResponseStream = returnedStream2
                };
                s3Client.Setup(x => x.GetObjectAsync(It.Is<GetObjectRequest>(gr => gr.Key == key1), CancellationToken.None))
                        .ReturnsAsync(getResponse1)
                        .Callback<GetObjectRequest, CancellationToken>((gr, ct) =>
                        {
                            Assert.Equal(Bucket, gr.BucketName);
                            Assert.Equal(key1, gr.Key);
                        });
                s3Client.Setup(x => x.GetObjectAsync(It.Is<GetObjectRequest>(gr => gr.Key == key2), CancellationToken.None))
                        .ReturnsAsync(getResponse2)
                        .Callback<GetObjectRequest, CancellationToken>((gr, ct) =>
                        {
                            Assert.Equal(Bucket, gr.BucketName);
                            Assert.Equal(key2, gr.Key);
                        });

                var list = xmlRepository.GetAllElements();

                Assert.Equal(2, list.Count);

                Assert.True(XNode.DeepEquals(myXml1, list.First()));
                Assert.True(XNode.DeepEquals(myXml2, list.Last()));
            }
        }
    }
}
