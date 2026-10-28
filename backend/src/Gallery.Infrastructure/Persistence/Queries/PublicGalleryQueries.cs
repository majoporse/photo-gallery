using System.Text.Json;
using Gallery.Application.Gallery;
using Gallery.Domain.Photos;
using Microsoft.EntityFrameworkCore;

namespace Gallery.Persistence.Queries;

public sealed class PublicGalleryQueries(GalleryDbContext database) : IPublicGalleryQueries
{
    public async Task<PagedResult<CollectionSummary>> ListCollectionsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var ordered = database.Collections
            .AsNoTracking()
            .OrderBy(collection => collection.Position)
            .ThenBy(collection => collection.Id);

        var total = await ordered.CountAsync(cancellationToken);

        var rows = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(collection => new CollectionRow(
                collection.Id,
                collection.Slug,
                collection.Name,
                collection.Description,
                database.Photos.Count(photo => photo.CollectionId == collection.Id),
                database.Photos
                    .Where(photo => photo.CollectionId == collection.Id)
                    .OrderBy(photo => photo.Position)
                    .Select(photo => new PhotoRef(photo.Id, photo.OriginalSha256, photo.Width, photo.Height))
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return new PagedResult<CollectionSummary>(
            rows.Select(ToSummary).ToArray(),
            page,
            pageSize,
            total);
    }

    public async Task<CollectionSummary?> GetCollectionAsync(string slug, CancellationToken cancellationToken)
    {
        var row = await database.Collections
            .AsNoTracking()
            .Where(collection => collection.Slug == slug)
            .Select(collection => new CollectionRow(
                collection.Id,
                collection.Slug,
                collection.Name,
                collection.Description,
                database.Photos.Count(photo => photo.CollectionId == collection.Id),
                database.Photos
                    .Where(photo => photo.CollectionId == collection.Id)
                    .OrderBy(photo => photo.Position)
                    .Select(photo => new PhotoRef(photo.Id, photo.OriginalSha256, photo.Width, photo.Height))
                    .FirstOrDefault()))
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : ToSummary(row);
    }

    public async Task<PagedResult<PhotoSummary>?> ListPhotosAsync(
        string slug,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var collectionId = await database.Collections
            .AsNoTracking()
            .Where(collection => collection.Slug == slug)
            .Select(collection => collection.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (collectionId is null)
        {
            return null;
        }

        var query = database.Photos
            .AsNoTracking()
            .Where(photo => photo.CollectionId == collectionId)
            .OrderBy(photo => photo.Position);

        var total = await query.CountAsync(cancellationToken);

        var photos = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(photo => new PhotoRow(
                photo.Id,
                photo.OriginalSha256,
                photo.Width,
                photo.Height,
                photo.DominantColor,
                photo.Title,
                photo.AltText,
                photo.TakenAt,
                photo.FeaturedRank))
            .ToListAsync(cancellationToken);

        return new PagedResult<PhotoSummary>(photos.Select(ToSummary).ToArray(), page, pageSize, total);
    }

    public async Task<PhotoDetail?> GetPhotoAsync(string id, CancellationToken cancellationToken)
    {
        var photo = await database.Photos
            .AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new PhotoDetailRow(
                candidate.Id,
                candidate.OriginalSha256,
                candidate.Width,
                candidate.Height,
                candidate.DominantColor,
                candidate.Title,
                candidate.Caption,
                candidate.AltText,
                candidate.TakenAt,
                candidate.FeaturedRank,
                candidate.ExifJson,
                candidate.CollectionId,
                candidate.Position,
                database.Collections
                    .Where(collection => collection.Id == candidate.CollectionId)
                    .Select(collection => new CollectionRef(collection.Slug, collection.Name))
                    .FirstOrDefault()))
            .FirstOrDefaultAsync(cancellationToken);

        if (photo is null)
        {
            return null;
        }

        var previous = await database.Photos
            .AsNoTracking()
            .Where(candidate => candidate.CollectionId == photo.CollectionId && candidate.Position < photo.Position)
            .OrderByDescending(candidate => candidate.Position)
            .Select(candidate => candidate.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var next = await database.Photos
            .AsNoTracking()
            .Where(candidate => candidate.CollectionId == photo.CollectionId && candidate.Position > photo.Position)
            .OrderBy(candidate => candidate.Position)
            .Select(candidate => candidate.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return new PhotoDetail(
            ToSummary(new PhotoRow(
                photo.Id,
                photo.Sha256,
                photo.Width,
                photo.Height,
                photo.DominantColor,
                photo.Title,
                photo.AltText,
                photo.TakenAt,
                photo.FeaturedRank)),
            photo.Caption,
            photo.Collection?.Slug ?? string.Empty,
            photo.Collection?.Name ?? string.Empty,
            ParseExif(photo.ExifJson),
            previous,
            next);
    }

    public async Task<IReadOnlyList<PhotoSummary>> GetIntroAsync(
        int count,
        CancellationToken cancellationToken)
    {
        var photos = await database.Photos
            .AsNoTracking()
            .OrderBy(_ => EF.Functions.Random())
            .Take(count)
            .Select(photo => new PhotoRow(
                photo.Id,
                photo.OriginalSha256,
                photo.Width,
                photo.Height,
                photo.DominantColor,
                photo.Title,
                photo.AltText,
                photo.TakenAt,
                photo.FeaturedRank))
            .ToListAsync(cancellationToken);

        return photos.Select(ToSummary).ToArray();
    }

    public async Task<HomeFeed> GetHomeAsync(CancellationToken cancellationToken)
    {
        var featured = await database.Photos
            .AsNoTracking()
            .Where(photo => photo.FeaturedRank != null)
            .OrderBy(photo => photo.FeaturedRank)
            .Select(photo => new PhotoRow(
                photo.Id,
                photo.OriginalSha256,
                photo.Width,
                photo.Height,
                photo.DominantColor,
                photo.Title,
                photo.AltText,
                photo.TakenAt,
                photo.FeaturedRank))
            .ToListAsync(cancellationToken);

        var collections = await ListCollectionsAsync(page: 1, pageSize: 100, cancellationToken);

        return new HomeFeed(featured.Select(ToSummary).ToArray(), collections.Items);
    }

    private static CollectionSummary ToSummary(CollectionRow row) =>
        new(row.Id, row.Slug, row.Name, row.Description, row.PhotoCount, row.Cover);

    private static PhotoSummary ToSummary(PhotoRow row) =>
        new(
            row.Id,
            row.Sha256,
            row.Width,
            row.Height,
            DerivativeWidths.For(row.Width),
            row.DominantColor,
            row.Title,
            row.AltText,
            row.TakenAt,
            row.FeaturedRank);

    private static IReadOnlyDictionary<string, string> ParseExif(string exifJson)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(exifJson)
                ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private sealed record CollectionRow(
        string Id,
        string Slug,
        string Name,
        string? Description,
        int PhotoCount,
        PhotoRef? Cover);

    private sealed record PhotoRow(
        string Id,
        string Sha256,
        int Width,
        int Height,
        string DominantColor,
        string? Title,
        string? AltText,
        DateTimeOffset? TakenAt,
        int? FeaturedRank);

    private sealed record CollectionRef(string Slug, string Name);

    private sealed record PhotoDetailRow(
        string Id,
        string Sha256,
        int Width,
        int Height,
        string DominantColor,
        string? Title,
        string? Caption,
        string? AltText,
        DateTimeOffset? TakenAt,
        int? FeaturedRank,
        string ExifJson,
        string CollectionId,
        int Position,
        CollectionRef? Collection);
}
