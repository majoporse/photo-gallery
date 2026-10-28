namespace Gallery.Application.Uploads;

/// <summary>
/// Publishing is separate from the row write because the queue is at-least-once:
/// a lost publish is re-sent by the sweeper, and a duplicate is harmless.
/// </summary>
public interface IUploadQueue
{
    Task PublishAsync(string uploadId, CancellationToken cancellationToken);
}
