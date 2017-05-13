// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Amazon.S3;
using Amazon.S3.Model;
using AspNetCore.DataProtection.Aws.S3;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Xunit;

namespace AspNetCore.DataProtection.Aws.Tests
{
    public sealed class S3XmlRespositoryTests : IDisposable
    {
        private readonly S3XmlRepository xmlRepository;
        private readonly MockRepository repository;
        private readonly Mock<IAmazonS3> s3Client;
        private readonly Mock<IS3XmlRepositoryConfig> config;
        private readonly Mock<IMockingWrapper> mockingWrapper;
        private const string ElementName = "name";
        private const string ElementContent = "test";
        private const string Bucket = "bucket";
        private const string Prefix = "prefix";
        private const string AesKey = "x+AmYqxeD//Ky4vt0HmXxSVGll7TgEkJK6iTPGqFJbk=";
        private const string AwsStandardMetadata = "x-amz-meta-";
        private const string FriendlyNameMetadata = AwsStandardMetadata + S3XmlRepository.FriendlyNameMetadata;

        public S3XmlRespositoryTests()
        {
            repository = new MockRepository(MockBehavior.Strict);
            s3Client = repository.Create<IAmazonS3>();
            config = repository.Create<IS3XmlRepositoryConfig>();
            mockingWrapper = repository.Create<IMockingWrapper>();
            xmlRepository = new S3XmlRepository(s3Client.Object, config.Object, null, mockingWrapper.Object);
        }

        public void Dispose()
        {
            repository.VerifyAll();
        }

        [Fact]
        public void ExpectAlternativeConstructor()
        {
            var altRepo = new S3XmlRepository(s3Client.Object, config.Object);

            Assert.Same(config.Object, altRepo.Config);
        }

        [Fact]
        public void ExpectStoreToSucceed()
        {
            var myXml = new XElement(ElementName, ElementContent);
            var myTestName = "friendly";

            // Response isn't queried, so can be default arguments
            var response = new PutObjectResponse();

            config.Setup(x => x.Bucket).Returns(Bucket);
            config.Setup(x => x.KeyPrefix).Returns(Prefix);
            config.Setup(x => x.StorageClass).Returns(S3StorageClass.Standard);
            config.Setup(x => x.ServerSideEncryptionMethod).Returns(ServerSideEncryptionMethod.AES256);
            config.Setup(x => x.ServerSideEncryptionCustomerMethod).Returns(ServerSideEncryptionCustomerMethod.None);
            config.Setup(x => x.ClientSideCompression).Returns(false);

            var guid = new Guid("03ffb238-1f6b-4647-963a-5ed60e83c74e");
            mockingWrapper.Setup(x => x.GetNewGuid()).Returns(guid);

            s3Client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), CancellationToken.None))
                    .ReturnsAsync(response)
                    .Callback<PutObjectRequest, CancellationToken>((pr, ct) =>
                                                                   {
                                                                       Assert.Equal(Bucket, pr.BucketName);
                                                                       Assert.Equal(ServerSideEncryptionMethod.AES256, pr.ServerSideEncryptionMethod);
                                                                       Assert.Equal(ServerSideEncryptionCustomerMethod.None, pr.ServerSideEncryptionCustomerMethod);
                                                                       Assert.Null(pr.ServerSideEncryptionCustomerProvidedKey);
                                                                       Assert.Null(pr.ServerSideEncryptionCustomerProvidedKeyMD5);
                                                                       Assert.Null(pr.ServerSideEncryptionKeyManagementServiceKeyId);
                                                                       Assert.Equal(S3StorageClass.Standard, pr.StorageClass);
                                                                       Assert.Equal(Prefix + guid + ".xml", pr.Key);
                                                                       Assert.Contains(FriendlyNameMetadata, pr.Metadata.Keys);
                                                                       Assert.Equal(myTestName, pr.Metadata[FriendlyNameMetadata]);

                                                                       var body = XElement.Load(pr.InputStream);
                                                                       Assert.True(XNode.DeepEquals(myXml, body));
                                                                   });

            xmlRepository.StoreElement(myXml, myTestName);
        }

        [Fact]
        public void ExpectKmsStoreToSucceed()
        {
            var keyId = "keyId";
            var myXml = new XElement(ElementName, ElementContent);
            var myTestName = "friendly";

            // Response isn't queried, so can be default arguments
            var response = new PutObjectResponse();

            config.Setup(x => x.Bucket).Returns(Bucket);
            config.Setup(x => x.KeyPrefix).Returns(Prefix);
            config.Setup(x => x.StorageClass).Returns(S3StorageClass.Standard);
            config.Setup(x => x.ServerSideEncryptionMethod).Returns(ServerSideEncryptionMethod.AWSKMS);
            config.Setup(x => x.ServerSideEncryptionKeyManagementServiceKeyId).Returns(keyId);
            config.Setup(x => x.ClientSideCompression).Returns(false);

            var guid = new Guid("03ffb238-1f6b-4647-963a-5ed60e83c74e");
            mockingWrapper.Setup(x => x.GetNewGuid()).Returns(guid);

            s3Client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), CancellationToken.None))
                    .ReturnsAsync(response)
                    .Callback<PutObjectRequest, CancellationToken>((pr, ct) =>
                                                                   {
                                                                       Assert.Equal(Bucket, pr.BucketName);
                                                                       Assert.Equal(ServerSideEncryptionMethod.AWSKMS, pr.ServerSideEncryptionMethod);
                                                                       Assert.Equal(ServerSideEncryptionCustomerMethod.None, pr.ServerSideEncryptionCustomerMethod);
                                                                       Assert.Null(pr.ServerSideEncryptionCustomerProvidedKey);
                                                                       Assert.Null(pr.ServerSideEncryptionCustomerProvidedKeyMD5);
                                                                       Assert.Equal(keyId, pr.ServerSideEncryptionKeyManagementServiceKeyId);
                                                                       Assert.Equal(S3StorageClass.Standard, pr.StorageClass);
                                                                       Assert.Equal(Prefix + guid + ".xml", pr.Key);
                                                                       Assert.Contains(FriendlyNameMetadata, pr.Metadata.Keys);
                                                                       Assert.Equal(myTestName, pr.Metadata[FriendlyNameMetadata]);

                                                                       var body = XElement.Load(pr.InputStream);
                                                                       Assert.True(XNode.DeepEquals(myXml, body));
                                                                   });

            xmlRepository.StoreElement(myXml, myTestName);
        }

        [Fact]
        public void ExpectCustomStoreToSucceed()
        {
            var md5 = "md5";
            var myXml = new XElement(ElementName, ElementContent);
            var myTestName = "friendly";

            // Response isn't queried, so can be default arguments
            var response = new PutObjectResponse();

            config.Setup(x => x.Bucket).Returns(Bucket);
            config.Setup(x => x.KeyPrefix).Returns(Prefix);
            config.Setup(x => x.StorageClass).Returns(S3StorageClass.Standard);
            config.Setup(x => x.ServerSideEncryptionMethod).Returns(ServerSideEncryptionMethod.None);
            config.Setup(x => x.ServerSideEncryptionCustomerMethod).Returns(ServerSideEncryptionCustomerMethod.AES256);
            config.Setup(x => x.ServerSideEncryptionCustomerProvidedKey).Returns(AesKey);
            config.Setup(x => x.ServerSideEncryptionCustomerProvidedKeyMd5).Returns(md5);
            config.Setup(x => x.ClientSideCompression).Returns(false);

            var guid = new Guid("03ffb238-1f6b-4647-963a-5ed60e83c74e");
            mockingWrapper.Setup(x => x.GetNewGuid()).Returns(guid);

            s3Client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), CancellationToken.None))
                    .ReturnsAsync(response)
                    .Callback<PutObjectRequest, CancellationToken>((pr, ct) =>
                                                                   {
                                                                       Assert.Equal(Bucket, pr.BucketName);
                                                                       Assert.Equal(ServerSideEncryptionMethod.None, pr.ServerSideEncryptionMethod);
                                                                       Assert.Equal(ServerSideEncryptionCustomerMethod.AES256, pr.ServerSideEncryptionCustomerMethod);
                                                                       Assert.Equal(AesKey, pr.ServerSideEncryptionCustomerProvidedKey);
                                                                       Assert.Equal(md5, pr.ServerSideEncryptionCustomerProvidedKeyMD5);
                                                                       Assert.Null(pr.ServerSideEncryptionKeyManagementServiceKeyId);
                                                                       Assert.Equal(S3StorageClass.Standard, pr.StorageClass);
                                                                       Assert.Equal(Prefix + guid + ".xml", pr.Key);
                                                                       Assert.Contains(FriendlyNameMetadata, pr.Metadata.Keys);
                                                                       Assert.Equal(myTestName, pr.Metadata[FriendlyNameMetadata]);

                                                                       var body = XElement.Load(pr.InputStream);
                                                                       Assert.True(XNode.DeepEquals(myXml, body));
                                                                   });

            xmlRepository.StoreElement(myXml, myTestName);
        }

        [Fact]
        public void ExpectVariedStorageClassToSucceed()
        {
            var myXml = new XElement(ElementName, ElementContent);
            var myTestName = "friendly";

            // Response isn't queried, so can be default arguments
            var response = new PutObjectResponse();

            config.Setup(x => x.Bucket).Returns(Bucket);
            config.Setup(x => x.KeyPrefix).Returns(Prefix);
            config.Setup(x => x.StorageClass).Returns(S3StorageClass.ReducedRedundancy);
            config.Setup(x => x.ServerSideEncryptionMethod).Returns(ServerSideEncryptionMethod.AES256);
            config.Setup(x => x.ServerSideEncryptionCustomerMethod).Returns(ServerSideEncryptionCustomerMethod.None);
            config.Setup(x => x.ClientSideCompression).Returns(false);

            var guid = new Guid("03ffb238-1f6b-4647-963a-5ed60e83c74e");
            mockingWrapper.Setup(x => x.GetNewGuid()).Returns(guid);

            s3Client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), CancellationToken.None))
                    .ReturnsAsync(response)
                    .Callback<PutObjectRequest, CancellationToken>((pr, ct) =>
                                                                   {
                                                                       Assert.Equal(Bucket, pr.BucketName);
                                                                       Assert.Equal(ServerSideEncryptionMethod.AES256, pr.ServerSideEncryptionMethod);
                                                                       Assert.Equal(ServerSideEncryptionCustomerMethod.None, pr.ServerSideEncryptionCustomerMethod);
                                                                       Assert.Null(pr.ServerSideEncryptionCustomerProvidedKey);
                                                                       Assert.Null(pr.ServerSideEncryptionCustomerProvidedKeyMD5);
                                                                       Assert.Null(pr.ServerSideEncryptionKeyManagementServiceKeyId);
                                                                       Assert.Equal(S3StorageClass.ReducedRedundancy, pr.StorageClass);
                                                                       Assert.Equal(Prefix + guid + ".xml", pr.Key);
                                                                       Assert.Contains(FriendlyNameMetadata, pr.Metadata.Keys);
                                                                       Assert.Equal(myTestName, pr.Metadata[FriendlyNameMetadata]);

                                                                       var body = XElement.Load(pr.InputStream);
                                                                       Assert.True(XNode.DeepEquals(myXml, body));
                                                                   });

            xmlRepository.StoreElement(myXml, myTestName);
        }

        [Fact]
        public void ExpectCompressedStoreToSucceed()
        {
            var myXml = new XElement(ElementName, ElementContent);
            var myTestName = "friendly";

            // Response isn't queried, so can be default arguments
            var response = new PutObjectResponse();

            config.Setup(x => x.Bucket).Returns(Bucket);
            config.Setup(x => x.KeyPrefix).Returns(Prefix);
            config.Setup(x => x.StorageClass).Returns(S3StorageClass.Standard);
            config.Setup(x => x.ServerSideEncryptionMethod).Returns(ServerSideEncryptionMethod.AES256);
            config.Setup(x => x.ServerSideEncryptionCustomerMethod).Returns(ServerSideEncryptionCustomerMethod.None);
            config.Setup(x => x.ClientSideCompression).Returns(true);

            var guid = new Guid("03ffb238-1f6b-4647-963a-5ed60e83c74e");
            mockingWrapper.Setup(x => x.GetNewGuid()).Returns(guid);

            s3Client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), CancellationToken.None))
                    .ReturnsAsync(response)
                    .Callback<PutObjectRequest, CancellationToken>((pr, ct) =>
                                                                   {
                                                                       Assert.Equal(Bucket, pr.BucketName);
                                                                       Assert.Equal(ServerSideEncryptionMethod.AES256, pr.ServerSideEncryptionMethod);
                                                                       Assert.Equal(ServerSideEncryptionCustomerMethod.None, pr.ServerSideEncryptionCustomerMethod);
                                                                       Assert.Null(pr.ServerSideEncryptionCustomerProvidedKey);
                                                                       Assert.Null(pr.ServerSideEncryptionCustomerProvidedKeyMD5);
                                                                       Assert.Null(pr.ServerSideEncryptionKeyManagementServiceKeyId);
                                                                       Assert.Equal(S3StorageClass.Standard, pr.StorageClass);
                                                                       Assert.Equal(Prefix + guid + ".xml", pr.Key);
                                                                       Assert.Equal("gzip", pr.Headers.ContentEncoding);
                                                                       Assert.Contains(FriendlyNameMetadata, pr.Metadata.Keys);
                                                                       Assert.Equal(myTestName, pr.Metadata[FriendlyNameMetadata]);

                                                                       var body = XElement.Load(new GZipStream(pr.InputStream, CompressionMode.Decompress));
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

            config.Setup(x => x.Bucket).Returns(Bucket);
            config.Setup(x => x.KeyPrefix).Returns(Prefix);
            config.Setup(x => x.MaxS3QueryConcurrency).Returns(10);

            s3Client.Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), CancellationToken.None))
                    .ReturnsAsync(listResponse)
                    .Callback<ListObjectsV2Request, CancellationToken>((lr, ct) =>
                                                                       {
                                                                           Assert.Equal(Bucket, lr.BucketName);
                                                                           Assert.Equal(Prefix, lr.Prefix);
                                                                           Assert.Null(lr.ContinuationToken);
                                                                       });

            IReadOnlyCollection<XElement> list = xmlRepository.GetAllElements();

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

            config.Setup(x => x.Bucket).Returns(Bucket);
            config.Setup(x => x.KeyPrefix).Returns(Prefix);
            config.Setup(x => x.MaxS3QueryConcurrency).Returns(10);
            config.Setup(x => x.ServerSideEncryptionCustomerMethod).Returns(ServerSideEncryptionCustomerMethod.None);

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
                                                                           Assert.Equal(ServerSideEncryptionCustomerMethod.None, gr.ServerSideEncryptionCustomerMethod);
                                                                           Assert.Null(gr.ServerSideEncryptionCustomerProvidedKey);
                                                                           Assert.Null(gr.ServerSideEncryptionCustomerProvidedKeyMD5);
                                                                       });

                IReadOnlyCollection<XElement> list = xmlRepository.GetAllElements();

                Assert.Equal(1, list.Count);

                Assert.True(XNode.DeepEquals(myXml, list.First()));
            }
        }

        [Fact]
        public void ExpectFolderIgnored()
        {
            var key = "folder/";
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

            config.Setup(x => x.Bucket).Returns(Bucket);
            config.Setup(x => x.KeyPrefix).Returns(Prefix);
            config.Setup(x => x.MaxS3QueryConcurrency).Returns(10);
            config.Setup(x => x.ServerSideEncryptionCustomerMethod).Returns(ServerSideEncryptionCustomerMethod.None);

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
                                                                           Assert.Equal(ServerSideEncryptionCustomerMethod.None, gr.ServerSideEncryptionCustomerMethod);
                                                                           Assert.Null(gr.ServerSideEncryptionCustomerProvidedKey);
                                                                           Assert.Null(gr.ServerSideEncryptionCustomerProvidedKeyMD5);
                                                                       });

                IReadOnlyCollection<XElement> list = xmlRepository.GetAllElements();

                Assert.Equal(0, list.Count);
            }
        }

        [Fact]
        public void ExpectSingleUncompressedCompatibleQueryToSucceed()
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

            config.Setup(x => x.Bucket).Returns(Bucket);
            config.Setup(x => x.KeyPrefix).Returns(Prefix);
            config.Setup(x => x.MaxS3QueryConcurrency).Returns(10);
            config.Setup(x => x.ServerSideEncryptionCustomerMethod).Returns(ServerSideEncryptionCustomerMethod.None);

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
                // No Content-Encoding specified
                s3Client.Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), CancellationToken.None))
                        .ReturnsAsync(getResponse)
                        .Callback<GetObjectRequest, CancellationToken>((gr, ct) =>
                                                                       {
                                                                           Assert.Equal(Bucket, gr.BucketName);
                                                                           Assert.Equal(key, gr.Key);
                                                                           Assert.Equal(ServerSideEncryptionCustomerMethod.None, gr.ServerSideEncryptionCustomerMethod);
                                                                           Assert.Null(gr.ServerSideEncryptionCustomerProvidedKey);
                                                                           Assert.Null(gr.ServerSideEncryptionCustomerProvidedKeyMD5);
                                                                       });

                IReadOnlyCollection<XElement> list = xmlRepository.GetAllElements();

                Assert.Equal(1, list.Count);

                Assert.True(XNode.DeepEquals(myXml, list.First()));
            }
        }

        [Fact]
        public void ExpectSingleCompressedCompatibleQueryToSucceed()
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

            config.Setup(x => x.Bucket).Returns(Bucket);
            config.Setup(x => x.KeyPrefix).Returns(Prefix);
            config.Setup(x => x.MaxS3QueryConcurrency).Returns(10);
            config.Setup(x => x.ServerSideEncryptionCustomerMethod).Returns(ServerSideEncryptionCustomerMethod.None);

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
                using (var inputStream = new MemoryStream())
                {
                    using (var gZippedstream = new GZipStream(inputStream, CompressionMode.Compress))
                    {
                        myXml.Save(gZippedstream);
                    }
                    byte[] inputArray = inputStream.ToArray();
                    returnedStream.Write(inputArray, 0, inputArray.Length);
                }
                returnedStream.Seek(0, SeekOrigin.Begin);

                var getResponse = new GetObjectResponse
                {
                    BucketName = Bucket,
                    ETag = etag,
                    Key = key,
                    ResponseStream = returnedStream
                };
                getResponse.Headers.ContentEncoding = "gzip";
                s3Client.Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), CancellationToken.None))
                        .ReturnsAsync(getResponse)
                        .Callback<GetObjectRequest, CancellationToken>((gr, ct) =>
                                                                       {
                                                                           Assert.Equal(Bucket, gr.BucketName);
                                                                           Assert.Equal(key, gr.Key);
                                                                           Assert.Equal(ServerSideEncryptionCustomerMethod.None, gr.ServerSideEncryptionCustomerMethod);
                                                                           Assert.Null(gr.ServerSideEncryptionCustomerProvidedKey);
                                                                           Assert.Null(gr.ServerSideEncryptionCustomerProvidedKeyMD5);
                                                                       });

                IReadOnlyCollection<XElement> list = xmlRepository.GetAllElements();

                Assert.Equal(1, list.Count);

                Assert.True(XNode.DeepEquals(myXml, list.First()));
            }
        }

        [Fact]
        public void ExpectCustomSingleQueryToSucceed()
        {
            var md5 = "md5";
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

            config.Setup(x => x.Bucket).Returns(Bucket);
            config.Setup(x => x.KeyPrefix).Returns(Prefix);
            config.Setup(x => x.MaxS3QueryConcurrency).Returns(10);
            config.Setup(x => x.ServerSideEncryptionCustomerMethod).Returns(ServerSideEncryptionCustomerMethod.AES256);
            config.Setup(x => x.ServerSideEncryptionCustomerProvidedKey).Returns(AesKey);
            config.Setup(x => x.ServerSideEncryptionCustomerProvidedKeyMd5).Returns(md5);

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
                                                                           Assert.Equal(ServerSideEncryptionCustomerMethod.AES256, gr.ServerSideEncryptionCustomerMethod);
                                                                           Assert.Equal(AesKey, gr.ServerSideEncryptionCustomerProvidedKey);
                                                                           Assert.Equal(md5, gr.ServerSideEncryptionCustomerProvidedKeyMD5);
                                                                       });

                IReadOnlyCollection<XElement> list = xmlRepository.GetAllElements();

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

            config.Setup(x => x.Bucket).Returns(Bucket);
            config.Setup(x => x.KeyPrefix).Returns(Prefix);
            config.Setup(x => x.MaxS3QueryConcurrency).Returns(10);
            config.Setup(x => x.ServerSideEncryptionCustomerMethod).Returns(ServerSideEncryptionCustomerMethod.None);

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

                IReadOnlyCollection<XElement> list = xmlRepository.GetAllElements();

                Assert.Equal(2, list.Count);

                Assert.True(XNode.DeepEquals(myXml1, list.First()));
                Assert.True(XNode.DeepEquals(myXml2, list.Last()));
            }
        }
    }
}
