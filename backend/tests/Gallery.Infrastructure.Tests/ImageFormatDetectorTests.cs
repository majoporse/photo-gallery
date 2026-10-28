using Gallery.Contracts.Storage;
using Gallery.Infrastructure.Storage;

namespace Gallery.Infrastructure.Tests;

public sealed class ImageFormatDetectorTests
{
    private readonly FileSignaturesImageFormatDetector _detector = new();

    [Fact]
    public void Detects_the_formats_the_pipeline_accepts()
    {
        Assert.Equal(ImageFormats.Jpeg, _detector.Detect(Jpeg())?.Extension);
        Assert.Equal(ImageFormats.Png, _detector.Detect(Png())?.Extension);
        Assert.Equal(ImageFormats.Webp, _detector.Detect(Webp())?.Extension);
        Assert.Equal(ImageFormats.Avif, _detector.Detect(Avif())?.Extension);
        Assert.Equal(ImageFormats.Heic, _detector.Detect(Heic())?.Extension);
    }

    [Fact]
    public void Reports_the_media_type_for_the_original_upload()
    {
        Assert.Equal(ImageFormats.JpegMediaType, _detector.Detect(Jpeg())?.MediaType);
        Assert.Equal(ImageFormats.AvifMediaType, _detector.Detect(Avif())?.MediaType);
        Assert.Equal(ImageFormats.HeicMediaType, _detector.Detect(Heic())?.MediaType);
    }

    [Fact]
    public void Does_not_guess_at_arbitrary_bytes()
    {
        Assert.Null(_detector.Detect("this is definitely not an image"u8.ToArray()));
        Assert.Null(_detector.Detect([]));
    }

    [Fact]
    public void A_webp_is_not_mistaken_for_a_riff_audio_file()
    {
        // "RIFF" alone is shared with WAV/AVI; only the WEBP form is accepted.
        var riffWave = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x24, 0, 0, 0, 0x57, 0x41, 0x56, 0x45 };

        Assert.Null(_detector.Detect(riffWave));
    }

    private static byte[] Jpeg() => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];

    private static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    private static byte[] Webp() => [.. "RIFF"u8, 0x24, 0, 0, 0, .. "WEBPVP8 "u8, 0, 0, 0, 0];

    private static byte[] Avif() => [0, 0, 0, 0x1C, .. "ftypavif"u8, 0, 0, 0, 0, .. "avifmif1"u8];

    private static byte[] Heic() => [0, 0, 0, 0x18, .. "ftypheic"u8, 0, 0, 0, 0, .. "heicmif1"u8];
}
