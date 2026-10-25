namespace Gallery.Api.Configuration;

/// <summary>
/// Settings without which the application cannot serve a working gallery.
/// Only names are ever reported, never values.
/// </summary>
public static class RequiredSettings
{
    private static readonly string[] Keys =
    [
        "Storage:Endpoint",
        "Storage:PublicBucket",
        "Storage:PrivateBucket",
        "Storage:AccessKey",
        "Storage:SecretKey",
        "Admin:Username",
        "Admin:PasswordHash",
        "Auth:SigningKey",
        "Cors:AllowedOrigin",
    ];

    public static IReadOnlyList<string> Missing(IConfiguration configuration) =>
        Keys.Where(key => string.IsNullOrWhiteSpace(configuration[key])).ToArray();
}
