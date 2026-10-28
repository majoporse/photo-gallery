namespace Gallery.Contracts.Uploads;

/// <summary>
/// Worker to API. The only thing that turns an Upload into a Photo or a
/// rejection, and the reason the worker never needs the database.
/// </summary>
public interface IUploadResults
{
    Task PublishAsync(PhotoProcessed message, CancellationToken cancellationToken);

    Task PublishAsync(UploadRejected message, CancellationToken cancellationToken);
}
