using Gallery.Contracts.Uploads;
using Wolverine;

namespace Gallery.Infrastructure.Uploads;

/// <summary>
/// Both directions of the upload protocol are a publish on the bus; which queue a
/// message lands in is decided once, in <see cref="UploadTransport"/>.
/// </summary>
public sealed class WolverineUploadBus(IMessageBus bus) : IUploadQueue, IUploadResults
{
    public async Task PublishAsync(ProcessUpload message, CancellationToken cancellationToken) =>
        await bus.PublishAsync(message);

    public async Task PublishAsync(PhotoProcessed message, CancellationToken cancellationToken) =>
        await bus.PublishAsync(message);

    public async Task PublishAsync(UploadRejected message, CancellationToken cancellationToken) =>
        await bus.PublishAsync(message);
}
