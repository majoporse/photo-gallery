using Gallery.Contracts.Storage;
using Gallery.Domain.Collections;
using Gallery.Domain.Uploads;
using Gallery.Infrastructure.Seeding;
using Gallery.Infrastructure.Storage;
using Gallery.Persistence;
using ImageMagick;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Gallery.Integration.Tests;

[Collection(nameof(FullStackCollection))]
public sealed class DemoSeederTests(FullStackFixture fixture)
{
    [Fact]
    public async Task Stages_one_upload_per_photograph()
    {
        var slug = Slug();
        await using var database = fixture.Postgres.CreateContext();
        var harness = CreateSeeder(database, slug, count: 3, new FakeSource());

        var staged = await harness.Seeder.SeedAsync(default);

        Assert.Equal(3, staged);

        var collection = await database.Collections.SingleAsync(candidate => candidate.Slug == slug);
        var uploads = await database.Uploads
            .Where(upload => upload.CollectionId == collection.Id)
            .OrderBy(upload => upload.Position)
            .ToListAsync();

        Assert.Equal(3, uploads.Count);
        Assert.All(uploads, upload => Assert.Equal(UploadStatus.Uploaded, upload.Status));
        Assert.Equal(new[] { 0, 1, 2 }, uploads.Select(upload => upload.Position).ToArray());
        // Staged, not queued: a one-shot command cannot flush a message, so the
        // sweeper is what puts these on the queue.
        Assert.All(uploads, upload => Assert.Equal(0, upload.Attempts));

        foreach (var upload in uploads)
        {
            Assert.True(await harness.Store.ExistsAsync(ObjectBucket.Private, upload.ObjectKey, default));
        }

        // Nothing is a Photo until the worker says so.
        Assert.False(await database.Photos.AnyAsync(photo => photo.CollectionId == collection.Id));
    }

    [Fact]
    public async Task Seeding_twice_does_nothing_the_second_time()
    {
        var slug = Slug();
        await using var database = fixture.Postgres.CreateContext();
        var harness = CreateSeeder(database, slug, count: 2, new FakeSource());

        Assert.Equal(2, await harness.Seeder.SeedAsync(default));
        Assert.Equal(0, await harness.Seeder.SeedAsync(default));

        var collection = await database.Collections.SingleAsync(candidate => candidate.Slug == slug);
        Assert.Equal(2, await database.Uploads.CountAsync(upload => upload.CollectionId == collection.Id));
    }

    [Fact]
    public async Task An_empty_collection_left_by_a_failed_run_is_retried()
    {
        var slug = Slug();
        await using var database = fixture.Postgres.CreateContext();

        database.Collections.Add(Collection.Create("Demo", slug, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();

        var harness = CreateSeeder(database, slug, count: 2, new FakeSource());

        Assert.Equal(2, await harness.Seeder.SeedAsync(default));
    }

    [Fact]
    public async Task A_network_failure_reports_zero_photographs()
    {
        var slug = Slug();
        await using var database = fixture.Postgres.CreateContext();
        var harness = CreateSeeder(database, slug, count: 3, new UnreachableSource());

        var staged = await harness.Seeder.SeedAsync(default);

        // The collection row exists before the downloads run, so this asserts the
        // seeder reports zero rather than pretending it worked.
        Assert.Equal(0, staged);

        var collection = await database.Collections.SingleAsync(candidate => candidate.Slug == slug);
        Assert.False(await database.Uploads.AnyAsync(upload => upload.CollectionId == collection.Id));
    }

    private SeederHarness CreateSeeder(
        GalleryDbContext database,
        string slug,
        int count,
        IDemoPhotoSource source)
    {
        var store = new S3ObjectStore(Options.Create(fixture.Seaweed.Options));

        return new SeederHarness(
            new DemoSeeder(
                database,
                store,
                source,
                Options.Create(new DemoSeedOptions
                {
                    Enabled = true,
                    Count = count,
                    CollectionSlug = slug,
                    CollectionName = "Demo",
                }),
                TimeProvider.System,
                NullLogger<DemoSeeder>.Instance),
            store);
    }

    private static string Slug() =>
        $"seed-{Ulid.NewUlid().ToString()[..10].ToLowerInvariant()}";

    private sealed record SeederHarness(DemoSeeder Seeder, S3ObjectStore Store);

    private sealed class FakeSource : IDemoPhotoSource
    {
        public Task<byte[]?> FetchAsync(int index, CancellationToken cancellationToken)
        {
            using var image = new MagickImage(
                new MagickColor((byte)(20 * index), 60, 90),
                1600,
                1000);

            return Task.FromResult<byte[]?>(image.ToByteArray(MagickFormat.Jpeg));
        }
    }

    private sealed class UnreachableSource : IDemoPhotoSource
    {
        public Task<byte[]?> FetchAsync(int index, CancellationToken cancellationToken) =>
            Task.FromResult<byte[]?>(null);
    }
}
