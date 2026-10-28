using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gallery.Notifier;

[Route("api/v1/admin/uploads")]
public sealed class UploadStatusStreamController(UploadStatusHub hub) : ControllerBase
{
    private static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(20);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Deltas only. The browser fetches the active list from the API and then
    /// opens this; the notifier has no database and keeps nothing, so there is
    /// no snapshot to send.
    /// </summary>
    [HttpGet("events")]
    [EndpointName("AdminUploadEvents")]
    [Produces("text/event-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Authorize]
    public async Task Stream(CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache, no-transform";
        Response.Headers["X-Accel-Buffering"] = "no";

        using var subscription = hub.Subscribe();

        while (!cancellationToken.IsCancellationRequested)
        {
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            idle.CancelAfter(Heartbeat);

            try
            {
                var view = await subscription.Reader.ReadAsync(idle.Token);

                await Response.WriteAsync(
                    $"event: upload.updated\ndata: {JsonSerializer.Serialize(view, Json)}\n\n",
                    cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // A comment is a legal event payload and keeps proxies from
                // deciding an idle stream is dead.
                await Response.WriteAsync(": keep-alive\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
    }
}
