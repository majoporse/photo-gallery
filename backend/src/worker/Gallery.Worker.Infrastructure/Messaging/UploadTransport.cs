using Gallery.Contracts.Uploads;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Postgresql;
using Wolverine.Runtime.Handlers;

namespace Gallery.Worker.Infrastructure.Messaging;

/// <summary>
/// The worker half of the queue contract: consume commands, answer on results.
/// The queue names are duplicated from the API's copy on purpose. They are the
/// wire, and each service has to name it for itself.
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
            // Without this a receiver polls every five seconds; with it, a NOTIFY
            // wakes the receive loop as soon as the row is committed.
            .EnableQueueNotifications();

        options.PublishMessage<PhotoProcessed>().ToPostgresqlQueue(UploadQueues.Results);
        options.PublishMessage<UploadRejected>().ToPostgresqlQueue(UploadQueues.Results);

        // Transient failures retry with a cooldown; when they run out the API is
        // told why instead of the message dead-lettering into silence. This is
        // transport policy, so it lives here rather than in the application
        // layer, which stays free of Wolverine.
        options.OnException<Exception>()
            .RetryWithCooldown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15))
            .Then
            .CompensatingAction<ProcessUpload>(
                (message, exception, bus) => bus.PublishAsync(
                    new UploadRejected(message.UploadId, RejectionReason.ProcessingFailed, exception.Message)),
                InvokeResult.Stop);

        // Routing is explicit above; nothing may be handled in-process by accident.
        options.Policies.DisableConventionalLocalRouting();
    }
}
