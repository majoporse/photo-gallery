namespace Gallery.Contracts.Storage;

public sealed record DetectedImageFormat(string Extension, string MediaType);

/// <summary>
/// Identifies a file by its signature rather than its name, so a renamed file
/// cannot slip past validation.
/// </summary>
public interface IImageFormatDetector
{
    DetectedImageFormat? Detect(byte[] content);
}
