using Gallery.Contracts.Uploads;

namespace Gallery.Api.Uploads;

/// <summary>
/// Present so the container stays constructible without a database. Reaching it
/// means a route tried to enqueue work in a process that has no queue.
/// </summary>
public sealed class UnconfiguredUploadQueue : IUploadQueue
{
    public Task PublishAsync(ProcessUpload message, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("No database is configured, so uploads cannot be queued.");
}
