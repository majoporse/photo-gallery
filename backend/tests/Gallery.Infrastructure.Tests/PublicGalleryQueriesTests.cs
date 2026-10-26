using Gallery.Domain.Collections;
using Gallery.Domain.Photos;
using Gallery.Infrastructure.Persistence.Queries;
using Gallery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gallery.Infrastructure.Tests;

[Collection(nameof(PostgresCollection))]
public sealed class PublicGalleryQueriesTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Collections_carry_a_photo_count_and_cover_in_position_order()
    {
        var slug = NewSlug("trip");
        await using var database = fixture.CreateContext();

        var collection = Collection.Create("Trip", slug, Now);
        database.Collections.Add(collection);

        var first = NewPhoto(collection.Id, position: 0, width: 1600);
        var second = NewPhoto(collection.Id, position: 1, width: 900);
        database.Photos.AddRange(first, second);
        await database.SaveChangesAsync();

        await using var read = fixture.CreateContext();
        var queries = new PublicGalleryQueries(read);

        var page = await queries.ListCollectionsAsync(page: 1, pageSize: 20, default);
        var summary = page.Items.Single(candidate => candidate.Slug == slug);

        Assert.Equal(2, summary.PhotoCount);
        Assert.Equal(first.Id, summary.Cover!.Id);
    }

    [Fact]
    public async Task Photos_paginate_in_position_order_without_upscale_widths()
    {
        var slug = NewSlug("paged");
        await using var database = fixture.CreateContext();

        var collection = Collection.Create("Paged", slug, Now);
        database.Collections.Add(collection);
        database.Photos.AddRange(
            NewPhoto(collection.Id, position: 0, width: 1600),
            NewPhoto(collection.Id, position: 1, width: 800),
            NewPhoto(collection.Id, position: 2, width: 5000));
        await database.SaveChangesAsync();

        await using var read = fixture.CreateContext();
        var queries = new PublicGalleryQueries(read);

        var firstPage = await queries.ListPhotosAsync(slug, page: 1, pageSize: 2, default);
        var secondPage = await queries.ListPhotosAsync(slug, page: 2, pageSize: 2, default);

        Assert.Equal(3, firstPage!.Total);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Single(secondPage!.Items);

        // The 800px photo must not advertise 1280 or 2048.
        var narrow = firstPage.Items.Single(photo => photo.Width == 800);
        Assert.Equal(new[] { 320, 640 }, narrow.AvailableWidths);

        // The 5000px one advertises the full ladder.
        var wide = secondPage.Items.Single();
        Assert.Equal(new[] { 320, 640, 1280, 2048 }, wide.AvailableWidths);
    }

    [Fact]
    public async Task Photo_detail_resolves_neighbours_collection_and_exif()
    {
        var slug = NewSlug("neighbours");
        await using var database = fixture.CreateContext();

        var collection = Collection.Create("Neighbours", slug, Now);
        database.Collections.Add(collection);

        var first = NewPhoto(collection.Id, position: 0, width: 1200);
        var middle = NewPhoto(collection.Id, position: 1, width: 1200, exif: """{"Make":"Fujifilm","Lens":"23mm"}""");
        var last = NewPhoto(collection.Id, position: 2, width: 1200);
        database.Photos.AddRange(first, middle, last);
        await database.SaveChangesAsync();

        await using var read = fixture.CreateContext();
        var queries = new PublicGalleryQueries(read);

        var detail = await queries.GetPhotoAsync(middle.Id, default);

        Assert.NotNull(detail);
        Assert.Equal(first.Id, detail.PreviousPhotoId);
        Assert.Equal(last.Id, detail.NextPhotoId);
        Assert.Equal(slug, detail.CollectionSlug);
        Assert.Equal("Fujifilm", detail.Exif["Make"]);
    }

    [Fact]
    public async Task Soft_deleted_photos_disappear_from_listings()
    {
        var slug = NewSlug("deleted");
        await using var database = fixture.CreateContext();

        var collection = Collection.Create("Deleted", slug, Now);
        database.Collections.Add(collection);

        var kept = NewPhoto(collection.Id, position: 0, width: 1600);
        var removed = NewPhoto(collection.Id, position: 1, width: 1600);
        database.Photos.AddRange(kept, removed);
        await database.SaveChangesAsync();

        removed.SoftDelete(Now);
        await database.SaveChangesAsync();

        await using var read = fixture.CreateContext();
        var queries = new PublicGalleryQueries(read);

        var page = await queries.ListPhotosAsync(slug, page: 1, pageSize: 10, default);

        Assert.Single(page!.Items);
        Assert.Equal(kept.Id, page.Items[0].Id);
        Assert.Null(await queries.GetPhotoAsync(removed.Id, default));
    }

    [Fact]
    public async Task The_featured_rank_is_exposed_rather_than_inferred()
    {
        var slug = NewSlug("featured");
        await using var database = fixture.CreateContext();

        var collection = Collection.Create("Featured", slug, Now);
        database.Collections.Add(collection);

        var photo = NewPhoto(collection.Id, position: 0, width: 1600);
        photo.Feature(7, Now);
        database.Photos.Add(photo);
        await database.SaveChangesAsync();

        await using var read = fixture.CreateContext();
        var queries = new PublicGalleryQueries(read);

        var detail = await queries.GetPhotoAsync(photo.Id, default);

        // Clients used to derive this from the home feed's ordering, which
        // renumbered curation on any edit.
        Assert.Equal(7, detail!.Photo.FeaturedRank);
    }

    [Fact]
    public async Task Intro_returns_distinct_photos_up_to_the_requested_count()
    {
        var slug = NewSlug("intro");
        await using var database = fixture.CreateContext();

        var collection = Collection.Create("Intro", slug, Now);
        database.Collections.Add(collection);
        for (var position = 0; position < 6; position++)
        {
            database.Photos.Add(NewPhoto(collection.Id, position, width: 1600));
        }

        await database.SaveChangesAsync();

        await using var read = fixture.CreateContext();
        var queries = new PublicGalleryQueries(read);

        var photos = await queries.GetIntroAsync(5, default);

        Assert.Equal(5, photos.Count);
        Assert.Equal(5, photos.Select(photo => photo.Id).Distinct().Count());
    }

    [Fact]
    public async Task Unknown_slugs_return_null_rather_than_an_empty_page()
    {
        await using var read = fixture.CreateContext();
        var queries = new PublicGalleryQueries(read);

        Assert.Null(await queries.GetCollectionAsync("does-not-exist", default));
        Assert.Null(await queries.ListPhotosAsync("does-not-exist", page: 1, pageSize: 10, default));
        Assert.Null(await queries.GetPhotoAsync("01J0000000000000000000000", default));
    }

    private static string NewSlug(string prefix) =>
        $"{prefix}-{Ulid.NewUlid().ToString()[..10].ToLowerInvariant()}";

    private static Photo NewPhoto(
        string collectionId,
        int position,
        int width,
        string exif = "{}")
    {
        var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            BitConverter.GetBytes(position ^ width)));

        return Photo.Create(
            collectionId,
            $"originals/{position}-{width}.jpg",
            hash,
            width,
            width / 2,
            exif,
            "#334455",
            Now,
            position);
    }
}
