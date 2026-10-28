using Gallery.Contracts.Storage;
using Gallery.Contracts.Uploads;
using Gallery.Domain.Uploads;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Gallery.Worker.Processing;

/// <summary>
/// The worker's only handler, and the whole of its job: read the bytes the
/// browser put in the private bucket, render them, answer with a message. It
/// holds no database connection and no upload state; the API decides what the
/// answer means.
///
/// Delivery is at-least-once, so every step is safe to repeat: derivative keys
/// are content-addressed, and a rejection deletes an object whose absence is
/// itself reported as <see cref="RejectionReason.MissingBytes"/>.
/// </summary>
public sealed class ProcessUploadHandler(
    IObjectStore store,
    UploadPipeline pipeline,
    IUploadResults results)
{
    /// <summary>
    /// Transient failures retry with a cooldown; when they run out the API is
    /// told why instead of the message dead-lettering into silence. Without this
    /// the upload would sit Queued until the sweeper gave up on it, and the owner
    /// would never learn what went wrong.
    /// </summary>
    public static void Configure(HandlerChain chain) =>
        chain.OnAnyException()
            .RetryWithCooldown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15))
            .Then
            .CompensatingAction<ProcessUpload>(
                (message, exception, bus) => bus.PublishAsync(
                    new UploadRejected(message.UploadId, RejectionReason.ProcessingFailed, exception.Message)),
                InvokeResult.Stop);

    public async Task Handle(ProcessUpload message, CancellationToken cancellationToken)
    {
        try
        {
            await ProcessAsync(message, cancellationToken);
        }
        catch (ObjectNotFoundException)
        {
            // Nothing to delete and nothing to render: the bytes the API was
            // told about are not there.
            await results.PublishAsync(
                new UploadRejected(message.UploadId, RejectionReason.MissingBytes),
                cancellationToken);
        }
    }

    private async Task ProcessAsync(ProcessUpload message, CancellationToken cancellationToken)
    {
        // A presigned PUT cannot cap the size, so ask before downloading: a
        // 10 GB object must be refused without being pulled into memory.
        var length = await store.LengthAsync(ObjectBucket.Private, message.ObjectKey, cancellationToken);

        if (length > UploadPolicy.MaxBytes)
        {
            await RejectAsync(message, RejectionReason.FileTooLarge, cancellationToken);
            return;
        }

        using var content = await store.GetAsync(ObjectBucket.Private, message.ObjectKey, cancellationToken);

        var result = await pipeline.ProcessAsync(
            message.PhotoId,
            message.FileName,
            length,
            content,
            cancellationToken);

        if (!result.Succeeded)
        {
            // Validation failures are final and the bytes are provably useless.
            await RejectAsync(message, ToContract(result.Rejection!.Value), cancellationToken);
            return;
        }

        await results.PublishAsync(new PhotoProcessed(message.UploadId, result.Photo!), cancellationToken);
    }

    private async Task RejectAsync(
        ProcessUpload message,
        RejectionReason reason,
        CancellationToken cancellationToken)
    {
        await store.DeleteAsync(ObjectBucket.Private, message.ObjectKey, cancellationToken);
        await results.PublishAsync(new UploadRejected(message.UploadId, reason), cancellationToken);
    }

    private static RejectionReason ToContract(UploadRejection rejection) => rejection switch
    {
        UploadRejection.FileTooLarge => RejectionReason.FileTooLarge,
        UploadRejection.UnsupportedFormat => RejectionReason.UnsupportedFormat,
        UploadRejection.TooSmall => RejectionReason.TooSmall,
        UploadRejection.DimensionsTooLarge => RejectionReason.DimensionsTooLarge,
        UploadRejection.CorruptImage => RejectionReason.CorruptImage,
        UploadRejection.MissingBytes => RejectionReason.MissingBytes,
        _ => RejectionReason.ProcessingFailed,
    };
}
