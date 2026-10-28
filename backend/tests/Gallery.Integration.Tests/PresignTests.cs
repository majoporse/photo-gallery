using Gallery.Contracts.Storage;
using Gallery.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace Gallery.Integration.Tests;

/// <summary>
/// Presigning is offline, so these run without a container. The scheme is the
/// point: the SDK always emits https, but a browser on a plain-http dev stack
/// cannot follow it.
/// </summary>
public sealed class PresignTests
{
    [Fact]
    public async Task A_plain_http_endpoint_produces_a_plain_http_url()
    {
        var store = Create("http://localhost:8333");

        var url = await store.PresignPutAsync(
            ObjectBucket.Private,
            "01J0000000000000000000000/one.jpg",
            TimeSpan.FromMinutes(15),
            default);

        Assert.StartsWith("http://localhost:8333/originals/01J0000000000000000000000/one.jpg?", url);
        Assert.Contains("X-Amz-Signature=", url);
        Assert.Contains("X-Amz-Expires=900", url);
    }

    [Fact]
    public async Task An_https_endpoint_produces_an_https_url()
    {
        var store = Create("https://img.example.com");

        var url = await store.PresignPutAsync(
            ObjectBucket.Private,
            "01J0000000000000000000000/one.jpg",
            TimeSpan.FromMinutes(15),
            default);

        Assert.StartsWith("https://img.example.com/originals/01J0000000000000000000000/one.jpg?", url);
    }

    private static S3ObjectStore Create(string publicEndpoint) =>
        new(Options.Create(new ObjectStorageOptions
        {
            Endpoint = "http://seaweedfs:8333",
            PublicEndpoint = publicEndpoint,
            AccessKey = "dev-access-key",
            SecretKey = "dev-secret-key",
            PublicBucket = "photos",
            PrivateBucket = "originals",
            ForcePathStyle = true,
        }));
}
