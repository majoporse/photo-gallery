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
