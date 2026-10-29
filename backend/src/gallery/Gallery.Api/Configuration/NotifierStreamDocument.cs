using System.Net;
using Microsoft.OpenApi;

namespace Gallery.Api.Configuration;

/// <summary>
/// The admin upload stream is served by Gallery.Notifier. In a cluster the API's
/// Ingress sends this exact path to that Service on the same host, so the route
/// is part of the API's public surface even though this process never sees it.
/// The document is generated from the API, so the hop is declared here; without
/// it the committed contract silently loses the endpoint.
/// </summary>
public static class NotifierStreamDocument
{
    public const string Path = "/api/v1/admin/uploads/events";

    public static void Add(OpenApiDocument document) =>
        document.Paths[Path] = new OpenApiPathItem
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation>
            {
                [HttpMethod.Get] = new OpenApiOperation
                {
                    OperationId = "AdminUploadEvents",
                    Tags = new HashSet<OpenApiTagReference>
                    {
                        new("AdminUploads", document),
                    },
                    Responses = new OpenApiResponses
                    {
                        ["200"] = new OpenApiResponse
                        {
                            Description = "OK",
                            Content = new Dictionary<string, OpenApiMediaType>
                            {
                                ["text/event-stream"] = new OpenApiMediaType(),
                            },
                        },
                    },
                },
            },
        };
}
