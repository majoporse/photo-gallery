using Gallery.Application.Admin;
using Gallery.Application.Gallery;
using Gallery.Application.Storage;
using Gallery.Domain.Collections;
using Gallery.Infrastructure.Persistence.Commands;
using Gallery.Infrastructure.Persistence.Queries;
using Gallery.Infrastructure.Storage;
using ImageMagick;
using Microsoft.Extensions.Options;

namespace Gallery.Infrastructure.Tests;

/// <summary>
/// The upload path end to end: HTTP-shaped inputs, real bytes, real S3, real SQL.
/// Only started for the tests that need both containers.
/// </summary>
public sealed class FullStackFixture : IAsyncLifetime
{
    public PostgresFixture Postgres { get; } = new();

    public SeaweedFixture Seaweed { get; } = new();

    public async Task InitializeAsync()
    {
        await Postgres.InitializeAsync();
        await Seaweed.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await Seaweed.DisposeAsync();
        await Postgres.DisposeAsync();
    }
}

[CollectionDefinition(nameof(FullStackCollection))]
public sealed class FullStackCollection : ICollectionFixture<FullStackFixture>;

[Collection(nameof(FullStackCollection))]
public sealed class AdminUploadTests(FullStackFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Upload_creates_photos_and_reports_per_file_failures()
    {
        await using var database = fixture.Postgres.CreateContext();

        var slug = $"upload-{Ulid.NewUlid().ToString()[..10].ToLowerInvariant()}";
        var collection = Collection.Create("Upload", slug, Now);
        database.Collections.Add(collection);
        await database.SaveChangesAsync();

        var admin = new AdminGallery(
            database,
            new UploadPipeline(
                new S3ObjectStore(Options.Create(fixture.Seaweed.Options)),
                new MagickImageProcessor(),
                new FileSignaturesImageFormatDetector()),
            TimeProvider.System);

        var good = CreateJpeg(1600, 1000);
        var notAnImage = "definitely not a photograph"u8.ToArray();

        var batch = await admin.AddPhotosAsync(
            collection.Id,
            [
                new UploadFile("good.jpg", good.Length, new MemoryStream(good)),
                new UploadFile("notes.txt", notAnImage.Length, new MemoryStream(notAnImage)),
            ],
            default);

        Assert.True(batch.Ok);
        Assert.Single(batch.Value!.Created);
        var failure = Assert.Single(batch.Value.Failed);
        Assert.Equal("notes.txt", failure.FileName);
        Assert.Equal(UploadRejection.UnsupportedFormat, failure.Reason);

        var queries = new PublicGalleryQueries(fixture.Postgres.CreateContext());
        var page = await queries.ListPhotosAsync(slug, page: 1, pageSize: 10, default);

        Assert.Single(page!.Items);
        Assert.Equal(new[] { 320, 640, 1280 }, page.Items[0].AvailableWidths);
    }

    [Fact]
    public async Task Uploading_to_a_missing_collection_is_a_404()
    {
        await using var database = fixture.Postgres.CreateContext();

        var admin = new AdminGallery(
            database,
            new UploadPipeline(
                new S3ObjectStore(Options.Create(fixture.Seaweed.Options)),
                new MagickImageProcessor(),
                new FileSignaturesImageFormatDetector()),
            TimeProvider.System);

        var batch = await admin.AddPhotosAsync(
            "01J0000000000000000000000",
            [new UploadFile("x.jpg", 3, new MemoryStream([1, 2, 3]))],
            default);

        Assert.False(batch.Ok);
        Assert.Equal(AdminError.NotFound, batch.Error);
    }

    private static byte[] CreateJpeg(int width, int height)
    {
        using var image = new MagickImage(MagickColors.DarkSlateBlue, (uint)width, (uint)height);
        return image.ToByteArray(MagickFormat.Jpeg);
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var image = new MagickImage(MagickColors.Orange, (uint)width, (uint)height);
        return image.ToByteArray(MagickFormat.Png);
    }
}
