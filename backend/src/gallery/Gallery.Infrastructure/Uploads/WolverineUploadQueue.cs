using Gallery.Contracts.Uploads;
using Wolverine;

namespace Gallery.Infrastructure.Uploads;

/// <summary>
/// Publishing a command is a publish on the bus; which queue it lands in is
/// decided once, in <see cref="UploadTransport"/>.
/// </summary>
public sealed class WolverineUploadQueue(IMessageBus bus) : IUploadQueue
{
    public async Task PublishAsync(ProcessUpload message, CancellationToken cancellationToken) =>
        await bus.PublishAsync(message);
}
