using System.Security.Cryptography;
using Gallery.Domain.Uploads;

namespace Gallery.Application.Storage;

/// <summary>
/// Upload is the only operation that touches bytes: validate, ensure the
/// untouched original is stored, then render and store the derivative ladder.
/// The original either arrives in memory (seeding) or is already in the
/// bucket because the browser put it there.
/// </summary>
public sealed class UploadPipeline(
    IObjectStore store,
    IImageProcessor processor,
    IImageFormatDetector detector)
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

    public const string ImmutableCache = "public, max-age=31536000, immutable";
    public const string DerivativePrefix = "derivatives";

    // Eight simultaneous 2048px encodes would hold several hundred megabytes of
    // pixel buffers; two at a time keeps memory predictable.
    private readonly SemaphoreSlim _concurrency = new(initialCount: 2, maxCount: 2);

    /// <summary>Bytes arriving in this process; the original is written here.</summary>
    public Task<UploadResult> ProcessAsync(
        string photoId,
        string fileName,
        long contentLength,
        Stream content,
        CancellationToken cancellationToken) =>
        ProcessCoreAsync(photoId, fileName, contentLength, content, storeOriginal: true, cancellationToken);

    /// <summary>
    /// Bytes the browser already wrote to the private bucket. The object is not
    /// rewritten: the worker only hashes, validates and renders it.
    /// </summary>
    public Task<UploadResult> ProcessStoredAsync(
        string photoId,
        string fileName,
        long contentLength,
        Stream content,
        CancellationToken cancellationToken) =>
        ProcessCoreAsync(photoId, fileName, contentLength, content, storeOriginal: false, cancellationToken);

    private async Task<UploadResult> ProcessCoreAsync(
        string photoId,
        string fileName,
        long contentLength,
        Stream content,
        bool storeOriginal,
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

        var format = detector.Detect(bytes);
        if (format is null)
        {
            return UploadResult.Failed(UploadRejection.UnsupportedFormat);
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

        if ((long)metadata.Width * metadata.Height > MaxPixels)
        {
            return UploadResult.Failed(UploadRejection.DimensionsTooLarge);
        }

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var originalKey = $"{photoId}/{SafeFileName(fileName)}";

        if (storeOriginal)
        {
            using var original = new MemoryStream(bytes, writable: false);
            await store.PutAsync(
                ObjectBucket.Private,
                originalKey,
                original,
                format.MediaType,
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
        $"{DerivativePrefix}/{sha256}/{derivative.FileName}";

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
