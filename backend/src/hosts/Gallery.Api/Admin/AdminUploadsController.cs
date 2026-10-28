using System.Text.Json;
using Gallery.Api.Uploads;
using Gallery.Application.Uploads;
using Microsoft.AspNetCore.Mvc;

namespace Gallery.Api.Admin;

[Route("api/v1/admin")]
public sealed class AdminUploadsController(IUploadService uploads, UploadStreamHub streams) : AdminControllerBase
{
    private static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(20);

    [HttpPost("collections/{id}/uploads")]
    [Consumes("application/json")]
    [EndpointName("AdminBeginUpload")]
    [ProducesResponseType<PresignedUpload>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PresignedUpload>> BeginUpload(
        string id,
        [FromBody] BeginUpload command,
        CancellationToken cancellationToken)
    {
        var result = await uploads.BeginAsync(id, command, cancellationToken);

        return result.Ok
            ? Created($"/api/v1/admin/uploads/{result.Value!.UploadId}", result.Value)
            : Fail(result.Error!.Value);
    }

    [HttpPost("uploads/{id}/complete")]
    [EndpointName("AdminCompleteUpload")]
    [ProducesResponseType<UploadView>(StatusCodes.Status202Accepted, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<UploadView>> CompleteUpload(
        string id,
        CancellationToken cancellationToken)
    {
        var result = await uploads.CompleteAsync(id, cancellationToken);
        return result.Ok ? Accepted(result.Value) : Fail(result.Error!.Value);
    }

    /// <summary>
    /// The stream opens with a snapshot and then carries deltas. Subscribing
    /// before reading the snapshot is what closes the gap between the two: any
    /// event committed during the read is already queued behind it.
    /// </summary>
    [HttpGet("uploads/events")]
    [EndpointName("AdminUploadEvents")]
    [Produces("text/event-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task StreamUploads(CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache, no-transform";
        Response.Headers["X-Accel-Buffering"] = "no";

        using var subscription = streams.Subscribe();

        var snapshot = await uploads.ListActiveAsync(cancellationToken);
        await WriteEventAsync("snapshot", snapshot, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            idle.CancelAfter(Heartbeat);

            try
            {
                var view = await subscription.Reader.ReadAsync(idle.Token);
                await WriteEventAsync("upload.updated", view, cancellationToken);
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

    private async Task WriteEventAsync(string name, object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, UploadJson.Options);

        await Response.WriteAsync($"event: {name}\ndata: {json}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
