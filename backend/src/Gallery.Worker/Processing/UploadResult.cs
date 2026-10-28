using Gallery.Contracts.Uploads;
using Gallery.Domain.Uploads;

namespace Gallery.Worker.Processing;

public sealed record UploadResult(ProcessedPhoto? Photo, UploadRejection? Rejection)
{
    public bool Succeeded => Photo is not null;

    public static UploadResult Success(ProcessedPhoto photo) => new(photo, null);

    public static UploadResult Failed(UploadRejection rejection) => new(null, rejection);
}
