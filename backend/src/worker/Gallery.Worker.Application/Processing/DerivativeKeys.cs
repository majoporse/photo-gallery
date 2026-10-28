using Gallery.Worker.Application.Storage;

namespace Gallery.Worker.Application.Processing;

/// <summary>
/// Where a rendered derivative lives and how it may be cached. Content-addressed:
/// the same bytes always land on the same key, which is what makes a redelivered
/// message cost a re-render instead of a duplicate.
/// </summary>
public static class DerivativeKeys
{
    public const string Prefix = "derivatives";
    public const string ImmutableCache = "public, max-age=31536000, immutable";

    public static string For(string sha256, RenderedDerivative derivative) =>
        $"{Prefix}/{sha256}/{derivative.FileName}";
}
