using Gallery.Application.Uploads;
using Gallery.Contracts.Uploads;
using Wolverine;

namespace Gallery.Infrastructure.Uploads;

public sealed class WolverineUploadStatusPublisher(IMessageBus bus) : IUploadStatusPublisher
{
    public async Task PublishAsync(UploadView view, CancellationToken cancellationToken) =>
        await bus.PublishAsync(view);
}
