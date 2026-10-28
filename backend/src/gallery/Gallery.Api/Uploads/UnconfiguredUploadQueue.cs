using Gallery.Application.Uploads;
using Gallery.Contracts.Uploads;

namespace Gallery.Api.Uploads;

/// <summary>
/// Present so the container stays constructible without a database. Reaching it
/// means something tried to queue or publish in a process with no queue.
/// </summary>
public sealed class UnconfiguredUploadQueue : IUploadQueue, IUploadStatusPublisher
{
    public Task PublishAsync(ProcessUpload message, CancellationToken cancellationToken) =>
        throw Unconfigured();

    public Task PublishAsync(UploadView view, CancellationToken cancellationToken) =>
        throw Unconfigured();

    private static InvalidOperationException Unconfigured() =>
        new("No database is configured, so uploads cannot be queued.");
}
