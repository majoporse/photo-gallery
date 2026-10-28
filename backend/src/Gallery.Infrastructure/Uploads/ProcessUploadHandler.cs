using Gallery.Application.Storage;
using Gallery.Application.Uploads;
using Gallery.Contracts.Storage;
using Gallery.Domain.Photos;
using Gallery.Domain.Uploads;
using Gallery.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gallery.Infrastructure.Uploads;

/// <summary>
/// The worker's only handler. Delivery is at-least-once, so every step either
/// checks for prior work or is naturally idempotent; re-rendering derivatives
/// overwrites content-addressed keys and inserting the Photo is guarded by its
/// primary key.
///
/// The context comes from a factory rather than the container: Wolverine generates
/// this call site, and `AddDbContext`'s opaque registration is not something it
/// will resolve for you.
/// </summary>
public sealed class ProcessUploadHandler(
    IDbContextFactory<GalleryDbContext> contexts,
    IObjectStore store,
    UploadPipeline pipeline,
    TimeProvider clock)
{
    private DateTimeOffset Now => clock.GetUtcNow();

    public async Task Handle(ProcessUpload message, CancellationToken cancellationToken)
    {
        await using var database = await contexts.CreateDbContextAsync(cancellationToken);

        var upload = await database.Uploads.FirstOrDefaultAsync(
            candidate => candidate.Id == message.UploadId,
            cancellationToken);

        if (upload is null || upload.IsTerminal)
        {
            return;
        }

        upload.StartProcessing(Now);
        await database.SaveChangesAsync(cancellationToken);
        await NotifyAsync(database, upload, cancellationToken);

        try
        {
            await ProcessAsync(database, upload, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var exhausted = upload.RecordFailure(exception.Message, Now);

            if (exhausted)
            {
                upload.Reject(UploadRejection.ProcessingFailed, Now);
            }

            try
            {
                await database.SaveChangesAsync(CancellationToken.None);
                await NotifyAsync(database, upload, CancellationToken.None);
            }
            catch (Exception persistenceFailure)
            {
                // The failure was probably the database itself; let Wolverine
                // retry rather than pretending the row was updated.
                _ = persistenceFailure;
                throw;
            }

            if (!exhausted)
            {
                throw;
            }
        }
    }

    private async Task ProcessAsync(
        GalleryDbContext database,
        Upload upload,
        CancellationToken cancellationToken)
    {
        // A presigned PUT cannot cap the size, so ask before downloading: a
        // 10 GB object must be refused without being pulled into memory.
        var length = await store.LengthAsync(ObjectBucket.Private, upload.ObjectKey, cancellationToken);

        if (length > UploadPolicy.MaxBytes)
        {
            await store.DeleteAsync(ObjectBucket.Private, upload.ObjectKey, cancellationToken);
            upload.Reject(UploadRejection.FileTooLarge, Now);
            await database.SaveChangesAsync(cancellationToken);
            await NotifyAsync(database, upload, cancellationToken);
            return;
        }

        using var content = await store.GetAsync(ObjectBucket.Private, upload.ObjectKey, cancellationToken);

        var result = await pipeline.ProcessStoredAsync(
            upload.PhotoId,
            upload.FileName,
            length,
            content,
            cancellationToken);

        if (!result.Succeeded)
        {
            // Validation failures are final and the bytes are provably useless.
            await store.DeleteAsync(ObjectBucket.Private, upload.ObjectKey, cancellationToken);
            upload.Reject(result.Rejection!.Value, Now);
            await database.SaveChangesAsync(cancellationToken);
            await NotifyAsync(database, upload, cancellationToken);
            return;
        }

        var processed = result.Photo!;

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
                Now,
                upload.Position,
                id: upload.PhotoId));
        }

        upload.Succeed(Now);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await NotifyAsync(database, upload, cancellationToken);
    }

    private static Task NotifyAsync(
        GalleryDbContext database,
        Upload upload,
        CancellationToken cancellationToken) =>
        UploadEvents.PublishAsync(database, UploadView.From(upload), cancellationToken);
}
