namespace Gallery.Domain.Uploads;

/// <summary>
/// Why an upload never became a Photo. Validation codes are final and their
/// bytes are discarded; <see cref="ProcessingFailed"/> means the file was fine
/// but processing kept failing, so the original is kept.
/// </summary>
public enum UploadRejection
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
