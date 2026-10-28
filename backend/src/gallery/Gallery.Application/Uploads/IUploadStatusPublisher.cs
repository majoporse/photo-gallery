using Gallery.Contracts.Uploads;

namespace Gallery.Application.Uploads;

/// <summary>
/// API to notifier: this is what the owner should see now. Fire and forget, on
/// purpose: the row is the truth, and the browser refetches the list after any
/// reconnect, so a lost event costs a stale badge, never data.
/// </summary>
public interface IUploadStatusPublisher
{
    Task PublishAsync(UploadView view, CancellationToken cancellationToken);
}
