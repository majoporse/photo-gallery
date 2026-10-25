namespace Gallery.Api.Configuration;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string Endpoint { get; init; } = string.Empty;
    public string PublicBucket { get; init; } = "photos";
    public string PrivateBucket { get; init; } = "originals";
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public bool ForcePathStyle { get; init; } = true;
}
