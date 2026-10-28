using Gallery.Api.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace Gallery.Api.Controllers;

[ApiController]
[Route("api/v1/health")]
public sealed class HealthController(IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    [EndpointName("GetHealth")]
    [EndpointSummary("Liveness. Answers whenever the process is up.")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK, "application/json")]
    public ActionResult<HealthResponse> GetHealth() => Ok(new HealthResponse("ok", []));

    [HttpGet("ready")]
    [EndpointName("GetReadiness")]
    [EndpointSummary("Readiness. Fails while required configuration is absent.")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status503ServiceUnavailable, "application/json")]
    public ActionResult<HealthResponse> GetReadiness()
    {
        var missing = RequiredSettings.Missing(configuration);

        return missing.Count == 0
            ? Ok(new HealthResponse("ready", []))
            : new ObjectResult(new HealthResponse("degraded", missing))
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable,
            };
    }
}

public sealed record HealthResponse(string Status, IReadOnlyList<string> MissingConfiguration);
