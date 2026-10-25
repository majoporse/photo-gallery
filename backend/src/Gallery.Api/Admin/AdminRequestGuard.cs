using Gallery.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Gallery.Api.Admin;

/// <summary>
/// Cookie authentication makes CSRF possible; a custom header that a
/// cross-origin HTML form cannot set closes the gap, and the Origin check
/// catches the case where someone forgets.
/// </summary>
public sealed class AdminRequestGuard(IOptions<CorsOptions> cors) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;

        var isMutation = !HttpMethods.IsGet(request.Method)
            && !HttpMethods.IsHead(request.Method)
            && !HttpMethods.IsOptions(request.Method);

        if (isMutation)
        {
            if (request.Headers[AdminCookies.RequiredHeader] != AdminCookies.RequiredHeaderValue)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: $"Missing {AdminCookies.RequiredHeader}: {AdminCookies.RequiredHeaderValue} header");
            }

            var origin = request.Headers.Origin.ToString();
            var allowed = cors.Value.AllowedOrigin;

            if (!string.IsNullOrEmpty(origin) && !string.Equals(origin, allowed, StringComparison.OrdinalIgnoreCase))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Origin not allowed");
            }
        }

        return await next(context);
    }
}
