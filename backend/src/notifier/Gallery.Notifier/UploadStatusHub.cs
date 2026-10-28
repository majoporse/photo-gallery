using System.Collections.Concurrent;
using System.Threading.Channels;
using Gallery.Contracts.Uploads;

namespace Gallery.Notifier;

/// <summary>
/// Fan-out to the owner's open streams. Each subscriber gets its own bounded
/// channel: a slow browser drops its oldest deltas rather than stalling the
/// queue consumer, and its next reconnect refetches the list anyway.
/// </summary>
public sealed class UploadStatusHub
{
    private readonly ConcurrentDictionary<Guid, Channel<UploadView>> _subscribers = new();

    public Subscription Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<UploadView>(new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        _subscribers[id] = channel;
        return new Subscription(id, channel.Reader, this);
    }

    public void Publish(UploadView view)
    {
        foreach (var channel in _subscribers.Values)
        {
            channel.Writer.TryWrite(view);
        }
    }

    private void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }

    public sealed class Subscription(Guid id, ChannelReader<UploadView> reader, UploadStatusHub hub) : IDisposable
    {
        public ChannelReader<UploadView> Reader => reader;

        public void Dispose() => hub.Unsubscribe(id);
    }
}
