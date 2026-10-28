namespace Gallery.Worker.Infrastructure.Storage;

public sealed class ObjectStorageOptions
{
    public const string SectionName = "Storage";
    public const string DefaultPublicBucket = "photos";
    public const string DefaultPrivateBucket = "originals";

    /// <summary>The address this process uses; inside the cluster it is the service name.</summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>
    /// The address a browser uses. Presigned URLs must carry the host the
    /// browser will actually call, because that host is part of the signature.
    /// Empty means the process endpoint is also the public one.
    /// </summary>
    public string PublicEndpoint { get; init; } = string.Empty;

    public string PublicBucket { get; init; } = DefaultPublicBucket;
    public string PrivateBucket { get; init; } = DefaultPrivateBucket;
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public bool ForcePathStyle { get; init; } = true;

    /// <summary>Long enough for a slow connection, short enough to be worth stealing.</summary>
    public int PresignMinutes { get; init; } = 15;

    public string SigningEndpoint =>
        string.IsNullOrWhiteSpace(PublicEndpoint) ? Endpoint : PublicEndpoint;
}
