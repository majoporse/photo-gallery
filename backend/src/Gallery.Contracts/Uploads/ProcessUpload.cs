namespace Gallery.Contracts.Uploads;

/// <summary>
/// The worker's instruction. It carries everything the worker needs because the
/// worker has no database to look the row up in, which makes these fields
/// immutable once the message is sent.
/// </summary>
public sealed record ProcessUpload(
    string UploadId,
    string PhotoId,
    string ObjectKey,
    string FileName);
