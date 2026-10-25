using System.Security.Cryptography;

namespace Gallery.Application.Storage;

/// <summary>
/// Upload is the only operation that touches bytes, and it is synchronous with
/// the request: validate, store the untouched original, then render and store
/// the derivative ladder.
/// </summary>
public sealed class UploadPipeline(IObjectStore store, IImageProcessor processor)
{
    public static readonly IReadOnlyList<int> Ladder = [320, 640, 1280, 2048];
    public static readonly IReadOnlyList<DerivativeFormat> Formats =
        [DerivativeFormat.Avif, DerivativeFormat.Webp, DerivativeFormat.Jpeg];

    public const long MaxBytes = 50L * 1024 * 1024;
    public const int MinLongEdge = 640;
    public const string ImmutableCache = "public, max-age=31536000, immutable";

    // Eight simultaneous 2048px encodes would hold several hundred megabytes of
    // pixel buffers; two at a time keeps memory predictable.
    private readonly SemaphoreSlim _concurrency = new(initialCount: 2, maxCount: 2);

    public async Task<UploadResult> ProcessAsync(
        string photoId,
        string fileName,
        long contentLength,
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(photoId);

        if (contentLength <= 0)
        {
            return UploadResult.Failed(UploadRejection.CorruptImage);
        }

        if (contentLength > MaxBytes)
        {
            return UploadResult.Failed(UploadRejection.FileTooLarge);
        }

        var bytes = await ReadBoundedAsync(content, cancellationToken);

        var format = ImageFormatSniffer.Detect(bytes);
        if (format is null)
        {
            return UploadResult.Failed(UploadRejection.UnsupportedFormat);
        }

        if (!ExtensionMatches(fileName, format))
        {
            return UploadResult.Failed(UploadRejection.ExtensionMismatch);
        }

        ImageMetadata metadata;
        try
        {
            using var source = new MemoryStream(bytes, writable: false);
            metadata = processor.ReadMetadata(source);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return UploadResult.Failed(UploadRejection.CorruptImage);
        }

        if (Math.Max(metadata.Width, metadata.Height) < MinLongEdge)
        {
            return UploadResult.Failed(UploadRejection.TooSmall);
        }

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var originalKey = $"{photoId}/{SafeFileName(fileName)}";

        using (var original = new MemoryStream(bytes, writable: false))
        {
            await store.PutAsync(
                ObjectBucket.Private,
                originalKey,
                original,
                ContentTypeFor(format),
                cacheControl: null,
                cancellationToken);
        }

        var derivativeKeys = new List<string>(Ladder.Count * Formats.Count);

        await _concurrency.WaitAsync(cancellationToken);
        try
        {
            using var source = new MemoryStream(bytes, writable: false);
            foreach (var derivative in processor.Render(source, Ladder, Formats))
            {
                var key = DerivativeKey(sha256, derivative);
                using var buffer = new MemoryStream(derivative.Bytes, writable: false);
                await store.PutAsync(
                    ObjectBucket.Public,
                    key,
                    buffer,
                    derivative.ContentType,
                    ImmutableCache,
                    cancellationToken);
                derivativeKeys.Add(key);
            }
        }
        finally
        {
            _concurrency.Release();
        }

        return UploadResult.Success(new ProcessedPhoto(
            originalKey,
            sha256,
            metadata.Width,
            metadata.Height,
            metadata.ExifJson,
            metadata.DominantColor,
            derivativeKeys));
    }

    public static string DerivativeKey(string sha256, RenderedDerivative derivative) =>
        $"derivatives/{sha256}/{derivative.FileName}";

    private static async Task<byte[]> ReadBoundedAsync(Stream content, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long total = 0;

        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            total += read;
            if (total > MaxBytes)
            {
                throw new InvalidOperationException("Upload exceeded the declared size while streaming.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static bool ExtensionMatches(string fileName, string format)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return format switch
        {
            "jpeg" => extension is ".jpg" or ".jpeg",
            "png" => extension is ".png",
            "webp" => extension is ".webp",
            "avif" => extension is ".avif",
            "heic" => extension is ".heic" or ".heif",
            _ => false,
        };
    }

    private static string ContentTypeFor(string format) => format switch
    {
        "jpeg" => "image/jpeg",
        "png" => "image/png",
        "webp" => "image/webp",
        "avif" => "image/avif",
        _ => "application/octet-stream",
    };

    /// <summary>Keeps the name readable without letting it escape the prefix.</summary>
    private static string SafeFileName(string fileName)
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
