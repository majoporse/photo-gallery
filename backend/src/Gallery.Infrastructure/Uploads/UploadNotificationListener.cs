using Gallery.Application.Uploads;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Gallery.Infrastructure.Uploads;

/// <summary>
/// Holds one dedicated connection per API replica and relays whatever the writer
/// committed. A dropped connection is retried forever: losing a notification is
/// survivable (the next stream opens with a snapshot), losing the listener is not.
/// </summary>
public sealed class UploadNotificationListener(
    IConfiguration configuration,
    IUploadStream stream,
    ILogger<UploadNotificationListener> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = configuration.GetConnectionString("Gallery");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogInformation("No database configured; upload notifications are disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ListenAsync(connectionString, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "The upload notification listener disconnected; retrying in {Delay}.",
                    RetryDelay);
            }

            try
            {
                await Task.Delay(RetryDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task ListenAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);

        connection.Notification += (_, notification) =>
        {
            var view = UploadJson.Deserialize(notification.Payload);

            if (view is not null)
            {
                stream.Publish(view);
            }
        };

        await connection.OpenAsync(cancellationToken);

        await using (var command = new NpgsqlCommand($"LISTEN {UploadEvents.Channel}", connection))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        logger.LogInformation("Listening for upload notifications.");

        while (!cancellationToken.IsCancellationRequested)
        {
            // WaitAsync returns as soon as a notification arrives; the loop is
            // what keeps the connection's notification queue drained.
            await connection.WaitAsync(cancellationToken);
        }
    }
}
