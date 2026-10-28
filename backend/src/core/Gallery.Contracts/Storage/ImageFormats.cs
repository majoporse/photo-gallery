namespace Gallery.Contracts.Storage;

/// <summary>
/// The only image formats this application accepts, named once. Extensions and
/// media types are used by detection, policy and encoding; keeping them in one
/// place is what stops "avif" and "image/avif" from drifting apart.
/// </summary>
public static class ImageFormats
{
    public const string Jpeg = "jpg";
    public const string Png = "png";
    public const string Webp = "webp";
    public const string Avif = "avif";
    public const string Heic = "heic";
    public const string Heif = "heif";

    public const string JpegMediaType = "image/jpeg";
    public const string PngMediaType = "image/png";
    public const string WebpMediaType = "image/webp";
    public const string AvifMediaType = "image/avif";
    public const string HeicMediaType = "image/heic";
}
