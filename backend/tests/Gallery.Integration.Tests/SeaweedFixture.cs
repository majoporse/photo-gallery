using Amazon.Runtime;
using Amazon.S3;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Gallery.Contracts.Storage;
using Gallery.Infrastructure.Storage;

namespace Gallery.Integration.Tests;

/// <summary>
/// Real SeaweedFS via `weed mini`: same engine as production, so presigned
/// behaviour, cache headers and multipart are not simulated.
/// </summary>
public sealed class SeaweedFixture : IAsyncLifetime
{
    private const string AccessKey = "test-access";
    private const string SecretKey = "test-secret";

    private readonly IContainer _container = new ContainerBuilder("chrislusf/seaweedfs:4.50")
        .WithCommand("mini", "-dir=/data")
        .WithPortBinding(8333, true)
        .Build();

    public string Endpoint => $"http://{_container.Hostname}:{_container.GetMappedPublicPort(8333)}";

    public ObjectStorageOptions Options => new()
    {
        Endpoint = Endpoint,
        PublicBucket = ObjectStorageOptions.DefaultPublicBucket,
        PrivateBucket = ObjectStorageOptions.DefaultPrivateBucket,
        AccessKey = AccessKey,
        SecretKey = SecretKey,
        ForcePathStyle = true,
    };

    public IAmazonS3 CreateClient() =>
        new AmazonS3Client(
            new BasicAWSCredentials(AccessKey, SecretKey),
            new AmazonS3Config
            {
                ServiceURL = Endpoint,
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
                UseHttp = true,
            });

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        using var client = CreateClient();
        await CreateBucketWhenReadyAsync(client, Options.PublicBucket);
        await CreateBucketWhenReadyAsync(client, Options.PrivateBucket);
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    private static async Task CreateBucketWhenReadyAsync(IAmazonS3 client, string bucket)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);

        while (true)
        {
            try
            {
                await client.PutBucketAsync(bucket);
                return;
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException && DateTimeOffset.UtcNow < deadline)
            {
                // The S3 gateway starts after master/volume, so the first
                // attempts can find a closed or half-ready listener. Any
                // transport error here means "not yet", not "broken".
                await Task.Delay(500);
            }
        }
    }
}
