using Gallery.Application.Admin;

namespace Gallery.Application.Uploads;

public interface IUploadService
{
    /// <summary>Creates the Upload, reserves its position, and signs one PUT.</summary>
    Task<AdminResult<PresignedUpload>> BeginAsync(
        string collectionId,
        BeginUpload command,
        CancellationToken cancellationToken);

    /// <summary>Confirms the bytes arrived and hands the Upload to the worker.</summary>
    Task<AdminResult<UploadView>> CompleteAsync(string uploadId, CancellationToken cancellationToken);

    /// <summary>Every Upload that has not reached a terminal state, oldest first.</summary>
    Task<IReadOnlyList<UploadView>> ListActiveAsync(CancellationToken cancellationToken);
}
