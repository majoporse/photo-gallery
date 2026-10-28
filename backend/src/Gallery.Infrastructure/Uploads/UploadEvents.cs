using Gallery.Application.Uploads;
using Gallery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gallery.Infrastructure.Uploads;

/// <summary>
/// Postgres is the fan-out spine: the writer notifies on commit, every API
/// replica holds one listener and relays to its own event streams. No broker,
/// and no event can be observed before it is durable.
/// </summary>
public static class UploadEvents
{
    public const string Channel = "upload_status";

    public static async Task PublishAsync(
        GalleryDbContext database,
        UploadView view,
        CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlRawAsync(
            "SELECT pg_notify('upload_status', {0})",
            new object[] { UploadJson.Serialize(view) },
            cancellationToken);
    }
}
