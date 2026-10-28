using Gallery.Contracts.Uploads;
using Wolverine;

namespace Gallery.Worker.Infrastructure.Messaging;

/// <summary>
/// The worker's only outbound dependency: publish the answer, let the API decide
/// what it means.
/// </summary>
public sealed class WolverineUploadResults(IMessageBus bus) : IUploadResults
{
    public async Task PublishAsync(PhotoProcessed message, CancellationToken cancellationToken) =>
        await bus.PublishAsync(message);

    public async Task PublishAsync(UploadRejected message, CancellationToken cancellationToken) =>
        await bus.PublishAsync(message);
}
