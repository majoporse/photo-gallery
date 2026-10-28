using Gallery.Api.Uploads;
using Gallery.Application.Admin;
using Gallery.Application.Gallery;
using Gallery.Application.Uploads;
using Gallery.Contracts.Storage;
using Gallery.Contracts.Uploads;
using Gallery.Domain.Collections;
using Gallery.Domain.Uploads;
using Gallery.Infrastructure.Storage;
using Gallery.Persistence;
using Gallery.Worker.Processing;
using ImageMagick;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gallery.Integration.Tests;

/// <summary>
/// The upload path end to end: a presigned destination, real bytes in real S3,
/// the worker's handler, the API's reply handler, real SQL. Only started for
/// tests that need both containers.
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
    public async Task An_upload_becomes_a_photo_at_the_position_it_reserved()
    {
        await using var database = fixture.Postgres.CreateContext();
        var harness = Create(database);
        var collection = await CreateCollection(database);

        var begun = await harness.Commands.BeginAsync(
            collection.Id,
            new BeginUpload("good.jpg", CreateJpeg(1600, 1000).Length),
            default);

        Assert.True(begun.Ok);

        await PutAsync(harness.Store, begun.Value!.Key, CreateJpeg(1600, 1000));

        var completed = await harness.Commands.CompleteAsync(begun.Value.UploadId, default);
        Assert.True(completed.Ok);
        Assert.Equal("Queued", completed.Value!.Status);
        Assert.Contains(begun.Value.UploadId, harness.Queue.Published.Select(message => message.UploadId));

        await harness.ProcessAsync(begun.Value.UploadId);

        await using var read = fixture.Postgres.CreateContext();
        var upload = await read.Uploads.SingleAsync(candidate => candidate.Id == begun.Value.UploadId);
        var photo = await read.Photos.SingleAsync(candidate => candidate.CollectionId == collection.Id);

        Assert.Equal(UploadStatus.Succeeded, upload.Status);
        Assert.Equal(photo.Id, upload.PhotoId);
        Assert.Equal(0, photo.Position);
        Assert.Equal(1600, photo.Width);
        Assert.Equal(new[] { 320, 640, 1280 }, DerivativeWidths.For(photo.Width));

        Assert.True(await harness.Store.ExistsAsync(
            ObjectBucket.Public,
            UploadPolicy.DerivativeKey(photo.OriginalSha256, new RenderedDerivative(320, DerivativeFormat.Jpeg, [])),
            default));
    }

    [Fact]
    public async Task An_answer_that_arrives_twice_creates_one_photo()
    {
        await using var database = fixture.Postgres.CreateContext();
        var harness = Create(database);
        var collection = await CreateCollection(database);

        var begun = await harness.Commands.BeginAsync(
            collection.Id, new BeginUpload("twice.jpg", CreateJpeg(900, 700).Length), default);

        await PutAsync(harness.Store, begun.Value!.Key, CreateJpeg(900, 700));
        await harness.Commands.CompleteAsync(begun.Value.UploadId, default);
        await harness.ProcessAsync(begun.Value.UploadId);

        // The queue is at-least-once: the API must treat a redelivered answer as
        // a no-op rather than a second insert.
        await harness.ApplyAsync(harness.Results.Published.OfType<PhotoProcessed>().Single());

        await using var read = fixture.Postgres.CreateContext();
        Assert.Equal(1, await read.Photos.CountAsync(photo => photo.CollectionId == collection.Id));
    }

    [Fact]
    public async Task A_presigned_object_that_is_not_an_image_is_rejected_and_deleted()
    {
        await using var database = fixture.Postgres.CreateContext();
        var harness = Create(database);
        var collection = await CreateCollection(database);

        var bytes = "definitely not a photograph"u8.ToArray();
        var begun = await harness.Commands.BeginAsync(
            collection.Id,
            new BeginUpload("notes.txt", bytes.Length),
            default);

        await PutAsync(harness.Store, begun.Value!.Key, bytes);
        await harness.Commands.CompleteAsync(begun.Value.UploadId, default);
        await harness.ProcessAsync(begun.Value.UploadId);

        await using var read = fixture.Postgres.CreateContext();
        var upload = await read.Uploads.SingleAsync(candidate => candidate.Id == begun.Value.UploadId);

        Assert.Equal(UploadStatus.Rejected, upload.Status);
        Assert.Equal(UploadRejection.UnsupportedFormat, upload.Rejection);
        Assert.False(await harness.Store.ExistsAsync(ObjectBucket.Private, begun.Value.Key, default));
        Assert.False(await read.Photos.AnyAsync(photo => photo.CollectionId == collection.Id));
    }

    [Fact]
    public async Task An_upload_whose_bytes_vanished_is_rejected_as_missing()
    {
        await using var database = fixture.Postgres.CreateContext();
        var harness = Create(database);
        var collection = await CreateCollection(database);

        var begun = await harness.Commands.BeginAsync(
            collection.Id, new BeginUpload("gone.jpg", CreateJpeg(800, 600).Length), default);

        await PutAsync(harness.Store, begun.Value!.Key, CreateJpeg(800, 600));
        await harness.Commands.CompleteAsync(begun.Value.UploadId, default);
        await harness.Store.DeleteAsync(ObjectBucket.Private, begun.Value.Key, default);

        await harness.ProcessAsync(begun.Value.UploadId);

        await using var read = fixture.Postgres.CreateContext();
        var upload = await read.Uploads.SingleAsync(candidate => candidate.Id == begun.Value.UploadId);

        Assert.Equal(UploadStatus.Rejected, upload.Status);
        Assert.Equal(UploadRejection.MissingBytes, upload.Rejection);
    }

    [Fact]
    public async Task Completing_before_the_bytes_arrive_is_a_conflict()
    {
        await using var database = fixture.Postgres.CreateContext();
        var harness = Create(database);
        var collection = await CreateCollection(database);

        var begun = await harness.Commands.BeginAsync(
            collection.Id,
            new BeginUpload("missing.jpg", 10),
            default);

        var completed = await harness.Commands.CompleteAsync(begun.Value!.UploadId, default);

        Assert.False(completed.Ok);
        Assert.Equal(AdminError.NotUploaded, completed.Error);
        Assert.Empty(harness.Queue.Published);
    }

    [Fact]
    public async Task Two_uploads_keep_the_order_they_were_started_in()
    {
        await using var database = fixture.Postgres.CreateContext();
        var harness = Create(database);
        var collection = await CreateCollection(database);

        var first = await harness.Commands.BeginAsync(
            collection.Id, new BeginUpload("first.jpg", 1), default);
        var second = await harness.Commands.BeginAsync(
            collection.Id, new BeginUpload("second.jpg", 1), default);

        await PutAsync(harness.Store, first.Value!.Key, CreateJpeg(800, 600));
        await PutAsync(harness.Store, second.Value!.Key, CreateJpeg(800, 600));

        await harness.Commands.CompleteAsync(first.Value.UploadId, default);
        await harness.Commands.CompleteAsync(second.Value.UploadId, default);

        // Processed in the opposite order to prove position is not arrival order.
        await harness.ProcessAsync(second.Value.UploadId);
        await harness.ProcessAsync(first.Value.UploadId);

        await using var read = fixture.Postgres.CreateContext();
        var photos = await read.Photos
            .Where(photo => photo.CollectionId == collection.Id)
            .OrderBy(photo => photo.Position)
            .ToListAsync();

        var uploads = await read.Uploads
            .ToDictionaryAsync(upload => upload.Id, upload => upload.PhotoId);

        Assert.Equal(2, photos.Count);
        Assert.Equal(uploads[first.Value.UploadId], photos[0].Id);
        Assert.Equal(uploads[second.Value.UploadId], photos[1].Id);
    }

    [Fact]
    public async Task Uploading_to_a_missing_collection_is_a_404()
    {
        await using var database = fixture.Postgres.CreateContext();
        var harness = Create(database);

        var begun = await harness.Commands.BeginAsync(
            "01J0000000000000000000000",
            new BeginUpload("x.jpg", 3),
            default);

        Assert.False(begun.Ok);
        Assert.Equal(AdminError.NotFound, begun.Error);
    }

    [Fact]
    public async Task An_upload_over_the_size_limit_is_refused_before_any_row_is_written()
    {
        await using var database = fixture.Postgres.CreateContext();
        var harness = Create(database);
        var collection = await CreateCollection(database);

        var begun = await harness.Commands.BeginAsync(
            collection.Id,
            new BeginUpload("huge.jpg", UploadPolicy.MaxBytes + 1),
            default);

        Assert.False(begun.Ok);
        Assert.Equal(AdminError.InvalidUpload, begun.Error);
        Assert.False(await database.Uploads.AnyAsync(upload => upload.CollectionId == collection.Id));
    }

    private static async Task<Collection> CreateCollection(GalleryDbContext database)
    {
        var slug = $"upload-{Ulid.NewUlid().ToString()[..10].ToLowerInvariant()}";
        var collection = Collection.Create("Upload", slug, Now);

        database.Collections.Add(collection);
        await database.SaveChangesAsync();

        return collection;
    }

    private async Task PutAsync(S3ObjectStore store, string key, byte[] bytes) =>
        await store.PutAsync(
            ObjectBucket.Private,
            key,
            new MemoryStream(bytes),
            "application/octet-stream",
            cacheControl: null,
            default);

    private Harness Create(GalleryDbContext database)
    {
        var store = new S3ObjectStore(Options.Create(fixture.Seaweed.Options));
        var queue = new RecordingQueue();
        var results = new RecordingResults();

        return new Harness(
            new UploadCommands(database, store, queue, Options.Create(fixture.Seaweed.Options), TimeProvider.System),
            new ProcessUploadHandler(
                store,
                new UploadPipeline(store, new MagickImageProcessor(), new FileSignaturesImageFormatDetector()),
                results),
            new UploadResultHandler(new TestContextFactory(fixture.Postgres.CreateContext), TimeProvider.System),
            queue,
            results,
            store);
    }

    private sealed class TestContextFactory(Func<GalleryDbContext> create) : IDbContextFactory<GalleryDbContext>
    {
        public GalleryDbContext CreateDbContext() => create();

        public Task<GalleryDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(create());
    }

    private sealed record Harness(
        UploadCommands Commands,
        ProcessUploadHandler Worker,
        UploadResultHandler Api,
        RecordingQueue Queue,
        RecordingResults Results,
        S3ObjectStore Store)
    {
        /// <summary>The command the API published for this upload.</summary>
        public ProcessUpload Command(string uploadId) =>
            Queue.Published.Single(message => message.UploadId == uploadId);

        /// <summary>
        /// One round trip: the worker answers the published command and the API
        /// applies every answer it produced, exactly as the two queues would.
        /// </summary>
        public async Task ProcessAsync(string uploadId)
        {
            Results.Published.Clear();
            await Worker.Handle(Command(uploadId), default);

            foreach (var message in Results.Published.ToList())
            {
                await ApplyAsync(message);
            }
        }

        public async Task ApplyAsync(object message)
        {
            switch (message)
            {
                case PhotoProcessed processed:
                    await Api.Handle(processed, default);
                    break;
                case UploadRejected rejected:
                    await Api.Handle(rejected, default);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected message {message.GetType().Name}.");
            }
        }
    }

    private sealed class RecordingQueue : IUploadQueue
    {
        public List<ProcessUpload> Published { get; } = [];

        public Task PublishAsync(ProcessUpload message, CancellationToken cancellationToken)
        {
            Published.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingResults : IUploadResults
    {
        public List<object> Published { get; } = [];

        public Task PublishAsync(PhotoProcessed message, CancellationToken cancellationToken)
        {
            Published.Add(message);
            return Task.CompletedTask;
        }

        public Task PublishAsync(UploadRejected message, CancellationToken cancellationToken)
        {
            Published.Add(message);
            return Task.CompletedTask;
        }
    }

    private static byte[] CreateJpeg(int width, int height)
    {
        using var image = new MagickImage(MagickColors.DarkSlateBlue, (uint)width, (uint)height);
        return image.ToByteArray(MagickFormat.Jpeg);
    }
}
