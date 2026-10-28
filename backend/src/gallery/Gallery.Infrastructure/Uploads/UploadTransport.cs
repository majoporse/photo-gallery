using Gallery.Contracts.Uploads;
using Wolverine;
using Wolverine.Postgresql;

namespace Gallery.Infrastructure.Uploads;

/// <summary>
/// The API half of the queue contract: publish commands, consume answers. The
/// queue names are duplicated from the worker's copy on purpose. They are the
/// wire, and each service has to name it for itself.
/// </summary>
public static class UploadTransport
{
    public const string Commands = "uploads";
    public const string Results = "upload-results";

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

        options.PublishMessage<ProcessUpload>().ToPostgresqlQueue(Commands);

        // Routing is explicit above; nothing may be handled in-process by accident.
        options.Policies.DisableConventionalLocalRouting();
    }
}
