using Gallery.Contracts.Uploads;
using Gallery.Domain.Uploads;
using Gallery.Policy;
using Gallery.Worker.Application.Storage;
using Wolverine.Attributes;

namespace Gallery.Worker.Application.Processing;

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
[WolverineHandler]
public sealed class ProcessUploadHandler(
    IObjectStore store,
    UploadPipeline pipeline,
    IUploadResults results)
{
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
