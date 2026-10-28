namespace Gallery.Contracts.Storage;

public enum DerivativeFormat
{
    Avif,
    Webp,
    Jpeg,
}

public sealed record ImageMetadata(
    int Width,
    int Height,
    string Format,
    string ExifJson,
    string DominantColor);

public sealed record RenderedDerivative(int Width, DerivativeFormat Format, byte[] Bytes)
{
    public string Extension => Format switch
    {
        DerivativeFormat.Avif => $".{ImageFormats.Avif}",
        DerivativeFormat.Webp => $".{ImageFormats.Webp}",
        _ => $".{ImageFormats.Jpeg}",
    };

    public string ContentType => Format switch
    {
        DerivativeFormat.Avif => ImageFormats.AvifMediaType,
        DerivativeFormat.Webp => ImageFormats.WebpMediaType,
        _ => ImageFormats.JpegMediaType,
    };

    public string FileName => $"{Width}w{Extension}";
}

public interface IImageProcessor
{
    /// <summary>Dimensions are post-rotation: a phone photo reports how it is displayed.</summary>
    ImageMetadata ReadMetadata(Stream content);

    /// <summary>Widths above the original are skipped rather than upscaled.</summary>
    IEnumerable<RenderedDerivative> Render(
        Stream content,
        IReadOnlyList<int> widths,
        IReadOnlyList<DerivativeFormat> formats);
}
