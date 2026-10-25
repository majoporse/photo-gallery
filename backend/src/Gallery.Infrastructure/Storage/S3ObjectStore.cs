using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Gallery.Application.Storage;
using Microsoft.Extensions.Options;

namespace Gallery.Infrastructure.Storage;

public sealed class S3ObjectStore : IObjectStore, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly ObjectStorageOptions _options;

    public S3ObjectStore(IOptions<ObjectStorageOptions> options)
    {
        _options = options.Value;

        _client = new AmazonS3Client(
            new BasicAWSCredentials(_options.AccessKey, _options.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = _options.Endpoint,
                ForcePathStyle = _options.ForcePathStyle,
                AuthenticationRegion = "us-east-1",
                UseHttp = _options.Endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
            });
    }

    public async Task PutAsync(
        ObjectBucket bucket,
        string key,
        Stream content,
        string contentType,
        string? cacheControl,
        CancellationToken cancellationToken)
    {
        var request = new PutObjectRequest
        {
            BucketName = BucketName(bucket),
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
        };

        if (cacheControl is not null)
        {
            request.Headers.CacheControl = cacheControl;
        }

        await _client.PutObjectAsync(request, cancellationToken);
    }

    public async Task<bool> ExistsAsync(ObjectBucket bucket, string key, CancellationToken cancellationToken)
    {
        try
        {
            await _client.GetObjectMetadataAsync(BucketName(bucket), key, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<Stream> GetAsync(ObjectBucket bucket, string key, CancellationToken cancellationToken)
    {
        using var response = await _client.GetObjectAsync(BucketName(bucket), key, cancellationToken);

        var copy = new MemoryStream();
        await response.ResponseStream.CopyToAsync(copy, cancellationToken);
        copy.Position = 0;
        return copy;
    }

    public void Dispose() => _client.Dispose();

    private string BucketName(ObjectBucket bucket) => bucket switch
    {
        ObjectBucket.Public => _options.PublicBucket,
        _ => _options.PrivateBucket,
    };
}
