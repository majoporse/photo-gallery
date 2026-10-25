using Gallery.Application.Admin;

namespace Gallery.Api.Endpoints;

public static class AdminGalleryEndpoints
{
    public sealed record OrderRequest(IReadOnlyList<string> Ids);

    public static RouteGroupBuilder MapAdminGalleryEndpoints(this RouteGroupBuilder admin)
    {
        admin.MapGet("/collections", async (IAdminGallery gallery, CancellationToken cancellationToken) =>
                Results.Ok(await gallery.ListCollectionsAsync(cancellationToken)))
            .WithName("AdminListCollections")
            .Produces<IReadOnlyList<CollectionAdminView>>();

        admin.MapPost("/collections", async (
                CreateCollection command,
                IAdminGallery gallery,
                CancellationToken cancellationToken) =>
            {
                var result = await gallery.CreateCollectionAsync(command, cancellationToken);

                return result.Ok
                    ? Results.Created($"/api/v1/admin/collections/{result.Value!.Id}", result.Value)
                    : Fail(result.Error!.Value);
            })
            .WithName("AdminCreateCollection")
            .Produces<CollectionAdminView>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        admin.MapPatch("/collections/{id}", async (
                string id,
                UpdateCollection command,
                IAdminGallery gallery,
                CancellationToken cancellationToken) =>
            {
                // Slugs are permanent; refuse the field instead of quietly ignoring it.
                if (command.Slug is not null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["slug"] = ["A collection's slug is permanent. Create a new collection instead."],
                    });
                }

                var result = await gallery.UpdateCollectionAsync(id, command, cancellationToken);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error!.Value);
            })
            .WithName("AdminUpdateCollection")
            .Produces<CollectionAdminView>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        admin.MapDelete("/collections/{id}", async (
                string id,
                IAdminGallery gallery,
                CancellationToken cancellationToken) =>
                await gallery.DeleteCollectionAsync(id, cancellationToken)
                    ? Results.NoContent()
                    : Fail(AdminError.NotFound))
            .WithName("AdminDeleteCollection")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPut("/collections/order", async (
                OrderRequest request,
                IAdminGallery gallery,
                CancellationToken cancellationToken) =>
            {
                var result = await gallery.ReorderCollectionsAsync(request.Ids, cancellationToken);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error!.Value);
            })
            .WithName("AdminReorderCollections")
            .Produces<IReadOnlyList<CollectionAdminView>>()
            .ProducesValidationProblem();

        admin.MapPost("/collections/{id}/photos", async (
                string id,
                HttpRequest request,
                IAdminGallery gallery,
                CancellationToken cancellationToken) =>
            {
                if (!request.HasFormContentType)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status415UnsupportedMediaType,
                        title: "Uploads must be multipart/form-data.");
                }

                var form = await request.ReadFormAsync(cancellationToken);
                if (form.Files.Count == 0)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["files"] = ["At least one file is required."],
                    });
                }

                var files = form.Files
                    .Select(file => new UploadFile(file.FileName, file.Length, file.OpenReadStream()))
                    .ToList();

                try
                {
                    var result = await gallery.AddPhotosAsync(id, files, cancellationToken);

                    return result.Ok
                        ? Results.Created($"/api/v1/collections/{id}/photos", result.Value)
                        : Fail(result.Error!.Value);
                }
                finally
                {
                    foreach (var file in files)
                    {
                        await file.Content.DisposeAsync();
                    }
                }
            })
            .WithName("AdminUploadPhotos")
            .DisableAntiforgery()
            .Produces<UploadBatch>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        admin.MapPatch("/photos/{id}", async (
                string id,
                EditPhoto command,
                IAdminGallery gallery,
                CancellationToken cancellationToken) =>
            {
                var result = await gallery.EditPhotoAsync(id, command, cancellationToken);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error!.Value);
            })
            .WithName("AdminEditPhoto")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapDelete("/photos/{id}", async (
                string id,
                IAdminGallery gallery,
                CancellationToken cancellationToken) =>
                await gallery.DeletePhotoAsync(id, cancellationToken)
                    ? Results.NoContent()
                    : Fail(AdminError.NotFound))
            .WithName("AdminDeletePhoto")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPut("/collections/{id}/photos/order", async (
                string id,
                OrderRequest request,
                IAdminGallery gallery,
                CancellationToken cancellationToken) =>
            {
                var result = await gallery.ReorderPhotosAsync(id, request.Ids, cancellationToken);
                return result.Ok ? Results.Ok(result.Value) : Fail(result.Error!.Value);
            })
            .WithName("AdminReorderPhotos")
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        return admin;
    }

    private static IResult Fail(AdminError error) => error switch
    {
        AdminError.NotFound => Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not found"),

        AdminError.SlugTaken => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "That slug is already in use"),

        AdminError.InvalidSlug => Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["slug"] = ["Lowercase letters, digits and single hyphens, 1-120 characters."],
        }),

        AdminError.InvalidOrder => Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["ids"] = ["Must contain every current id exactly once."],
        }),

        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
    };
}
