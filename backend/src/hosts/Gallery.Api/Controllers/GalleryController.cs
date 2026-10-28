using Gallery.Application.Gallery;
using Microsoft.AspNetCore.Mvc;

namespace Gallery.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class GalleryController(IPublicGalleryQueries queries) : ControllerBase
{
    [HttpGet("collections")]
    [EndpointName("ListCollections")]
    [ProducesResponseType<PagedResult<CollectionSummary>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PagedResult<CollectionSummary>>> ListCollections(
        [FromQuery] PagingQuery paging,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = paging.Resolve();
        return Ok(await queries.ListCollectionsAsync(page, pageSize, cancellationToken));
    }

    [HttpGet("collections/{slug}")]
    [EndpointName("GetCollection")]
    [ProducesResponseType<CollectionSummary>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<CollectionSummary>> GetCollection(
        string slug,
        CancellationToken cancellationToken)
    {
        var collection = await queries.GetCollectionAsync(slug, cancellationToken);
        return collection is null ? NotFoundProblem("Collection") : Ok(collection);
    }

    [HttpGet("collections/{slug}/photos")]
    [EndpointName("ListCollectionPhotos")]
    [ProducesResponseType<PagedResult<PhotoSummary>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PagedResult<PhotoSummary>>> ListCollectionPhotos(
        string slug,
        [FromQuery] PagingQuery paging,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = paging.Resolve();
        var photos = await queries.ListPhotosAsync(slug, page, pageSize, cancellationToken);
        return photos is null ? NotFoundProblem("Collection") : Ok(photos);
    }

    [HttpGet("photos/{id}")]
    [EndpointName("GetPhoto")]
    [ProducesResponseType<PhotoDetail>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PhotoDetail>> GetPhoto(string id, CancellationToken cancellationToken)
    {
        var photo = await queries.GetPhotoAsync(id, cancellationToken);
        return photo is null ? NotFoundProblem("Photo") : Ok(photo);
    }

    [HttpGet("home")]
    [EndpointName("GetHome")]
    [ProducesResponseType<HomeFeed>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<HomeFeed>> GetHome(CancellationToken cancellationToken) =>
        Ok(await queries.GetHomeAsync(cancellationToken));

    /// <summary>
    /// An out-of-range count is clamped, not rejected: the landing sequence has
    /// no user-facing error to show, and asking for 0 or 500 still has an
    /// obvious answer.
    /// </summary>
    [HttpGet("intro")]
    [EndpointName("GetIntro")]
    [ProducesResponseType<IReadOnlyList<PhotoSummary>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<PhotoSummary>>> GetIntro(
        [FromQuery] int? count,
        CancellationToken cancellationToken) =>
        Ok(await queries.GetIntroAsync(Math.Clamp(count ?? 10, 1, 24), cancellationToken));

    private ObjectResult NotFoundProblem(string what) =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: $"{what} not found");
}
