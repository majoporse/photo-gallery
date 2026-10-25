namespace Gallery.Application.Storage;

public enum ObjectBucket
{
    /// <summary>Derivatives. Anonymous read, internet-facing.</summary>
    Public,

    /// <summary>Originals. Never served to visitors.</summary>
    Private,
}
