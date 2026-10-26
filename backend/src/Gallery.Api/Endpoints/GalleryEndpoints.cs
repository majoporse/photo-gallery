using Gallery.Application.Gallery;

namespace Gallery.Api.Endpoints;

public static class GalleryEndpoints
{
    private const int DefaultPageSize = 24;
    private const int MaxPageSize = 100;

    public static RouteGroupBuilder MapGalleryEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/collections", async (
                IPublicGalleryQueries queries,
                int? page,
                int? pageSize,
                CancellationToken cancellationToken) =>
            {
                if (!TryPage(page, pageSize, out var paging, out var problem))
                {
                    return problem;
                }

                return Results.Ok(await queries.ListCollectionsAsync(paging.Page, paging.PageSize, cancellationToken));
            })
            .WithName("ListCollections")
            .Produces<PagedResult<CollectionSummary>>()
            .ProducesValidationProblem();

        group.MapGet("/collections/{slug}", async (
                string slug,
                IPublicGalleryQueries queries,
                CancellationToken cancellationToken) =>
            {
                var collection = await queries.GetCollectionAsync(slug, cancellationToken);
                return collection is null ? NotFound("Collection") : Results.Ok(collection);
            })
            .WithName("GetCollection")
            .Produces<CollectionSummary>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/collections/{slug}/photos", async (
                string slug,
                IPublicGalleryQueries queries,
                int? page,
                int? pageSize,
                CancellationToken cancellationToken) =>
            {
                if (!TryPage(page, pageSize, out var paging, out var problem))
                {
                    return problem;
                }

                var photos = await queries.ListPhotosAsync(slug, paging.Page, paging.PageSize, cancellationToken);
                return photos is null ? NotFound("Collection") : Results.Ok(photos);
            })
            .WithName("ListCollectionPhotos")
            .Produces<PagedResult<PhotoSummary>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapGet("/photos/{id}", async (
                string id,
                IPublicGalleryQueries queries,
                CancellationToken cancellationToken) =>
            {
                var photo = await queries.GetPhotoAsync(id, cancellationToken);
                return photo is null ? NotFound("Photo") : Results.Ok(photo);
            })
            .WithName("GetPhoto")
            .Produces<PhotoDetail>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/home", async (IPublicGalleryQueries queries, CancellationToken cancellationToken) =>
                Results.Ok(await queries.GetHomeAsync(cancellationToken)))
            .WithName("GetHome")
            .Produces<HomeFeed>();

        group.MapGet("/intro", async (
                IPublicGalleryQueries queries,
                int? count,
                CancellationToken cancellationToken) =>
            {
                var take = Math.Clamp(count ?? 10, 1, 24);
                return Results.Ok(await queries.GetIntroAsync(take, cancellationToken));
            })
            .WithName("GetIntro")
            .Produces<IReadOnlyList<PhotoSummary>>()
            .ProducesValidationProblem();

        return group;
    }

    private static IResult NotFound(string what) =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: $"{what} not found");

    private static bool TryPage(int? page, int? pageSize, out Paging paging, out IResult problem)
    {
        paging = new Paging(page ?? 1, pageSize ?? DefaultPageSize);
        problem = Results.Empty;

        var errors = new Dictionary<string, string[]>();

        if (paging.Page < 1)
        {
            errors["page"] = ["page must be 1 or greater."];
        }

        if (paging.PageSize is < 1 or > MaxPageSize)
        {
            errors["pageSize"] = [$"pageSize must be between 1 and {MaxPageSize}."];
        }

        if (errors.Count == 0)
        {
            return true;
        }

        problem = Results.ValidationProblem(errors);
        return false;
    }

    private readonly record struct Paging(int Page, int PageSize);
}
