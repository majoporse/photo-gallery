namespace Gallery.Contracts.Uploads;

/// <summary>
/// The worker's answer for an Upload it will not render. The bytes are already
/// deleted by the time this is published, so the API only records the outcome.
/// </summary>
public sealed record UploadRejected(string UploadId, RejectionReason Reason, string? Detail = null);
