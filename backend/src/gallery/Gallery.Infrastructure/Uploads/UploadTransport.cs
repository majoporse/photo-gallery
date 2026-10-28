using Gallery.Contracts.Uploads;
using Wolverine;
using Wolverine.Postgresql;

namespace Gallery.Infrastructure.Uploads;

/// <summary>
/// The API half of the queue contract: publish commands, consume answers, publish
/// status. The queue names come from the contracts because all three hosts have
/// to spell them the same way.
/// </summary>
public static class UploadTransport
{
    public static void UseUploadQueues(this WolverineOptions options, string connectionString)
    {
        options.UsePostgresqlPersistenceAndTransport(
                connectionString,
                "gallery",
                transportSchema: "wolverine_queues")
            .AutoProvision()
            // The NOTIFY is emitted by the sender: without this flag on both ends
            // a receiver falls back to polling every five seconds.
            .EnableQueueNotifications();

        options.PublishMessage<ProcessUpload>().ToPostgresqlQueue(UploadQueues.Commands);
        options.PublishMessage<UploadView>().ToPostgresqlQueue(UploadQueues.Status);

        // Routing is explicit above; nothing may be handled in-process by accident.
        options.Policies.DisableConventionalLocalRouting();
    }
}
