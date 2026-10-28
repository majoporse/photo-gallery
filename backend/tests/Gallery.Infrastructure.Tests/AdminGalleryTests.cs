using Gallery.Application.Admin;
using Gallery.Application.Storage;
using Gallery.Domain.Collections;
using Gallery.Domain.Photos;
using Gallery.Infrastructure.Persistence.Commands;
using Gallery.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace Gallery.Infrastructure.Tests;

[Collection(nameof(PostgresCollection))]
public sealed class AdminGalleryTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Created_collections_append_to_the_end()
    {
        await using var database = fixture.CreateContext();
        var admin = Create(database);

        var first = await admin.CreateCollectionAsync(new CreateCollection("First", Slug("first"), null), default);
        var second = await admin.CreateCollectionAsync(new CreateCollection("Second", Slug("second"), null), default);

        Assert.True(first.Ok);
        Assert.True(second.Ok);
        Assert.True(second.Value!.Position > first.Value!.Position);
    }

    [Fact]
    public async Task A_taken_slug_is_a_conflict_not_an_exception()
    {
        await using var database = fixture.CreateContext();
        var admin = Create(database);
        var slug = Slug("clash");

        await admin.CreateCollectionAsync(new CreateCollection("One", slug, null), default);
        var duplicate = await admin.CreateCollectionAsync(new CreateCollection("Two", slug, null), default);

        Assert.False(duplicate.Ok);
        Assert.Equal(AdminError.SlugTaken, duplicate.Error);
    }

    [Fact]
    public async Task Reordering_collections_rewrites_positions_contiguously()
    {
        await using var database = fixture.CreateContext();
        var admin = Create(database);

        var seeded = new List<string>();
        foreach (var index in Enumerable.Range(0, 3))
        {
            var created = await admin.CreateCollectionAsync(
                new CreateCollection($"C{index}", Slug($"reorder{index}"), null), default);
            seeded.Add(created.Value!.Id);
        }

        var all = await database.Collections
            .OrderBy(collection => collection.Position)
            .Select(collection => collection.Id)
            .ToListAsync();

        var reordered = all.AsEnumerable().Reverse().ToList();
        var result = await admin.ReorderCollectionsAsync(reordered, default);

        Assert.True(result.Ok);

        await using var read = fixture.CreateContext();
        var positions = await read.Collections
            .OrderBy(collection => collection.Position)
            .Select(collection => collection.Id)
            .ToListAsync();

        Assert.Equal(reordered, positions);
    }

    [Fact]
    public async Task Reordering_photos_does_not_trip_the_unique_position_index()
    {
        await using var database = fixture.CreateContext();
        var (collection, photos) = await SeedAsync(database, "reverse", 3);
        var admin = Create(database);

        var reversed = photos.Select(photo => photo.Id).Reverse().ToList();
        var result = await admin.ReorderPhotosAsync(collection.Id, reversed, default);

        Assert.True(result.Ok);

        await using var read = fixture.CreateContext();
        var stored = await read.Photos
            .Where(photo => photo.CollectionId == collection.Id)
            .OrderBy(photo => photo.Position)
            .Select(photo => photo.Id)
            .ToListAsync();

        Assert.Equal(reversed, stored);
        Assert.Equal(new[] { 0, 1, 2 }, await read.Photos
            .Where(photo => photo.CollectionId == collection.Id)
            .OrderBy(photo => photo.Position)
            .Select(photo => photo.Position)
            .ToArrayAsync());
    }

    [Fact]
    public async Task An_incomplete_order_is_rejected()
    {
        await using var database = fixture.CreateContext();
        var (collection, photos) = await SeedAsync(database, "partial", 3);
        var admin = Create(database);

        var result = await admin.ReorderPhotosAsync(collection.Id, [photos[0].Id], default);

        Assert.False(result.Ok);
        Assert.Equal(AdminError.InvalidOrder, result.Error);
    }

    [Fact]
    public async Task Moving_a_photo_closes_the_gap_it_left()
    {
        await using var database = fixture.CreateContext();
        var (source, photos) = await SeedAsync(database, "source", 3);
        var target = Collection.Create("Target", Slug("target"), Now);
        database.Collections.Add(target);
        await database.SaveChangesAsync();

        var admin = Create(database);
        var moved = await admin.EditPhotoAsync(photos[0].Id, new EditPhoto(null, null, null, null, null, target.Id), default);

        Assert.True(moved.Ok);

        await using var read = fixture.CreateContext();
        var remaining = await read.Photos
            .Where(photo => photo.CollectionId == source.Id)
            .OrderBy(photo => photo.Position)
            .Select(photo => photo.Position)
            .ToListAsync();

        Assert.Equal(new[] { 0, 1 }, remaining);
    }

    [Fact]
    public async Task Deleting_a_photo_closes_the_gap()
    {
        await using var database = fixture.CreateContext();
        var (collection, photos) = await SeedAsync(database, "gap", 3);
        var admin = Create(database);

        Assert.True(await admin.DeletePhotoAsync(photos[1].Id, default));

        await using var read = fixture.CreateContext();
        var positions = await read.Photos
            .Where(photo => photo.CollectionId == collection.Id)
            .OrderBy(photo => photo.Position)
            .Select(photo => photo.Position)
            .ToListAsync();

        Assert.Equal(new[] { 0, 1 }, positions);
    }

    [Fact]
    public async Task Deleting_a_collection_hides_its_photos_without_touching_them()
    {
        await using var database = fixture.CreateContext();
        var (collection, photos) = await SeedAsync(database, "cascade", 2);
        var admin = Create(database);

        Assert.True(await admin.DeleteCollectionAsync(collection.Id, default));

        await using var read = fixture.CreateContext();
        Assert.Empty(await read.Photos.Where(photo => photo.CollectionId == collection.Id).ToListAsync());
        Assert.Equal(
            2,
            await read.Photos.IgnoreQueryFilters().CountAsync(photo => photo.CollectionId == collection.Id));
        Assert.All(photos, photo => Assert.True(
            read.Photos.IgnoreQueryFilters().Any(candidate => candidate.Id == photo.Id)));
    }

    [Fact]
    public async Task Restoring_a_collection_brings_back_the_photos_it_hid()
    {
        await using var database = fixture.CreateContext();
        var (collection, photos) = await SeedAsync(database, "restore", 2);
        var clock = new TestTimeProvider(Now);
        var admin = Create(database, clock);

        var other = await admin.CreateCollectionAsync(
            new CreateCollection("Other", Slug("other"), null), default);
        Assert.True(other.Ok);

        Assert.True(await admin.DeleteCollectionAsync(collection.Id, default));
        clock.Advance(TimeSpan.FromMinutes(1));

        var hiddenView = (await admin.ListCollectionsAsync(default))
            .Single(candidate => candidate.Id == collection.Id);
        Assert.Equal(2, hiddenView.PhotoCount);

        var restored = await admin.RestoreCollectionAsync(collection.Id, default);

        Assert.True(restored.Ok);
        Assert.Null(restored.Value!.DeletedAt);
        Assert.Equal(2, restored.Value.PhotoCount);
        Assert.True(restored.Value.Position > other.Value!.Position);

        await using var read = fixture.CreateContext();
        Assert.NotNull(await read.Collections.SingleOrDefaultAsync(candidate => candidate.Id == collection.Id));
        var visible = await read.Photos
            .Where(photo => photo.CollectionId == collection.Id)
            .OrderBy(photo => photo.Position)
            .Select(photo => photo.Id)
            .ToListAsync();
        Assert.Equal(photos.Select(photo => photo.Id), visible);
    }

    [Fact]
    public async Task Restoring_a_collection_does_not_resurrect_photos_hidden_before_it()
    {
        await using var database = fixture.CreateContext();
        var (collection, photos) = await SeedAsync(database, "restore-individual", 2);
        var clock = new TestTimeProvider(Now);
        var admin = Create(database, clock);

        Assert.True(await admin.DeletePhotoAsync(photos[0].Id, default));
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await admin.DeleteCollectionAsync(collection.Id, default));
        clock.Advance(TimeSpan.FromMinutes(1));

        var hiddenView = (await admin.ListCollectionsAsync(default))
            .Single(candidate => candidate.Id == collection.Id);
        Assert.Equal(1, hiddenView.PhotoCount);

        var restored = await admin.RestoreCollectionAsync(collection.Id, default);

        Assert.True(restored.Ok);
        Assert.Equal(1, restored.Value!.PhotoCount);

        await using var read = fixture.CreateContext();
        var hiddenPhoto = await read.Photos
            .IgnoreQueryFilters()
            .SingleAsync(photo => photo.Id == photos[0].Id);
        Assert.NotNull(hiddenPhoto.DeletedAt);

        var visible = await read.Photos
            .Where(photo => photo.CollectionId == collection.Id)
            .Select(photo => photo.Id)
            .ToListAsync();
        Assert.Equal(new[] { photos[1].Id }, visible);
    }

    [Fact]
    public async Task Restoring_a_collection_whose_slug_was_reused_is_a_conflict()
    {
        await using var database = fixture.CreateContext();
        var (collection, _) = await SeedAsync(database, "restore-slug", 1);
        var admin = Create(database);

        Assert.True(await admin.DeleteCollectionAsync(collection.Id, default));

        var replacement = await admin.CreateCollectionAsync(
            new CreateCollection("Replacement", collection.Slug, null), default);
        Assert.True(replacement.Ok);

        var restored = await admin.RestoreCollectionAsync(collection.Id, default);

        Assert.False(restored.Ok);
        Assert.Equal(AdminError.SlugTaken, restored.Error);

        await using var read = fixture.CreateContext();
        var hidden = await read.Collections
            .IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.Id == collection.Id);
        Assert.NotNull(hidden.DeletedAt);
        Assert.NotNull(await read.Collections.SingleOrDefaultAsync(
            candidate => candidate.Id == replacement.Value!.Id));
    }

    [Fact]
    public async Task Restoring_a_live_collection_is_a_no_op()
    {
        await using var database = fixture.CreateContext();
        var (collection, _) = await SeedAsync(database, "restore-live", 1);
        var admin = Create(database);

        var restored = await admin.RestoreCollectionAsync(collection.Id, default);

        Assert.True(restored.Ok);
        Assert.Null(restored.Value!.DeletedAt);
        Assert.Equal(collection.Position, restored.Value.Position);
    }

    private static AdminGallery Create(Persistence.GalleryDbContext database, TimeProvider? clock = null) =>
        new(database, clock ?? TimeProvider.System);

    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private static async Task<(Collection Collection, List<Photo> Photos)> SeedAsync(
        Persistence.GalleryDbContext database,
        string prefix,
        int count)
    {
        var collection = Collection.Create(prefix, Slug(prefix), Now);
        database.Collections.Add(collection);

        var photos = Enumerable.Range(0, count)
            .Select(position => Photo.Create(
                collection.Id,
                $"originals/{prefix}/{position}.jpg",
                Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes($"{prefix}-{position}"))),
                1600,
                1000,
                "{}",
                "#223344",
                Now,
                position))
            .ToList();

        database.Photos.AddRange(photos);
        await database.SaveChangesAsync();

        return (collection, photos);
    }

    private static string Slug(string prefix) =>
        $"{prefix}-{Ulid.NewUlid().ToString()[..10].ToLowerInvariant()}";
}
