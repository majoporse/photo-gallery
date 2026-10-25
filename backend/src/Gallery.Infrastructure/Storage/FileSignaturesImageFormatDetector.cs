using FileSignatures;
using FileSignatures.Formats;
using Gallery.Application.Storage;

namespace Gallery.Infrastructure.Storage;

/// <summary>
/// FileSignatures (MIT) supplies JPEG/PNG/WebP and the matching machinery; the
/// AVIF and HEIC variants are registered here because they live inside an ISO
/// base-media container rather than at offset zero.
/// </summary>
public sealed class FileSignaturesImageFormatDetector : IImageFormatDetector
{
    private static readonly IFileFormatInspector Inspector = new FileFormatInspector(
        FileFormatLocator.GetFormats()
            .OfType<Image>()
            .Concat<FileFormat>([new Avif(), new AvifSequence(), new Heic(), new HeicSequence(), new Heif()]));

    public DetectedImageFormat? Detect(byte[] content)
    {
        using var header = new MemoryStream(content, writable: false);
        var format = Inspector.DetermineFileFormat(header);

        return format is null ? null : new DetectedImageFormat(format.Extension, format.MediaType);
    }
}

/// <summary>
/// ISO base-media images carry the brand after <c>ftyp</c> at offset 4, so the
/// signature starts at 4 rather than at byte zero.
/// </summary>
internal abstract class IsoMediaImage(byte[] brand, string mediaType, string extension)
    : Image([.. "ftyp"u8, .. brand], mediaType, extension, 4);

internal sealed class Avif : IsoMediaImage
{
    public Avif() : base("avif"u8.ToArray(), ImageFormats.AvifMediaType, ImageFormats.Avif)
    {
    }
}

internal sealed class AvifSequence : IsoMediaImage
{
    public AvifSequence() : base("avis"u8.ToArray(), ImageFormats.AvifMediaType, ImageFormats.Avif)
    {
    }
}

internal sealed class Heic : IsoMediaImage
{
    public Heic() : base("heic"u8.ToArray(), ImageFormats.HeicMediaType, ImageFormats.Heic)
    {
    }
}

internal sealed class HeicSequence : IsoMediaImage
{
    public HeicSequence() : base("heix"u8.ToArray(), ImageFormats.HeicMediaType, ImageFormats.Heic)
    {
    }
}

/// <summary>The generic HEIF brand some cameras and phones emit.</summary>
internal sealed class Heif : IsoMediaImage
{
    public Heif() : base("mif1"u8.ToArray(), ImageFormats.HeicMediaType, ImageFormats.Heif)
    {
    }
}
