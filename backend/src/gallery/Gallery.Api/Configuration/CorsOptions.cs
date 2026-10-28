namespace Gallery.Api.Configuration;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public string AllowedOrigin { get; init; } = string.Empty;
}
