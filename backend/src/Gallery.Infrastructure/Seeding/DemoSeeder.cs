using Gallery.Application.Storage;
using Gallery.Domain.Collections;
using Gallery.Domain.Photos;
using Gallery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gallery.Infrastructure.Seeding;

/// <summary>
/// Fills an empty gallery with a handful of real photographs so the site has
/// something to show. Idempotent: if the collection exists at all, it does
/// nothing, which means a second migrate cannot double the archive.
/// </summary>
public sealed class DemoSeeder(
    GalleryDbContext database,
    UploadPipeline pipeline,
    IDemoPhotoSource source,
    IOptions<DemoSeedOptions> options,
    TimeProvider clock,
    ILogger<DemoSeeder> logger)
{
    public async Task<int> SeedAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;

        var exists = await database.Collections
            .IgnoreQueryFilters()
            .AnyAsync(collection => collection.Slug == settings.CollectionSlug, cancellationToken);

        if (exists)
        {
            logger.LogInformation(
                "Demo collection '{Slug}' already exists; not seeding.",
                settings.CollectionSlug);
            return 0;
        }

        var now = clock.GetUtcNow();
        var collection = Collection.Create(
            settings.CollectionName,
            settings.CollectionSlug,
            now,
            settings.CollectionDescription);

        database.Collections.Add(collection);
        await database.SaveChangesAsync(cancellationToken);

        var photos = new List<Photo>(settings.Count);
        var failed = 0;

        for (var index = 1; index <= settings.Count; index++)
        {
            var bytes = await source.FetchAsync(index, cancellationToken);
            if (bytes is null)
            {
                failed++;
                continue;
            }

            var id = Ulid.NewUlid().ToString();
            using var content = new MemoryStream(bytes, writable: false);

            var result = await pipeline.ProcessAsync(id, $"picsum-{index}.jpg", bytes.Length, content, cancellationToken);

            if (!result.Succeeded)
            {
                failed++;
                logger.LogWarning("Demo photograph {Index} was rejected: {Reason}.", index, result.Rejection);
                continue;
            }

            var processed = result.Photo!;
            photos.Add(Photo.Create(
                collection.Id,
                processed.OriginalKey,
                processed.OriginalSha256,
                processed.Width,
                processed.Height,
                processed.ExifJson,
                processed.DominantColor,
                now,
                position: photos.Count,
                id: id));
        }

        if (photos.Count == 0)
        {
            logger.LogWarning(
                "No demo photographs could be seeded ({Failed} failed). Is the network reachable?",
                failed);
            return 0;
        }

        photos[0].Feature(0, now);
        database.Photos.AddRange(photos);
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded {Seeded} demo photographs into '{Slug}' ({Failed} failed).",
            photos.Count,
            settings.CollectionSlug,
            failed);

        return photos.Count;
    }
}
