namespace Gallery.Application.Gallery;

public interface IPublicGalleryQueries
{
    Task<PagedResult<CollectionSummary>> ListCollectionsAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<CollectionSummary?> GetCollectionAsync(string slug, CancellationToken cancellationToken);

    /// <summary>Null when the collection does not exist; an empty page when it has no photos.</summary>
    Task<PagedResult<PhotoSummary>?> ListPhotosAsync(string slug, int page, int pageSize, CancellationToken cancellationToken);

    Task<PhotoDetail?> GetPhotoAsync(string id, CancellationToken cancellationToken);

    Task<HomeFeed> GetHomeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// A random selection for the intro. Random per request, which is safe:
    /// the loader runs on the server and its result is what the client hydrates.
    /// </summary>
    Task<IReadOnlyList<PhotoSummary>> GetIntroAsync(int count, CancellationToken cancellationToken);
}
