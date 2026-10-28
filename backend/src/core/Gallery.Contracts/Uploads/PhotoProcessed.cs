namespace Gallery.Contracts.Uploads;

/// <summary>The worker's answer for an Upload whose bytes were rendered.</summary>
public sealed record PhotoProcessed(string UploadId, ProcessedPhoto Photo);
