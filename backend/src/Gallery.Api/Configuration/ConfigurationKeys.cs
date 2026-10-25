using Gallery.Infrastructure.Storage;

namespace Gallery.Api.Configuration;

/// <summary>
/// Every configuration key the application requires. Derived from each section's
/// constant plus <c>nameof</c>, so renaming a property cannot leave a stale key
/// behind in a validator or a test.
/// </summary>
public static class ConfigurationKeys
{
    public static class Storage
    {
        public const string Section = ObjectStorageOptions.SectionName;
        public const string Endpoint = $"{Section}:{nameof(ObjectStorageOptions.Endpoint)}";
        public const string PublicBucket = $"{Section}:{nameof(ObjectStorageOptions.PublicBucket)}";
        public const string PrivateBucket = $"{Section}:{nameof(ObjectStorageOptions.PrivateBucket)}";
        public const string AccessKey = $"{Section}:{nameof(ObjectStorageOptions.AccessKey)}";
        public const string SecretKey = $"{Section}:{nameof(ObjectStorageOptions.SecretKey)}";
    }

    public static class Admin
    {
        public const string Section = AdminOptions.SectionName;
        public const string Username = $"{Section}:{nameof(AdminOptions.Username)}";
        public const string PasswordHash = $"{Section}:{nameof(AdminOptions.PasswordHash)}";
    }

    public static class Auth
    {
        public const string Section = AuthOptions.SectionName;
        public const string SigningKey = $"{Section}:{nameof(AuthOptions.SigningKey)}";
        public const string Issuer = $"{Section}:{nameof(AuthOptions.Issuer)}";
        public const string Audience = $"{Section}:{nameof(AuthOptions.Audience)}";
        public const string CookieSecure = $"{Section}:{nameof(AuthOptions.CookieSecure)}";
    }

    public static class Cors
    {
        public const string Section = CorsOptions.SectionName;
        public const string AllowedOrigin = $"{Section}:{nameof(CorsOptions.AllowedOrigin)}";
    }
}
