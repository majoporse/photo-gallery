using System.Security.Cryptography;
using Gallery.Contracts.Storage;
using Gallery.Domain.Uploads;

namespace Gallery.Worker.Processing;

/// <summary>
/// Upload is the only operation that touches bytes: validate, then render and
/// store the derivative ladder from the object the browser already put in the
/// private bucket. The original is never rewritten, so the archival master is
/// exactly the bytes that were uploaded.
/// </summary>
public sealed class UploadPipeline(
    IObjectStore store,
    IImageProcessor processor,
    IImageFormatDetector detector)
{
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

        if (contentLength > UploadPolicy.MaxBytes)
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

        if (Math.Max(metadata.Width, metadata.Height) < UploadPolicy.MinLongEdge)
        {
            return UploadResult.Failed(UploadRejection.TooSmall);
        }

        if ((long)metadata.Width * metadata.Height > UploadPolicy.MaxPixels)
        {
            return UploadResult.Failed(UploadRejection.DimensionsTooLarge);
        }

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var originalKey = $"{photoId}/{UploadPolicy.SafeFileName(fileName)}";
        var derivativeKeys = new List<string>(UploadPolicy.Ladder.Count * UploadPolicy.Formats.Count);

        await _concurrency.WaitAsync(cancellationToken);
        try
        {
            using var source = new MemoryStream(bytes, writable: false);
            foreach (var derivative in processor.Render(source, UploadPolicy.Ladder, UploadPolicy.Formats))
            {
                var key = UploadPolicy.DerivativeKey(sha256, derivative);
                using var buffer = new MemoryStream(derivative.Bytes, writable: false);
                await store.PutAsync(
                    ObjectBucket.Public,
                    key,
                    buffer,
                    derivative.ContentType,
                    UploadPolicy.ImmutableCache,
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

    private static async Task<byte[]> ReadBoundedAsync(Stream content, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long total = 0;

        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            total += read;
            if (total > UploadPolicy.MaxBytes)
            {
                throw new InvalidOperationException("Upload exceeded the declared size while streaming.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
