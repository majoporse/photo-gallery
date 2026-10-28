namespace Gallery.Worker.Application.Storage;

public interface IObjectStore
{
    Task PutAsync(
        ObjectBucket bucket,
        string key,
        Stream content,
        string contentType,
        string? cacheControl,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(ObjectBucket bucket, string key, CancellationToken cancellationToken);

    /// <summary>The stored object's size, without transferring it.</summary>
    Task<long> LengthAsync(ObjectBucket bucket, string key, CancellationToken cancellationToken);

    Task<Stream> GetAsync(ObjectBucket bucket, string key, CancellationToken cancellationToken);

    Task DeleteAsync(ObjectBucket bucket, string key, CancellationToken cancellationToken);

    /// <summary>
    /// A single-use PUT signed against the public endpoint, so a browser can
    /// write the object without the API carrying the bytes.
    /// </summary>
    Task<string> PresignPutAsync(
        ObjectBucket bucket,
        string key,
        TimeSpan lifetime,
        CancellationToken cancellationToken);
}
