namespace Gallery.Notifier.Admin;

/// <summary>
/// Mirrored from the API: the cookie is set by the API and only its name has to
/// match here. Auth is the one thing this service copies rather than shares.
/// </summary>
public static class AdminCookies
{
    public const string Name = "pg_session";
}
