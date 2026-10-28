using Gallery.Domain.Collections;
using Microsoft.EntityFrameworkCore;

namespace Gallery.Integration.Tests;

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

[Collection(nameof(PostgresCollection))]
public sealed class SchemaTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Migration_is_applied()
    {
        await using var database = fixture.CreateContext();

        var applied = await database.Database.GetAppliedMigrationsAsync();

        Assert.NotEmpty(applied);
    }

    [Fact]
    public async Task Duplicate_slug_fails_but_a_soft_deleted_one_frees_it()
    {
        await using var database = fixture.CreateContext();

        var first = Collection.Create("Iceland", "iceland", Now);
        var second = Collection.Create("Other", "iceland", Now);
        database.Collections.Add(first);
        await database.SaveChangesAsync();

        database.Collections.Add(second);
        await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());

        database.ChangeTracker.Clear();
        var stored = await database.Collections.SingleAsync(collection => collection.Slug == "iceland");
        stored.SoftDelete(Now);
        await database.SaveChangesAsync();

        database.ChangeTracker.Clear();
        database.Collections.Add(Collection.Create("Reused", "iceland", Now));
        await database.SaveChangesAsync();

        Assert.Equal(1, await database.Collections.CountAsync(collection => collection.Slug == "iceland"));
    }

    [Fact]
    public async Task Soft_deleted_rows_are_invisible_unless_filters_are_ignored()
    {
        await using var database = fixture.CreateContext();

        var collection = Collection.Create("Hidden", "hidden", Now);
        database.Collections.Add(collection);
        await database.SaveChangesAsync();

        collection.SoftDelete(Now);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();

        Assert.False(await database.Collections.AnyAsync(candidate => candidate.Id == collection.Id));
        Assert.True(await database.Collections
            .IgnoreQueryFilters()
            .AnyAsync(candidate => candidate.Id == collection.Id));
    }

    [Fact]
    public void Position_cannot_be_negative()
    {
        var collection = Collection.Create("Bad", "bad", Now);

        Assert.Throws<ArgumentOutOfRangeException>(() => collection.MoveTo(-1, Now));
    }
}
