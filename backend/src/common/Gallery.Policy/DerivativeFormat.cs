namespace Gallery.Policy;

/// <summary>
/// The formats a derivative can be rendered in. Shared rather than duplicated
/// because the ladder the API advertises and the ladder the worker renders have
/// to be the same ladder.
/// </summary>
public enum DerivativeFormat
{
    Avif,
    Webp,
    Jpeg,
}
