namespace Gallery.Policy;

/// <summary>
/// What an acceptable photograph is and what a derivative is, in one place. The
/// API advertises these numbers and the worker enforces them, and they live in a
/// shared package because drift means the API offers widths the worker never
/// rendered.
/// </summary>
public static class UploadPolicy
{
    public static readonly IReadOnlyList<int> Ladder = [320, 640, 1280, 2048];
    public static readonly IReadOnlyList<DerivativeFormat> Formats =
        [DerivativeFormat.Avif, DerivativeFormat.Webp, DerivativeFormat.Jpeg];

    public const long MaxBytes = 50L * 1024 * 1024;
    public const int MinLongEdge = 640;

    /// <summary>
    /// A pixel ceiling, not a format rule: widening the accepted formats means a
    /// compressed 50 MB TIFF can decode into gigabytes of pixels, and the guard
    /// has to sit after decode where the real dimensions are known.
    /// </summary>
    public const long MaxPixels = 120_000_000;

    /// <summary>Keeps the name readable without letting it escape the prefix.</summary>
    public static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName).Trim();
        var cleaned = new string(name
            .Select(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_'
                ? character
                : '-')
            .ToArray())
            .Trim('-', '.');

        if (cleaned.Length == 0)
        {
            return "original";
        }

        return cleaned.Length <= 120 ? cleaned : cleaned[..120];
    }
}
