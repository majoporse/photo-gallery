using Gallery.Application.Gallery;
using Gallery.Application.Storage;

namespace Gallery.Application.Admin;

public sealed record CollectionAdminView(
    string Id,
    string Slug,
    string Name,
    string? Description,
    int Position,
    int PhotoCount,
    DateTimeOffset? DeletedAt);

public sealed record CreateCollection(string Name, string Slug, string? Description);

public sealed record UpdateCollection(string Name, string? Description, string? Slug = null);

public sealed record EditPhoto(
    string? Title,
    string? Caption,
    string? AltText,
    DateTimeOffset? TakenAt,
    int? FeaturedRank,
    string? CollectionId);

public sealed record UploadFile(string FileName, long Length, Stream Content);

public sealed record UploadFailure(string FileName, UploadRejection Reason);

public sealed record UploadBatch(IReadOnlyList<PhotoSummary> Created, IReadOnlyList<UploadFailure> Failed);

public enum AdminError
{
    NotFound,
    SlugTaken,
    InvalidSlug,
    InvalidOrder,
}

public sealed record AdminResult<T>(T? Value, AdminError? Error)
{
    public bool Ok => Error is null;

    public static AdminResult<T> Success(T value) => new(value, null);

    public static AdminResult<T> Fail(AdminError error) => new(default, error);
}
