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
    private readonly AmazonS3Client _signingClient;
    private readonly ObjectStorageOptions _options;

    public S3ObjectStore(IOptions<ObjectStorageOptions> options)
    {
        _options = options.Value;

        _client = CreateClient(_options.Endpoint);
        // Only `host` is signed, so a URL built against the browser-facing host
        // verifies even though this process talks to a service name.
        _signingClient = string.Equals(
            _options.SigningEndpoint,
            _options.Endpoint,
            StringComparison.OrdinalIgnoreCase)
            ? _client
            : CreateClient(_options.SigningEndpoint);
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

    public async Task<long> LengthAsync(ObjectBucket bucket, string key, CancellationToken cancellationToken)
    {
        var metadata = await _client.GetObjectMetadataAsync(BucketName(bucket), key, cancellationToken);
        return metadata.ContentLength;
    }

    public async Task<Stream> GetAsync(ObjectBucket bucket, string key, CancellationToken cancellationToken)
    {
        using var response = await _client.GetObjectAsync(BucketName(bucket), key, cancellationToken);

        var copy = new MemoryStream();
        await response.ResponseStream.CopyToAsync(copy, cancellationToken);
        copy.Position = 0;
        return copy;
    }

    public async Task DeleteAsync(ObjectBucket bucket, string key, CancellationToken cancellationToken) =>
        await _client.DeleteObjectAsync(
            new DeleteObjectRequest { BucketName = BucketName(bucket), Key = key },
            cancellationToken);

    public async Task<string> PresignPutAsync(
        ObjectBucket bucket,
        string key,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = BucketName(bucket),
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(lifetime),
        };

        var url = await _signingClient.GetPreSignedURLAsync(request);

        // The SDK always presigns with https. Only `host` is part of the
        // signature, so the scheme can be corrected to whatever the browser
        // must actually call — a dev stack over plain http needs this.
        var scheme = _options.SigningEndpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? "https"
            : "http";

        var separator = url.IndexOf("://", StringComparison.Ordinal);
        return separator < 0 ? url : scheme + url[separator..];
    }

    public void Dispose()
    {
        if (!ReferenceEquals(_client, _signingClient))
        {
            _signingClient.Dispose();
        }

        _client.Dispose();
    }

    private AmazonS3Client CreateClient(string endpoint) =>
        new(
            new BasicAWSCredentials(_options.AccessKey, _options.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = _options.ForcePathStyle,
                AuthenticationRegion = "us-east-1",
                UseHttp = endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
            });

    private string BucketName(ObjectBucket bucket) => bucket switch
    {
        ObjectBucket.Public => _options.PublicBucket,
        _ => _options.PrivateBucket,
    };
}
