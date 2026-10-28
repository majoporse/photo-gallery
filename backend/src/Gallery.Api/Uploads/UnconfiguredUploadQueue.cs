using Gallery.Contracts.Uploads;

namespace Gallery.Api.Uploads;

/// <summary>
/// Present so the container stays constructible without a database. Reaching it
/// means a route or a listener tried to use the queue in a process that has none.
/// </summary>
public sealed class UnconfiguredUploadQueue : IUploadQueue, IUploadResults
{
    public Task PublishAsync(ProcessUpload message, CancellationToken cancellationToken) =>
        throw Unconfigured();

    public Task PublishAsync(PhotoProcessed message, CancellationToken cancellationToken) =>
        throw Unconfigured();

    public Task PublishAsync(UploadRejected message, CancellationToken cancellationToken) =>
        throw Unconfigured();

    private static InvalidOperationException Unconfigured() =>
        new("No database is configured, so uploads cannot be queued.");
}
