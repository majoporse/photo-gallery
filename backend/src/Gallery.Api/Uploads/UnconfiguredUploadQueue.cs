using Gallery.Application.Uploads;

namespace Gallery.Api.Uploads;

/// <summary>
/// Present so the container stays constructible without a database. Reaching it
/// means a route tried to enqueue a job in a process that has no queue.
/// </summary>
public sealed class UnconfiguredUploadQueue : IUploadQueue
{
    public Task PublishAsync(string uploadId, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(
            "No database is configured, so uploads cannot be queued.");
}
