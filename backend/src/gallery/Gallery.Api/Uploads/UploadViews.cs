using Gallery.Contracts.Uploads;
using Gallery.Domain.Uploads;

namespace Gallery.Api.Uploads;

/// <summary>
/// Maps the row onto the shape the owner sees. It lives here rather than on the
/// record because the record is a contract: the notifier parses it and has no
/// access to the domain.
/// </summary>
public static class UploadViews
{
    public static UploadView From(Upload upload) =>
        new(
            upload.Id,
            upload.CollectionId,
            upload.FileName,
            upload.Position,
            upload.Status.ToString(),
            upload.Rejection?.ToString(),
            upload.Failure,
            upload.Status == UploadStatus.Succeeded ? upload.PhotoId : null,
            upload.CreatedAt);
}
