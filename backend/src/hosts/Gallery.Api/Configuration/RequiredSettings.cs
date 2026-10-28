namespace Gallery.Api.Configuration;

/// <summary>
/// Settings without which the application cannot serve a working gallery.
/// Only names are ever reported, never values.
/// </summary>
public static class RequiredSettings
{
    private static readonly string[] Keys =
    [
        ConfigurationKeys.Storage.Endpoint,
        ConfigurationKeys.Storage.PublicBucket,
        ConfigurationKeys.Storage.PrivateBucket,
        ConfigurationKeys.Storage.AccessKey,
        ConfigurationKeys.Storage.SecretKey,
        ConfigurationKeys.Admin.Username,
        ConfigurationKeys.Admin.PasswordHash,
        ConfigurationKeys.Auth.SigningKey,
        ConfigurationKeys.Cors.AllowedOrigin,
    ];

    public static IReadOnlyList<string> Missing(IConfiguration configuration) =>
        Keys.Where(key => string.IsNullOrWhiteSpace(configuration[key])).ToArray();
}
