using Gallery.Domain.Uploads;

namespace Gallery.Application.Uploads;

/// <summary>What the client intends to send. The size is a claim, not a fact.</summary>
public sealed record BeginUpload(string FileName, long Bytes);

/// <summary>
/// A destination the browser may write to once, plus the position the Photo will
/// take if processing succeeds.
/// </summary>
public sealed record PresignedUpload(
    string UploadId,
    string Key,
    string Url,
    DateTimeOffset ExpiresAt,
    int Position);

/// <summary>
/// The state of an Upload, as shown to the owner and pushed over SSE. Status
/// and rejection cross the wire as their names: the OpenAPI generator does not
/// reflect enum converters, and a browser should not be decoding ordinals.
/// </summary>
public sealed record UploadView(
    string Id,
    string CollectionId,
    string FileName,
    int Position,
    string Status,
    string? Rejection,
    string? Failure,
    string? PhotoId,
    DateTimeOffset CreatedAt)
{
    public static UploadView From(Upload upload) =>
        new(
            upload.Id,
            upload.CollectionId,
            upload.FileName,
            upload.Position,
            upload.Status.ToString(),
            upload.Rejection?.ToString(),
            upload.Failure,
            upload.Status == UploadStatus.Succeeded ? upload.PhotoId : null,
            upload.CreatedAt);
}
