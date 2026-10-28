using Gallery.Contracts.Storage;
using Gallery.Contracts.Uploads;
using Gallery.Domain.Collections;
using Gallery.Domain.Uploads;
using Gallery.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gallery.Infrastructure.Seeding;

/// <summary>
/// Fills an empty gallery with a handful of real photographs so the site has
/// something to show. It stages work rather than doing it: the bytes go into the
/// private bucket, an Upload row records the intent, and the worker renders it
/// exactly as it would a photograph the owner sent. Idempotent: a collection
/// that already has photographs, or uploads still in flight, is left alone.
///
/// It does not publish. A one-shot command has no running host to flush a
/// message, and a publish that silently disappears is the failure the sweeper
/// exists to repair: left as <see cref="UploadStatus.Uploaded"/>, the row is
/// picked up by the API's sweeper on its next pass.
/// </summary>
public sealed class DemoSeeder(
    GalleryDbContext database,
    IObjectStore store,
    IDemoPhotoSource source,
    IOptions<DemoSeedOptions> options,
    TimeProvider clock,
    ILogger<DemoSeeder> logger)
{
    public async Task<int> SeedAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;

        var now = clock.GetUtcNow();

        var existing = await database.Collections
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(collection => collection.Slug == settings.CollectionSlug, cancellationToken);

        Collection collection;

        if (existing is null)
        {
            collection = Collection.Create(
                settings.CollectionName,
                settings.CollectionSlug,
                now,
                settings.CollectionDescription);

            database.Collections.Add(collection);
            await database.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var photoCount = await database.Photos
                .IgnoreQueryFilters()
                .CountAsync(photo => photo.CollectionId == existing.Id && photo.DeletedAt == null, cancellationToken);

            var inFlight = await database.Uploads
                .CountAsync(
                    upload => upload.CollectionId == existing.Id
                        && upload.Status != UploadStatus.Succeeded
                        && upload.Status != UploadStatus.Rejected
                        && upload.Status != UploadStatus.Expired,
                    cancellationToken);

            if (photoCount > 0 || inFlight > 0)
            {
                logger.LogInformation(
                    "Demo collection '{Slug}' already has {Photos} photographs and {Uploads} uploads in flight; not seeding.",
                    settings.CollectionSlug,
                    photoCount,
                    inFlight);
                return 0;
            }

            // A previous run failed before staging anything. Retry rather than
            // treating the empty shell as "done" forever.
            collection = existing;
            logger.LogInformation(
                "Demo collection '{Slug}' exists but is empty; continuing to seed.",
                settings.CollectionSlug);
        }

        var staged = 0;
        var failed = 0;

        for (var index = 1; index <= settings.Count; index++)
        {
            var bytes = await source.FetchAsync(index, cancellationToken);
            if (bytes is null)
            {
                failed++;
                continue;
            }

            var photoId = Ulid.NewUlid().ToString();
            var fileName = UploadPolicy.SafeFileName($"picsum-{index}.jpg");
            var key = $"{photoId}/{fileName}";
            var position = await PhotoPositions.ReserveAsync(database, collection.Id, cancellationToken);

            var upload = Upload.Create(
                collection.Id,
                photoId,
                fileName,
                key,
                bytes.Length,
                position,
                now);

            database.Uploads.Add(upload);
            await database.SaveChangesAsync(cancellationToken);

            try
            {
                using var content = new MemoryStream(bytes, writable: false);
                await store.PutAsync(
                    ObjectBucket.Private,
                    key,
                    content,
                    ImageFormats.JpegMediaType,
                    cacheControl: null,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A staged row whose bytes never landed would show up as an
                // upload the owner never started; drop it and let a retry take
                // the next position.
                database.Uploads.Remove(upload);
                await database.SaveChangesAsync(cancellationToken);
                failed++;
                logger.LogWarning(
                    exception,
                    "Demo photograph {Index} could not be stored. If this is 'Access Denied', Storage__AccessKey/Storage__SecretKey do not match the S3 identity.",
                    index);
                continue;
            }

            upload.MarkUploaded(now);
            await database.SaveChangesAsync(cancellationToken);

            staged++;
        }

        if (staged == 0)
        {
            logger.LogWarning(
                "No demo photographs could be staged ({Failed} failed). Is the network reachable?",
                failed);
            return 0;
        }

        logger.LogInformation(
            "Staged {Staged} demo uploads into '{Slug}' ({Failed} failed); the sweeper queues them once the API is running.",
            staged,
            settings.CollectionSlug,
            failed);

        return staged;
    }
}
