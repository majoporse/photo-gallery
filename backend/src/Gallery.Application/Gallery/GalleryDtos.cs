using Gallery.Application.Storage;

namespace Gallery.Application.Gallery;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public sealed record PhotoRef(string Id, string Sha256, int Width, int Height);

public sealed record CollectionSummary(
    string Id,
    string Slug,
    string Name,
    string? Description,
    int PhotoCount,
    PhotoRef? Cover);

public sealed record PhotoSummary(
    string Id,
    string Sha256,
    int Width,
    int Height,
    IReadOnlyList<int> AvailableWidths,
    string DominantColor,
    string? Title,
    string? AltText,
    DateTimeOffset? TakenAt);

public sealed record PhotoDetail(
    PhotoSummary Photo,
    string? Caption,
    string CollectionSlug,
    string CollectionName,
    IReadOnlyDictionary<string, string> Exif,
    string? PreviousPhotoId,
    string? NextPhotoId);

public sealed record HomeFeed(IReadOnlyList<PhotoSummary> Featured, IReadOnlyList<CollectionSummary> Collections);

/// <summary>
/// The widths a client may request for this photo. Upscaling is never offered.
/// </summary>
public static class DerivativeWidths
{
    public static IReadOnlyList<int> For(int originalWidth) =>
        UploadPipeline.Ladder.Where(width => width <= originalWidth).ToArray();
}
