namespace Gallery.Api.Configuration;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string SigningKey { get; init; } = string.Empty;
    public string Issuer { get; init; } = "photo-gallery";
    public string Audience { get; init; } = "photo-gallery-admin";
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromHours(12);
}
