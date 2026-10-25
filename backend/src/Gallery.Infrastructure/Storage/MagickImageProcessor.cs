using System.Text.Json;
using Gallery.Application.Storage;
using ImageMagick;

namespace Gallery.Infrastructure.Storage;

/// <summary>
/// Magick.NET rather than SkiaSharp because AVIF needs a real encoder: Skia's
/// <c>SKEncodedImageFormat.Avif</c> exists but <c>Encode</c> returns null.
/// </summary>
public sealed class MagickImageProcessor : IImageProcessor
{
    public ImageMetadata ReadMetadata(Stream content)
    {
        using var image = Load(content);

        return new ImageMetadata(
            (int)image.Width,
            (int)image.Height,
            image.Format.ToString().ToLowerInvariant(),
            SerializeExif(image),
            DominantColor(image));
    }

    public IEnumerable<RenderedDerivative> Render(
        Stream content,
        IReadOnlyList<int> widths,
        IReadOnlyList<DerivativeFormat> formats)
    {
        using var image = Load(content);

        var rendered = new List<RenderedDerivative>();

        foreach (var width in widths)
        {
            // Never upscale: serving a 2048 from an 800px original wastes bytes
            // and lies about the resolution.
            if (width > image.Width)
            {
                continue;
            }

            foreach (var format in formats)
            {
                using var derivative = image.Clone();

                derivative.Resize(new MagickGeometry((uint)width, 0));
                // Derivatives carry no metadata: the API serves EXIF as JSON, and
                // shipping GPS inside every derivative is pure weight.
                derivative.Strip();

                if (format == DerivativeFormat.Jpeg)
                {
                    // JPEG has no alpha channel; without this a transparent PNG
                    // comes out with black fringes.
                    derivative.BackgroundColor = MagickColors.White;
                    derivative.HasAlpha = false;
                }

                derivative.Format = format switch
                {
                    DerivativeFormat.Avif => MagickFormat.Avif,
                    DerivativeFormat.Webp => MagickFormat.WebP,
                    _ => MagickFormat.Jpeg,
                };
                derivative.Quality = format == DerivativeFormat.Jpeg ? 82u : 70u;

                rendered.Add(new RenderedDerivative(width, format, derivative.ToByteArray()));
            }
        }

        return rendered;
    }

    private static MagickImage Load(Stream content)
    {
        var image = new MagickImage(content);
        // Phone photos store rotation in EXIF; without this every portrait comes
        // out sideways and the reported dimensions are wrong.
        image.AutoOrient();
        return image;
    }

    private static string SerializeExif(MagickImage image)
    {
        var profile = image.GetExifProfile();
        if (profile is null)
        {
            return "{}";
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entry in profile.Values)
        {
            values[entry.Tag.ToString()] = entry.GetValue() switch
            {
                null => string.Empty,
                Array array => string.Join(',', array.Cast<object>()),
                var value => value.ToString() ?? string.Empty,
            };
        }

        return JsonSerializer.Serialize(values);
    }

    private static string DominantColor(MagickImage image)
    {
        using var scaled = image.Clone();
        scaled.Resize(new MagickGeometry(1, 1) { IgnoreAspectRatio = true });

        var color = scaled.GetPixels().GetPixel(0, 0)?.ToColor();
        return color is null ? "#000000" : $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
