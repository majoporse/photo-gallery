namespace Gallery.Api.Configuration;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public const string DefaultIssuer = "photo-gallery";
    public const string DefaultAudience = "photo-gallery-admin";

    public string SigningKey { get; init; } = string.Empty;
    public string Issuer { get; init; } = DefaultIssuer;
    public string Audience { get; init; } = DefaultAudience;
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromHours(12);

    /// <summary>Login attempts per minute per IP. Five is the production default.</summary>
    public int LoginPermitLimit { get; init; } = 5;

    /// <summary>Off only for plain-HTTP local development; SameSite=None needs it on.</summary>
    public bool CookieSecure { get; init; } = true;
}
