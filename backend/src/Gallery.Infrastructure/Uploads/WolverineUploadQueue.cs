using Gallery.Application.Uploads;
using Wolverine;

namespace Gallery.Infrastructure.Uploads;

public sealed class WolverineUploadQueue(IMessageBus bus) : IUploadQueue
{
    public async Task PublishAsync(string uploadId, CancellationToken cancellationToken) =>
        await bus.PublishAsync(new ProcessUpload(uploadId));
}
