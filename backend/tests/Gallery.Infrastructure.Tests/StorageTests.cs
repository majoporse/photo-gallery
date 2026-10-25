using Amazon.S3;
using Amazon.S3.Model;
using Gallery.Application.Storage;
using Gallery.Infrastructure.Storage;
using ImageMagick;
using Microsoft.Extensions.Options;

namespace Gallery.Infrastructure.Tests;

[CollectionDefinition(nameof(SeaweedCollection))]
public sealed class SeaweedCollection : ICollectionFixture<SeaweedFixture>;

[Collection(nameof(SeaweedCollection))]
public sealed class StorageTests(SeaweedFixture fixture)
{
    [Fact]
    public async Task Upload_stores_the_original_and_the_whole_ladder()
    {
        var pipeline = CreatePipeline();
        var bytes = CreateJpeg(width: 1600, height: 1000);

        var result = await Process(pipeline, bytes, "iceland.jpg");

        Assert.True(result.Succeeded);
        var photo = result.Photo!;
        Assert.Equal(1600, photo.Width);
        Assert.Equal(1000, photo.Height);

        // 1600px original: 320, 640 and 1280 survive; 2048 is skipped, not upscaled.
        Assert.Equal(3 * 3, photo.DerivativeKeys.Count);
        Assert.DoesNotContain(photo.DerivativeKeys, key => key.Contains("2048w", StringComparison.Ordinal));

        var store = CreateStore();
        Assert.True(await store.ExistsAsync(ObjectBucket.Private, photo.OriginalKey, default));

        foreach (var key in photo.DerivativeKeys)
        {
            Assert.True(await store.ExistsAsync(ObjectBucket.Public, key, default), key);
        }
    }

    [Fact]
    public async Task The_same_bytes_produce_identical_keys_and_no_new_objects()
    {
        var pipeline = CreatePipeline();
        var bytes = CreateJpeg(width: 900, height: 600);

        var first = await Process(pipeline, bytes, "same.jpg");

        using var client = fixture.CreateClient();
        var afterFirst = await CountObjectsAsync(client);

        var second = await Process(pipeline, bytes, "same.jpg");
        var afterSecond = await CountObjectsAsync(client);

        Assert.Equal(first.Photo!.DerivativeKeys, second.Photo!.DerivativeKeys);
        Assert.Equal(first.Photo.OriginalSha256, second.Photo.OriginalSha256);
        Assert.Equal(afterFirst, afterSecond);
    }

    private async Task<int> CountObjectsAsync(IAmazonS3 client)
    {
        var listed = await client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = fixture.Options.PublicBucket,
        });

        return listed.S3Objects.Count;
    }

    [Fact]
    public async Task Png_bytes_named_jpg_are_rejected()
    {
        var pipeline = CreatePipeline();
        using var image = new MagickImage(MagickColors.SeaGreen, 800, 600);
        var bytes = image.ToByteArray(MagickFormat.Png);

        var result = await Process(pipeline, bytes, "lie.jpg");

        Assert.False(result.Succeeded);
        Assert.Equal(UploadRejection.ExtensionMismatch, result.Rejection);
    }

    [Fact]
    public async Task A_tiny_image_is_rejected()
    {
        var pipeline = CreatePipeline();
        var bytes = CreateJpeg(width: 400, height: 300);

        var result = await Process(pipeline, bytes, "small.jpg");

        Assert.Equal(UploadRejection.TooSmall, result.Rejection);
    }

    [Fact]
    public async Task A_declared_length_over_the_limit_is_rejected_before_reading()
    {
        var pipeline = CreatePipeline();
        using var stream = new MemoryStream(CreateJpeg(width: 1000, height: 800));

        var result = await pipeline.ProcessAsync(
            "photo-1",
            "huge.jpg",
            UploadPipeline.MaxBytes + 1,
            stream,
            default);

        Assert.Equal(UploadRejection.FileTooLarge, result.Rejection);
    }

    [Fact]
    public async Task Derivatives_carry_an_immutable_cache_header()
    {
        // Settles the spec's open question: does SeaweedFS round-trip Cache-Control?
        var pipeline = CreatePipeline();
        var result = await Process(pipeline, CreateJpeg(width: 700, height: 500), "cached.jpg");
        var key = result.Photo!.DerivativeKeys[0];

        using var client = fixture.CreateClient();
        var metadata = await client.GetObjectMetadataAsync(fixture.Options.PublicBucket, key);

        Assert.Equal(UploadPipeline.ImmutableCache, metadata.Headers.CacheControl);
        Assert.StartsWith("derivatives/", key, StringComparison.Ordinal);
    }

    private UploadPipeline CreatePipeline() => new(CreateStore(), new MagickImageProcessor());

    private S3ObjectStore CreateStore() => new(Options.Create(fixture.Options));

    private static Task<UploadResult> Process(UploadPipeline pipeline, byte[] bytes, string fileName)
    {
        using var stream = new MemoryStream(bytes);
        return pipeline.ProcessAsync("photo-fixture", fileName, bytes.Length, stream, default);
    }

    private static byte[] CreateJpeg(int width, int height)
    {
        using var image = new MagickImage(MagickColors.SteelBlue, (uint)width, (uint)height);
        return image.ToByteArray(MagickFormat.Jpeg);
    }
}
