using Gallery.Application.Storage;
using Gallery.Contracts.Storage;
using Gallery.Domain.Collections;
using Gallery.Persistence;
using Gallery.Infrastructure.Seeding;
using Gallery.Infrastructure.Storage;
using ImageMagick;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Gallery.Infrastructure.Tests;

[Collection(nameof(FullStackCollection))]
public sealed class DemoSeederTests(FullStackFixture fixture)
{
    [Fact]
    public async Task Seeds_a_collection_and_features_the_first_photograph()
    {
        var slug = Slug();
        await using var database = fixture.Postgres.CreateContext();
        var seeder = CreateSeeder(database, slug, count: 3, new FakeSource());

        var created = await seeder.SeedAsync(default);

        Assert.Equal(3, created);

        var collection = await database.Collections.SingleAsync(candidate => candidate.Slug == slug);
        var photos = await database.Photos
            .Where(photo => photo.CollectionId == collection.Id)
            .OrderBy(photo => photo.Position)
            .ToListAsync();

        Assert.Equal(3, photos.Count);
        Assert.Equal(0, photos[0].FeaturedRank);
        Assert.All(photos.Skip(1), photo => Assert.Null(photo.FeaturedRank));
    }

    [Fact]
    public async Task Seeding_twice_does_nothing_the_second_time()
    {
        var slug = Slug();
        await using var database = fixture.Postgres.CreateContext();
        var seeder = CreateSeeder(database, slug, count: 2, new FakeSource());

        Assert.Equal(2, await seeder.SeedAsync(default));
        Assert.Equal(0, await seeder.SeedAsync(default));

        var collection = await database.Collections.SingleAsync(candidate => candidate.Slug == slug);
        Assert.Equal(2, await database.Photos.CountAsync(photo => photo.CollectionId == collection.Id));
    }

    [Fact]
    public async Task An_empty_collection_left_by_a_failed_run_is_retried()
    {
        var slug = Slug();
        await using var database = fixture.Postgres.CreateContext();

        database.Collections.Add(Collection.Create("Demo", slug, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();

        var seeder = CreateSeeder(database, slug, count: 2, new FakeSource());

        Assert.Equal(2, await seeder.SeedAsync(default));
    }

    [Fact]
    public async Task A_network_failure_reports_zero_photographs()
    {
        var slug = Slug();
        await using var database = fixture.Postgres.CreateContext();
        var seeder = CreateSeeder(database, slug, count: 3, new UnreachableSource());

        var created = await seeder.SeedAsync(default);

        // The collection row exists before the downloads run, so this asserts the
        // seeder reports zero rather than pretending it worked.
        Assert.Equal(0, created);

        var collection = await database.Collections.SingleAsync(candidate => candidate.Slug == slug);
        Assert.False(await database.Photos.AnyAsync(photo => photo.CollectionId == collection.Id));
    }

    private DemoSeeder CreateSeeder(
        GalleryDbContext database,
        string slug,
        int count,
        IDemoPhotoSource source) =>
        new(
            database,
            new UploadPipeline(
                new S3ObjectStore(Options.Create(fixture.Seaweed.Options)),
                new MagickImageProcessor(),
                new FileSignaturesImageFormatDetector()),
            source,
            Options.Create(new DemoSeedOptions
            {
                Enabled = true,
                Count = count,
                CollectionSlug = slug,
                CollectionName = "Demo",
            }),
            TimeProvider.System,
            NullLogger<DemoSeeder>.Instance);

    private static string Slug() =>
        $"seed-{Ulid.NewUlid().ToString()[..10].ToLowerInvariant()}";

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
