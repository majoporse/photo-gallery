using Gallery.Application.Admin;
using Gallery.Application.Gallery;
using Microsoft.AspNetCore.Mvc;

namespace Gallery.Api.Admin;

[Route("api/v1/admin")]
public sealed class AdminGalleryController(IAdminGallery gallery) : AdminControllerBase
{
    public sealed record OrderRequest(IReadOnlyList<string> Ids);

    [HttpGet("collections")]
    [EndpointName("AdminListCollections")]
    [ProducesResponseType<IReadOnlyList<CollectionAdminView>>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<IReadOnlyList<CollectionAdminView>>> ListCollections(
        CancellationToken cancellationToken) =>
        Ok(await gallery.ListCollectionsAsync(cancellationToken));

    [HttpPost("collections")]
    [Consumes("application/json")]
    [EndpointName("AdminCreateCollection")]
    [ProducesResponseType<CollectionAdminView>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<CollectionAdminView>> CreateCollection(
        [FromBody] CreateCollection command,
        CancellationToken cancellationToken)
    {
        var result = await gallery.CreateCollectionAsync(command, cancellationToken);

        return result.Ok
            ? Created($"/api/v1/admin/collections/{result.Value!.Id}", result.Value)
            : Fail(result.Error!.Value);
    }

    [HttpPatch("collections/{id}")]
    [Consumes("application/json")]
    [EndpointName("AdminUpdateCollection")]
    [ProducesResponseType<CollectionAdminView>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<CollectionAdminView>> UpdateCollection(
        string id,
        [FromBody] UpdateCollection command,
        CancellationToken cancellationToken)
    {
        // Slugs are permanent; refuse the field instead of quietly ignoring it.
        if (command.Slug is not null)
        {
            return ValidationProblemResult(new Dictionary<string, string[]>
            {
                ["slug"] = ["A collection's slug is permanent. Create a new collection instead."],
            });
        }

        var result = await gallery.UpdateCollectionAsync(id, command, cancellationToken);
        return result.Ok ? Ok(result.Value) : Fail(result.Error!.Value);
    }

    [HttpDelete("collections/{id}")]
    [EndpointName("AdminDeleteCollection")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult> DeleteCollection(string id, CancellationToken cancellationToken) =>
        await gallery.DeleteCollectionAsync(id, cancellationToken)
            ? NoContent()
            : Fail(AdminError.NotFound);

    [HttpPost("collections/{id}/restore")]
    [EndpointName("AdminRestoreCollection")]
    [ProducesResponseType<CollectionAdminView>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<CollectionAdminView>> RestoreCollection(
        string id,
        CancellationToken cancellationToken)
    {
        var result = await gallery.RestoreCollectionAsync(id, cancellationToken);
        return result.Ok ? Ok(result.Value) : Fail(result.Error!.Value);
    }

    [HttpPut("collections/order")]
    [Consumes("application/json")]
    [EndpointName("AdminReorderCollections")]
    [ProducesResponseType<IReadOnlyList<CollectionAdminView>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<CollectionAdminView>>> ReorderCollections(
        [FromBody] OrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await gallery.ReorderCollectionsAsync(request.Ids, cancellationToken);
        return result.Ok ? Ok(result.Value) : Fail(result.Error!.Value);
    }

    [HttpPatch("photos/{id}")]
    [Consumes("application/json")]
    [EndpointName("AdminEditPhoto")]
    [ProducesResponseType<PhotoSummary>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PhotoSummary>> UpdatePhoto(
        string id,
        [FromBody] EditPhoto command,
        CancellationToken cancellationToken)
    {
        var result = await gallery.EditPhotoAsync(id, command, cancellationToken);
        return result.Ok ? Ok(result.Value) : Fail(result.Error!.Value);
    }

    [HttpDelete("photos/{id}")]
    [EndpointName("AdminDeletePhoto")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult> DeletePhoto(string id, CancellationToken cancellationToken) =>
        await gallery.DeletePhotoAsync(id, cancellationToken)
            ? NoContent()
            : Fail(AdminError.NotFound);

    [HttpPut("collections/{id}/photos/order")]
    [Consumes("application/json")]
    [EndpointName("AdminReorderPhotos")]
    [ProducesResponseType<IReadOnlyList<PhotoSummary>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<PhotoSummary>>> ReorderPhotos(
        string id,
        [FromBody] OrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await gallery.ReorderPhotosAsync(id, request.Ids, cancellationToken);
        return result.Ok ? Ok(result.Value) : Fail(result.Error!.Value);
    }
}
