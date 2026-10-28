using Gallery.Application.Uploads;
using Gallery.Contracts.Uploads;
using Microsoft.AspNetCore.Mvc;

namespace Gallery.Api.Admin;

[Route("api/v1/admin")]
public sealed class AdminUploadsController(IUploadService uploads) : AdminControllerBase
{
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

    /// <summary>
    /// Everything in flight, oldest first. The stream carries only deltas, so the
    /// page reads this once and again after every reconnect.
    /// </summary>
    [HttpGet("uploads")]
    [EndpointName("AdminActiveUploads")]
    [ProducesResponseType<IReadOnlyList<UploadView>>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<IReadOnlyList<UploadView>>> ListUploads(CancellationToken cancellationToken) =>
        Ok(await uploads.ListActiveAsync(cancellationToken));

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
}
