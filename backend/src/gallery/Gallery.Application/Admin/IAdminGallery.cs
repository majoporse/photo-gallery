using Gallery.Application.Gallery;

namespace Gallery.Application.Admin;

public interface IAdminGallery
{
    Task<IReadOnlyList<CollectionAdminView>> ListCollectionsAsync(CancellationToken cancellationToken);

    Task<AdminResult<CollectionAdminView>> CreateCollectionAsync(
        CreateCollection command,
        CancellationToken cancellationToken);

    Task<AdminResult<CollectionAdminView>> UpdateCollectionAsync(
        string id,
        UpdateCollection command,
        CancellationToken cancellationToken);

    Task<bool> DeleteCollectionAsync(string id, CancellationToken cancellationToken);

    Task<AdminResult<CollectionAdminView>> RestoreCollectionAsync(
        string id,
        CancellationToken cancellationToken);

    Task<AdminResult<IReadOnlyList<CollectionAdminView>>> ReorderCollectionsAsync(
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken);

    Task<AdminResult<PhotoSummary>> EditPhotoAsync(
        string id,
        EditPhoto command,
        CancellationToken cancellationToken);

    Task<bool> DeletePhotoAsync(string id, CancellationToken cancellationToken);

    Task<AdminResult<IReadOnlyList<PhotoSummary>>> ReorderPhotosAsync(
        string collectionId,
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken);
}
