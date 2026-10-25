namespace Gallery.Application.Storage;

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

    Task<Stream> GetAsync(ObjectBucket bucket, string key, CancellationToken cancellationToken);
}
