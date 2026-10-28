namespace Gallery.Api.Admin;

public static class AdminCookies
{
    public const string Name = "pg_session";
    public const string RequiredHeader = "X-Requested-With";
    public const string RequiredHeaderValue = "photo-gallery";

    /// <summary>
    /// SameSite=None is mandatory because the SPA and the API are different
    /// origins; browsers only accept it together with Secure.
    /// </summary>
    public static CookieOptions Create(TimeSpan lifetime, bool secure) => new()
    {
        HttpOnly = true,
        Secure = secure,
        SameSite = secure ? SameSiteMode.None : SameSiteMode.Lax,
        Path = "/",
        MaxAge = lifetime,
        IsEssential = true,
    };
}
