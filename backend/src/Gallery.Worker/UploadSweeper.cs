using Gallery.Contracts.Storage;
using Gallery.Application.Uploads;
using Gallery.Domain.Uploads;
using Gallery.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gallery.Worker;

/// <summary>
/// Repair and housekeeping, not the processing trigger. A new upload is picked
/// up the moment the queue notifies the worker's listener; this only fixes the
/// states a queue cannot see — a publish that failed after the row was written,
/// a worker killed mid-processing, abandoned bytes, old rows.
/// </summary>
public sealed class UploadSweeper(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<UploadSweeper> logger) : BackgroundService
{
    // Two cadences because the jobs have nothing in common: a stranded message
    // should not wait minutes, and deleting month-old rows every 30 seconds is
    // pointless work.
    private static readonly TimeSpan RecoveryInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan HousekeepingInterval = TimeSpan.FromHours(1);

    private static readonly TimeSpan AbandonedAfter = TimeSpan.FromHours(24);
    private static readonly TimeSpan StaleUploadedAfter = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan StaleQueuedAfter = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StaleProcessingAfter = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan KeepSucceeded = TimeSpan.FromDays(7);
    private static readonly TimeSpan KeepRejected = TimeSpan.FromDays(30);

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(
            RunAsync(RecoveryInterval, RecoverAsync, stoppingToken),
            RunAsync(HousekeepingInterval, HousekeepAsync, stoppingToken));

    private async Task RunAsync(
        TimeSpan interval,
        Func<CancellationToken, Task> work,
        CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await work(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Upload maintenance failed; trying again in {Interval}.", interval);
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Re-publishes work whose message was never written or never arrived. The
    /// handler is idempotent, so a duplicate costs a re-render that overwrites
    /// content-addressed keys, never a second Photo.
    /// </summary>
    private async Task RecoverAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();

        var contexts = scope.ServiceProvider.GetRequiredService<IDbContextFactory<GalleryDbContext>>();
        var queue = scope.ServiceProvider.GetRequiredService<IUploadQueue>();
        var now = clock.GetUtcNow();

        await using var database = await contexts.CreateDbContextAsync(cancellationToken);

        var stale = await database.Uploads
            .Where(upload =>
                (upload.Status == UploadStatus.Uploaded && upload.UpdatedAt < now - StaleUploadedAfter)
                || (upload.Status == UploadStatus.Queued && upload.UpdatedAt < now - StaleQueuedAfter)
                || (upload.Status == UploadStatus.Processing && upload.UpdatedAt < now - StaleProcessingAfter))
            .OrderBy(upload => upload.UpdatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var upload in stale)
        {
            await queue.PublishAsync(upload.Id, cancellationToken);
            upload.Requeue(now);
        }

        if (stale.Count > 0)
        {
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task HousekeepAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();

        var contexts = scope.ServiceProvider.GetRequiredService<IDbContextFactory<GalleryDbContext>>();
        var store = scope.ServiceProvider.GetRequiredService<IObjectStore>();
        var now = clock.GetUtcNow();

        await using var database = await contexts.CreateDbContextAsync(cancellationToken);

        var abandoned = await database.Uploads
            .Where(upload =>
                (upload.Status == UploadStatus.Pending || upload.Status == UploadStatus.Uploaded)
                && upload.CreatedAt < now - AbandonedAfter)
            .OrderBy(upload => upload.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        foreach (var upload in abandoned)
        {
            // Pending rows usually have no object at all; deleting a missing
            // key is a no-op, which keeps this loop free of special cases.
            await store.DeleteAsync(ObjectBucket.Private, upload.ObjectKey, cancellationToken);
            upload.Expire(now);
        }

        if (abandoned.Count > 0)
        {
            await database.SaveChangesAsync(cancellationToken);
        }

        await database.Uploads
            .Where(upload => upload.Status == UploadStatus.Succeeded
                && upload.CompletedAt < now - KeepSucceeded)
            .ExecuteDeleteAsync(cancellationToken);

        await database.Uploads
            .Where(upload => (upload.Status == UploadStatus.Rejected || upload.Status == UploadStatus.Expired)
                && upload.CompletedAt < now - KeepRejected)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
