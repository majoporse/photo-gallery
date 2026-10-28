namespace Gallery.Contracts.Uploads;

/// <summary>
/// The state of an Upload, as shown to the owner, carried over the status queue
/// and parsed by the browser. Status and rejection cross the wire as their names:
/// the OpenAPI generator does not reflect enum converters, and a browser should
/// not be decoding ordinals.
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
    DateTimeOffset CreatedAt);
