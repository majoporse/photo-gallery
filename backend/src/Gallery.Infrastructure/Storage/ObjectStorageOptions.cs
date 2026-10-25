namespace Gallery.Infrastructure.Storage;

public sealed class ObjectStorageOptions
{
    public const string SectionName = "Storage";
    public const string DefaultPublicBucket = "photos";
    public const string DefaultPrivateBucket = "originals";

    public string Endpoint { get; init; } = string.Empty;
    public string PublicBucket { get; init; } = DefaultPublicBucket;
    public string PrivateBucket { get; init; } = DefaultPrivateBucket;
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public bool ForcePathStyle { get; init; } = true;
}
