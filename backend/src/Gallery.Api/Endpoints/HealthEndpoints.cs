using Gallery.Api.Configuration;

namespace Gallery.Api.Endpoints;

public static class HealthEndpoints
{
    public static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/health", () => Results.Ok(new HealthResponse("ok", [])))
            .WithName("GetHealth")
            .WithSummary("Liveness. Answers whenever the process is up.");

        group.MapGet("/health/ready", (IConfiguration configuration) =>
            {
                var missing = RequiredSettings.Missing(configuration);
                return missing.Count == 0
                    ? Results.Ok(new HealthResponse("ready", []))
                    : Results.Json(
                        new HealthResponse("degraded", missing),
                        statusCode: StatusCodes.Status503ServiceUnavailable);
            })
            .WithName("GetReadiness")
            .WithSummary("Readiness. Fails while required configuration is absent.")
            .Produces<HealthResponse>()
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);

        return group;
    }
}

public sealed record HealthResponse(string Status, IReadOnlyList<string> MissingConfiguration);
