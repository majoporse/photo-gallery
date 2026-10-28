using Gallery.Contracts.Uploads;
using Wolverine.Attributes;

namespace Gallery.Notifier;

/// <summary>
/// The whole consumer: one status change in, one fan-out out. The notifier keeps
/// no state, so a restarted process simply leaves reconnecting browsers to
/// refetch the list from the API.
/// </summary>
[WolverineHandler]
public sealed class UploadStatusHandler(UploadStatusHub hub)
{
    public void Handle(UploadView view) => hub.Publish(view);
}
