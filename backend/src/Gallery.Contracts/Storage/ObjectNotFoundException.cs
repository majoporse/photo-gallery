namespace Gallery.Contracts.Storage;

/// <summary>
/// Thrown when the object a caller asked for is not in the bucket. Separate from
/// any other storage failure because the caller can act on it: a missing original
/// is a rejection, while an unreachable bucket is a retry.
/// </summary>
public sealed class ObjectNotFoundException(ObjectBucket bucket, string key)
    : Exception($"No object at {bucket}/{key}.")
{
    public ObjectBucket Bucket { get; } = bucket;

    public string Key { get; } = key;
}
