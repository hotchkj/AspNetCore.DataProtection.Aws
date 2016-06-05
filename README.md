# AspNetCore.DataProtection.Aws
Amazon Web Services integration for ASP.NET Core data protection.
Server keys can be stored in S3 and encrypted using KMS.

## S3 Persistence
By default, ASP.NET Core stores encryption keys locally which causes issues with key mismatches across server farms. S3 can be used instead of a shared filesystem to provide key storage.

Server-side S3 encryption of AES256 is enabled by default for all keys written to S3. It remains the client's responsibility to ensure access control to the S3 bucket is appropriately configured.

### Configuration
In Startup.cs, specified as part of DataProtection configuration:
```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddDataProtection();
    services.ConfigureDataProtection(configure =>
    {
        configure.PersistKeysToS3(new AmazonS3Client(), new S3XmlRepositoryConfig("my-bucket-name")
        // Configuration has defaults; all below are optional
        {
            // How many concurrent connections will be made to S3 to retrieve key data
            MaxS3QueryConcurrency = 10,
            // Custom prefix in the S3 bucket enabling use of folders
            KeyPrefix = "MyKeys/",
            // Customise storage class for key storage
            StorageClass = S3StorageClass.Standard,
            // Customise encryption options (these can be mutually exclusive - don't just copy & paste!)
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
            ServerSideEncryptionCustomerMethod = ServerSideEncryptionCustomerMethod.AES256,
            ServerSideEncryptionCustomerProvidedKey = "MyBase64Key",
            ServerSideEncryptionCustomerProvidedKeyMD5 = "MD5OfMyBase64Key",
            ServerSideEncryptionKeyManagementServiceKeyId = "AwsKeyManagementServiceId"
        });
    });
}
```
If the `IAmazonS3` interface is discoverable via Dependency Injection in `IServiceCollection`, the constructor argument of `AmazonS3Client` can be omitted.

## KMS Cryptography
Default options for ASP.NET data encryption are bound to certificates or Windows-specific DPAPI constructs. AWS Key Management Service keys can be used instead to provide a consistent master key for handling the server keys while stored.

Please note that `IServiceProvider`/`IServiceCollection` Dependency Injection is required for this to operate correctly, due to the need to locate & create the appropriate decryptor.

It remains the client's responsibility to correctly configure access control to the chosen KMS key, and whether their precise scenario requires master or data keys, grants, or particular encryption contexts.

### Configuration
In Startup.cs, specified as part of DataProtection configuration:
```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddDataProtection();
    services.ConfigureDataProtection(configure =>
    {
        var kmsConfig = new KmsXmlEncryptorConfig("my-application-name", "alias/MyKmsAlias");
        // Configuration has default contexts added; below are optional if using grants or additional contexts
        kmsConfig.EncryptionContext.Add("my-custom-context", "my-custom-value");
        kmsConfig.GrantTokens.Add("my-grant-token");
        configure.ProtectKeysWithAwsKms(new AmazonKeyManagementServiceClient(), kmsConfig);
    });
}
```
If the `IAmazonKeyManagementService` interface is discoverable via Dependency Injection in `IServiceCollection`, the constructor argument of `AmazonKeyManagementServiceClient` can be omitted.