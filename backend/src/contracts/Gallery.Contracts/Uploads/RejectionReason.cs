namespace Gallery.Contracts.Uploads;

/// <summary>
/// Why the worker will not render an Upload, in contract terms. The API maps
/// these onto its own rejection enum: keeping them apart means a domain refactor
/// never changes a wire format.
/// </summary>
public enum RejectionReason
{
    FileTooLarge,
    UnsupportedFormat,
    TooSmall,
    DimensionsTooLarge,
    CorruptImage,
    ProcessingFailed,

    /// <summary>The object was gone when the worker went to read it.</summary>
    MissingBytes,
}
