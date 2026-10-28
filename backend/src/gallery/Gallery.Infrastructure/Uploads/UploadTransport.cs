using Gallery.Contracts.Uploads;
using Wolverine;
using Wolverine.Postgresql;

namespace Gallery.Infrastructure.Uploads;

/// <summary>
/// The two queues are the whole contract between the hosts, so they are named
/// once: the API writes to <see cref="Commands"/>, the worker answers on
/// <see cref="Results"/>, and neither process needs the other's assemblies.
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
            // Without this a receiver polls every five seconds; with it, a NOTIFY
            // wakes the receive loop as soon as the row is committed.
            .EnableQueueNotifications();

        options.PublishMessage<ProcessUpload>().ToPostgresqlQueue(Commands);
        options.PublishMessage<PhotoProcessed>().ToPostgresqlQueue(Results);
        options.PublishMessage<UploadRejected>().ToPostgresqlQueue(Results);

        // Routing is explicit above; nothing may be handled in-process by accident.
        options.Policies.DisableConventionalLocalRouting();
    }
}
