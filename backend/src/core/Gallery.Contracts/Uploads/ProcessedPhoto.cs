namespace Gallery.Contracts.Uploads;

/// <summary>Everything the API needs to create a Photo from bytes that are already stored.</summary>
public sealed record ProcessedPhoto(
    string OriginalKey,
    string OriginalSha256,
    int Width,
    int Height,
    string ExifJson,
    string DominantColor,
    IReadOnlyList<string> DerivativeKeys);
