namespace Gallery.Application.Storage;

/// <summary>
/// Decides what a file really is from its first bytes, rather than trusting the
/// extension the client sent.
/// </summary>
public static class ImageFormatSniffer
{
    public static string? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return "jpeg";
        }

        if (header.Length >= 8
            && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
            && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            return "png";
        }

        if (header.Length >= 12
            && header[..4].SequenceEqual("RIFF"u8)
            && header[8..12].SequenceEqual("WEBP"u8))
        {
            return "webp";
        }

        if (header.Length >= 12 && header[4..8].SequenceEqual("ftyp"u8))
        {
            var brand = System.Text.Encoding.ASCII.GetString(header[8..12]);
            return brand switch
            {
                "avif" => "avif",
                "heic" or "heix" or "hevc" or "mif1" => "heic",
                _ => null,
            };
        }

        return null;
    }
}
