using Gallery.Domain.Uploads;

namespace Gallery.Application.Storage;

public sealed record ProcessedPhoto(
    string OriginalKey,
    string OriginalSha256,
    int Width,
    int Height,
    string ExifJson,
    string DominantColor,
    IReadOnlyList<string> DerivativeKeys);

public sealed record UploadResult(ProcessedPhoto? Photo, UploadRejection? Rejection)
{
    public bool Succeeded => Photo is not null;

    public static UploadResult Success(ProcessedPhoto photo) => new(photo, null);

    public static UploadResult Failed(UploadRejection rejection) => new(null, rejection);
}
