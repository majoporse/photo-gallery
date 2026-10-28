using Gallery.Application.Admin;
using Gallery.Application.Uploads;
using Gallery.Contracts.Storage;
using Gallery.Contracts.Uploads;
using Gallery.Domain.Uploads;
using Gallery.Infrastructure.Storage;
using Gallery.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gallery.Api.Uploads;

public sealed class UploadCommands(
    GalleryDbContext database,
    IObjectStore store,
    IUploadQueue queue,
    IOptions<ObjectStorageOptions> storage,
    TimeProvider clock) : IUploadService
{
    private DateTimeOffset Now => clock.GetUtcNow();

    public async Task<AdminResult<PresignedUpload>> BeginAsync(
        string collectionId,
        BeginUpload command,
        CancellationToken cancellationToken)
    {
        if (command.Bytes is <= 0 or > UploadPolicy.MaxBytes)
        {
            return AdminResult<PresignedUpload>.Fail(AdminError.InvalidUpload);
        }

        var exists = await database.Collections.AnyAsync(
            collection => collection.Id == collectionId,
            cancellationToken);

        if (!exists)
        {
            return AdminResult<PresignedUpload>.Fail(AdminError.NotFound);
        }

        var photoId = Ulid.NewUlid().ToString();
        var fileName = UploadPolicy.SafeFileName(command.FileName);
        var key = $"{photoId}/{fileName}";
        var position = await PhotoPositions.ReserveAsync(database, collectionId, cancellationToken);

        var upload = Upload.Create(
            collectionId,
            photoId,
            fileName,
            key,
            command.Bytes,
            position,
            Now);

        database.Uploads.Add(upload);
        await database.SaveChangesAsync(cancellationToken);

        var lifetime = TimeSpan.FromMinutes(storage.Value.PresignMinutes);
        var url = await store.PresignPutAsync(ObjectBucket.Private, key, lifetime, cancellationToken);

        return AdminResult<PresignedUpload>.Success(
            new PresignedUpload(upload.Id, key, url, Now.Add(lifetime), position));
    }

    public async Task<AdminResult<UploadView>> CompleteAsync(
        string uploadId,
        CancellationToken cancellationToken)
    {
        var upload = await database.Uploads.FirstOrDefaultAsync(
            candidate => candidate.Id == uploadId,
            cancellationToken);

        if (upload is null)
        {
            return AdminResult<UploadView>.Fail(AdminError.NotFound);
        }

        if (upload.IsTerminal)
        {
            return AdminResult<UploadView>.Success(UploadView.From(upload));
        }

        // The client says the PUT finished; the bucket decides whether to
        // believe it. Without this check an authenticated client could enqueue
        // a key that was never written and make the worker fail slowly.
        if (upload.Status is UploadStatus.Pending)
        {
            var landed = await store.ExistsAsync(ObjectBucket.Private, upload.ObjectKey, cancellationToken);

            if (!landed)
            {
                return AdminResult<UploadView>.Fail(AdminError.NotUploaded);
            }

            upload.MarkUploaded(Now);
            await database.SaveChangesAsync(cancellationToken);
        }

        // Published before the status moves to Queued: if the process dies in
        // between, the row still says Uploaded and the sweeper re-publishes.
        // The other order would leave a Queued row with no message and no way
        // to tell it apart from one a worker is about to pick up.
        await queue.PublishAsync(
            new ProcessUpload(upload.Id, upload.PhotoId, upload.ObjectKey, upload.FileName),
            cancellationToken);

        upload.Queue(Now);
        await database.SaveChangesAsync(cancellationToken);
        await UploadEvents.PublishAsync(database, UploadView.From(upload), cancellationToken);

        return AdminResult<UploadView>.Success(UploadView.From(upload));
    }

    public async Task<IReadOnlyList<UploadView>> ListActiveAsync(CancellationToken cancellationToken)
    {
        var uploads = await database.Uploads
            .AsNoTracking()
            .Where(upload => upload.Status != UploadStatus.Succeeded
                && upload.Status != UploadStatus.Rejected
                && upload.Status != UploadStatus.Expired)
            .OrderBy(upload => upload.CreatedAt)
            .ThenBy(upload => upload.Id)
            .ToListAsync(cancellationToken);

        return uploads.Select(UploadView.From).ToList();
    }
}
