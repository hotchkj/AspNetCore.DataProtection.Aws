# AspNetCore.DataProtection.S3
S3 integration for ASP.NET Core data protection. By default, ASP.NET Core stores encryption keys locally which causes issues with key mismatches across server farms. S3 can be used instead of a shared filesystem to provide key storage.

Server-side S3 encryption of AES256 is enabled for all keys written to S3. It remains the client's responsibility to ensure access control to the S3 bucket is appropriately configured.

## Configuration of S3 Persistence
In Startup.cs, specified as part of DataProtection configuration:
```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddDataProtection();
    services.ConfigureDataProtection(configure =>
    {
        configure.PersistKeysToS3(new AmazonS3Client(), new S3XmlRepositoryConfig("my-bucket-name"));
    });
}
```
If the `IAmazonS3` interface is discoverable via Dependency Injection in IServiceCollection, the constructor argument of `AmazonS3Client` can be omitted.