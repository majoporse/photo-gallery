using Gallery.Application.Admin;
using Gallery.Application.Gallery;
using Gallery.Application.Storage;
using Gallery.Domain.Collections;
using Gallery.Domain.Photos;
using Microsoft.EntityFrameworkCore;

namespace Gallery.Infrastructure.Persistence.Commands;

/// <summary>
/// Every write the owner can perform. Ordering is the delicate part: a unique
/// index on (CollectionId, Position) means positions are rewritten in two
/// phases, never one.
/// </summary>
public sealed class AdminGallery(GalleryDbContext database, UploadPipeline uploads, TimeProvider clock) : IAdminGallery
{
    private const int TemporaryOffset = 1_000_000;

    private DateTimeOffset Now => clock.GetUtcNow();

    public async Task<IReadOnlyList<CollectionAdminView>> ListCollectionsAsync(CancellationToken cancellationToken)
    {
        var collections = await database.Collections
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OrderBy(collection => collection.Position)
            .ThenBy(collection => collection.Id)
            .Select(collection => new CollectionAdminView(
                collection.Id,
                collection.Slug,
                collection.Name,
                collection.Description,
                collection.Position,
                database.Photos.IgnoreQueryFilters().Count(photo =>
                    photo.CollectionId == collection.Id && photo.DeletedAt == null),
                collection.DeletedAt))
            .ToListAsync(cancellationToken);

        return collections;
    }

    public async Task<AdminResult<CollectionAdminView>> CreateCollectionAsync(
        CreateCollection command,
        CancellationToken cancellationToken)
    {
        if (!Collection.IsValidSlug(command.Slug))
        {
            return AdminResult<CollectionAdminView>.Fail(AdminError.InvalidSlug);
        }

        var slugTaken = await database.Collections.AnyAsync(
            collection => collection.Slug == command.Slug,
            cancellationToken);

        if (slugTaken)
        {
            return AdminResult<CollectionAdminView>.Fail(AdminError.SlugTaken);
        }

        var position = await NextCollectionPositionAsync(cancellationToken);
        var collection = Collection.Create(command.Name, command.Slug, Now, command.Description);
        collection.MoveTo(position, Now);

        database.Collections.Add(collection);
        await database.SaveChangesAsync(cancellationToken);

        return AdminResult<CollectionAdminView>.Success(ToView(collection, photoCount: 0));
    }

    public async Task<AdminResult<CollectionAdminView>> UpdateCollectionAsync(
        string id,
        UpdateCollection command,
        CancellationToken cancellationToken)
    {
        var collection = await database.Collections.FirstOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);

        if (collection is null)
        {
            return AdminResult<CollectionAdminView>.Fail(AdminError.NotFound);
        }

        collection.Rename(command.Name, command.Description, Now);
        await database.SaveChangesAsync(cancellationToken);

        var photoCount = await database.Photos.CountAsync(
            photo => photo.CollectionId == id,
            cancellationToken);

        return AdminResult<CollectionAdminView>.Success(ToView(collection, photoCount));
    }

    public async Task<bool> DeleteCollectionAsync(string id, CancellationToken cancellationToken)
    {
        var collection = await database.Collections.FirstOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);

        if (collection is null)
        {
            return false;
        }

        var now = Now;
        collection.SoftDelete(now);

        // Photos stay in the bucket; they only leave the public surface.
        await database.Photos
            .Where(photo => photo.CollectionId == id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(photo => photo.DeletedAt, now)
                    .SetProperty(photo => photo.UpdatedAt, now),
                cancellationToken);

        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AdminResult<IReadOnlyList<CollectionAdminView>>> ReorderCollectionsAsync(
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken)
    {
        var collections = await database.Collections
            .OrderBy(collection => collection.Position)
            .ToListAsync(cancellationToken);

        if (!IsPermutation(ids, collections.Select(collection => collection.Id)))
        {
            return AdminResult<IReadOnlyList<CollectionAdminView>>.Fail(AdminError.InvalidOrder);
        }

        var byId = collections.ToDictionary(collection => collection.Id);
        for (var index = 0; index < ids.Count; index++)
        {
            byId[ids[index]].MoveTo(index, Now);
        }

        await database.SaveChangesAsync(cancellationToken);

        var views = collections
            .OrderBy(collection => collection.Position)
            .Select(collection => ToView(collection, photoCount: 0))
            .ToList();

        return AdminResult<IReadOnlyList<CollectionAdminView>>.Success(views);
    }

    public async Task<AdminResult<UploadBatch>> AddPhotosAsync(
        string collectionId,
        IReadOnlyList<UploadFile> files,
        CancellationToken cancellationToken)
    {
        var collectionExists = await database.Collections.AnyAsync(
            collection => collection.Id == collectionId,
            cancellationToken);

        if (!collectionExists)
        {
            return AdminResult<UploadBatch>.Fail(AdminError.NotFound);
        }

        var position = await NextPhotoPositionAsync(collectionId, cancellationToken);

        var created = new List<PhotoSummary>();
        var failed = new List<UploadFailure>();

        foreach (var file in files)
        {
            var id = Ulid.NewUlid().ToString();
            var result = await uploads.ProcessAsync(id, file.FileName, file.Length, file.Content, cancellationToken);

            if (!result.Succeeded)
            {
                failed.Add(new UploadFailure(file.FileName, result.Rejection!.Value));
                continue;
            }

            var processed = result.Photo!;
            var photo = Photo.Create(
                collectionId,
                processed.OriginalKey,
                processed.OriginalSha256,
                processed.Width,
                processed.Height,
                processed.ExifJson,
                processed.DominantColor,
                Now,
                position++,
                id: id);

            database.Photos.Add(photo);
            await database.SaveChangesAsync(cancellationToken);

            created.Add(ToSummary(photo));
        }

        return AdminResult<UploadBatch>.Success(new UploadBatch(created, failed));
    }

    public async Task<AdminResult<PhotoSummary>> EditPhotoAsync(
        string id,
        EditPhoto command,
        CancellationToken cancellationToken)
    {
        var photo = await database.Photos.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (photo is null)
        {
            return AdminResult<PhotoSummary>.Fail(AdminError.NotFound);
        }

        if (command.CollectionId is { Length: > 0 } targetId && targetId != photo.CollectionId)
        {
            var targetExists = await database.Collections.AnyAsync(
                collection => collection.Id == targetId,
                cancellationToken);

            if (!targetExists)
            {
                return AdminResult<PhotoSummary>.Fail(AdminError.NotFound);
            }

            var previousCollection = photo.CollectionId;
            var nextPosition = await NextPhotoPositionAsync(targetId, cancellationToken);

            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

            photo.MoveTo(targetId, nextPosition, Now);
            await database.SaveChangesAsync(cancellationToken);
            await NormalisePositionsAsync(previousCollection, cancellationToken, ownTransaction: false);

            await transaction.CommitAsync(cancellationToken);
        }

        photo.Describe(command.Title, command.Caption, command.AltText, command.TakenAt, Now);
        photo.Feature(command.FeaturedRank, Now);
        await database.SaveChangesAsync(cancellationToken);

        return AdminResult<PhotoSummary>.Success(ToSummary(photo));
    }

    public async Task<bool> DeletePhotoAsync(string id, CancellationToken cancellationToken)
    {
        var photo = await database.Photos.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (photo is null)
        {
            return false;
        }

        var collectionId = photo.CollectionId;
        photo.SoftDelete(Now);
        await database.SaveChangesAsync(cancellationToken);

        await NormalisePositionsAsync(collectionId, cancellationToken);
        return true;
    }

    public async Task<AdminResult<IReadOnlyList<PhotoSummary>>> ReorderPhotosAsync(
        string collectionId,
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken)
    {
        var photos = await database.Photos
            .Where(photo => photo.CollectionId == collectionId)
            .OrderBy(photo => photo.Position)
            .ToListAsync(cancellationToken);

        if (photos.Count == 0 || !IsPermutation(ids, photos.Select(photo => photo.Id)))
        {
            return AdminResult<IReadOnlyList<PhotoSummary>>.Fail(AdminError.InvalidOrder);
        }

        var byId = photos.ToDictionary(photo => photo.Id);
        var ordered = ids.Select(id => byId[id]).ToList();

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        // Phase 1: everything moves out of the way, so no swap can collide.
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].Reposition(TemporaryOffset + index, Now);
        }

        await database.SaveChangesAsync(cancellationToken);

        // Phase 2: the real positions.
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].Reposition(index, Now);
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return AdminResult<IReadOnlyList<PhotoSummary>>.Success(ordered.Select(ToSummary).ToList());
    }

    private async Task NormalisePositionsAsync(
        string collectionId,
        CancellationToken cancellationToken,
        bool ownTransaction = true)
    {
        var photos = await database.Photos
            .Where(photo => photo.CollectionId == collectionId)
            .OrderBy(photo => photo.Position)
            .ToListAsync(cancellationToken);

        if (photos.Count == 0)
        {
            return;
        }

        if (ownTransaction)
        {
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            await RewritePositionsAsync(photos, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await RewritePositionsAsync(photos, cancellationToken);
    }

    /// <summary>
    /// Two phases: the unique index on (CollectionId, Position) makes a single
    /// pass collide as soon as two rows swap places.
    /// </summary>
    private async Task RewritePositionsAsync(List<Photo> ordered, CancellationToken cancellationToken)
    {
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].Reposition(TemporaryOffset + index, Now);
        }

        await database.SaveChangesAsync(cancellationToken);

        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].Reposition(index, Now);
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> NextCollectionPositionAsync(CancellationToken cancellationToken)
    {
        var max = await database.Collections
            .IgnoreQueryFilters()
            .MaxAsync(collection => (int?)collection.Position, cancellationToken);

        return (max ?? -1) + 1;
    }

    private async Task<int> NextPhotoPositionAsync(string collectionId, CancellationToken cancellationToken)
    {
        var max = await database.Photos
            .IgnoreQueryFilters()
            .Where(photo => photo.CollectionId == collectionId && photo.DeletedAt == null)
            .MaxAsync(photo => (int?)photo.Position, cancellationToken);

        return (max ?? -1) + 1;
    }

    private static bool IsPermutation(IReadOnlyList<string> requested, IEnumerable<string> existing)
    {
        var wanted = requested.ToHashSet(StringComparer.Ordinal);
        var have = existing.ToHashSet(StringComparer.Ordinal);

        return wanted.Count == requested.Count && wanted.SetEquals(have);
    }

    private static CollectionAdminView ToView(Collection collection, int photoCount) =>
        new(
            collection.Id,
            collection.Slug,
            collection.Name,
            collection.Description,
            collection.Position,
            photoCount,
            collection.DeletedAt);

    private static PhotoSummary ToSummary(Photo photo) =>
        new(
            photo.Id,
            photo.OriginalSha256,
            photo.Width,
            photo.Height,
            DerivativeWidths.For(photo.Width),
            photo.DominantColor,
            photo.Title,
            photo.AltText,
            photo.TakenAt,
            photo.FeaturedRank);
}
