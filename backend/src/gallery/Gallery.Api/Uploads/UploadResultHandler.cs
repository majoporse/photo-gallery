using Gallery.Application.Uploads;
using Gallery.Contracts.Uploads;
using Gallery.Domain.Photos;
using Gallery.Domain.Uploads;
using Gallery.Persistence;
using Microsoft.EntityFrameworkCore;
using Wolverine.Attributes;

namespace Gallery.Api.Uploads;

/// <summary>
/// The only place a Photo is created. The worker renders and answers; this
/// applies the answer in one transaction, so an Upload and the Photo it produced
/// can never disagree about what happened.
///
/// [WolverineHandler] is what registers it. The attribute rather than a naming
/// convention, because a rename that silently unregisters the reply path is a
/// failure with no stack trace.
///
/// The context comes from a factory rather than the container: Wolverine
/// generates this call site, and `AddDbContext`'s opaque registration is not
/// something it will resolve for you.
/// </summary>
[WolverineHandler]
public sealed class UploadResultHandler(
    IDbContextFactory<GalleryDbContext> contexts,
    TimeProvider clock)
{
    public async Task Handle(PhotoProcessed message, CancellationToken cancellationToken)
    {
        await using var database = await contexts.CreateDbContextAsync(cancellationToken);

        var upload = await database.Uploads.FirstOrDefaultAsync(
            candidate => candidate.Id == message.UploadId,
            cancellationToken);

        if (upload is null || upload.IsTerminal)
        {
            return;
        }

        var now = clock.GetUtcNow();
        var processed = message.Photo;

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        // The seam for a future delete-collection flow: it must take this same
        // row lock, or an insert can land in a collection being removed.
        await database.Database
            .SqlQueryRaw<int>(
                """SELECT 1 AS "Value" FROM "Collections" WHERE "Id" = {0} FOR UPDATE""",
                upload.CollectionId)
            .FirstOrDefaultAsync(cancellationToken);

        var alreadyInserted = await database.Photos
            .IgnoreQueryFilters()
            .AnyAsync(photo => photo.Id == upload.PhotoId, cancellationToken);

        if (!alreadyInserted)
        {
            database.Photos.Add(Photo.Create(
                upload.CollectionId,
                processed.OriginalKey,
                processed.OriginalSha256,
                processed.Width,
                processed.Height,
                processed.ExifJson,
                processed.DominantColor,
                now,
                upload.Position,
                id: upload.PhotoId));
        }

        upload.Succeed(now);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await UploadEvents.PublishAsync(database, UploadView.From(upload), cancellationToken);
    }

    public async Task Handle(UploadRejected message, CancellationToken cancellationToken)
    {
        await using var database = await contexts.CreateDbContextAsync(cancellationToken);

        var upload = await database.Uploads.FirstOrDefaultAsync(
            candidate => candidate.Id == message.UploadId,
            cancellationToken);

        if (upload is null || upload.IsTerminal)
        {
            return;
        }

        upload.Reject(ToDomain(message.Reason), clock.GetUtcNow(), message.Detail);
        await database.SaveChangesAsync(cancellationToken);

        await UploadEvents.PublishAsync(database, UploadView.From(upload), cancellationToken);
    }

    private static UploadRejection ToDomain(RejectionReason reason) => reason switch
    {
        RejectionReason.FileTooLarge => UploadRejection.FileTooLarge,
        RejectionReason.UnsupportedFormat => UploadRejection.UnsupportedFormat,
        RejectionReason.TooSmall => UploadRejection.TooSmall,
        RejectionReason.DimensionsTooLarge => UploadRejection.DimensionsTooLarge,
        RejectionReason.CorruptImage => UploadRejection.CorruptImage,
        RejectionReason.MissingBytes => UploadRejection.MissingBytes,
        _ => UploadRejection.ProcessingFailed,
    };
}
