using Gallery.Api.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;

namespace Gallery.Api.Admin;

/// <summary>
/// Cookie authentication makes CSRF possible; a custom header that a
/// cross-origin HTML form cannot set closes the gap, and the Origin check
/// catches the case where someone forgets.
/// </summary>
public sealed class AdminRequestGuard(
    IOptions<CorsOptions> cors,
    ProblemDetailsFactory problems) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;

        var isMutation = !HttpMethods.IsGet(request.Method)
            && !HttpMethods.IsHead(request.Method)
            && !HttpMethods.IsOptions(request.Method);

        if (isMutation)
        {
            if (request.Headers[AdminCookies.RequiredHeader] != AdminCookies.RequiredHeaderValue)
            {
                context.Result = Reject(
                    context,
                    StatusCodes.Status400BadRequest,
                    $"Missing {AdminCookies.RequiredHeader}: {AdminCookies.RequiredHeaderValue} header");
                return;
            }

            var origin = request.Headers.Origin.ToString();
            var allowed = cors.Value.AllowedOrigin;

            if (!string.IsNullOrEmpty(origin) && !string.Equals(origin, allowed, StringComparison.OrdinalIgnoreCase))
            {
                context.Result = Reject(context, StatusCodes.Status403Forbidden, "Origin not allowed");
                return;
            }
        }

        await next();
    }

    private ObjectResult Reject(ActionExecutingContext context, int statusCode, string title) =>
        new(problems.CreateProblemDetails(context.HttpContext, statusCode, title))
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" },
        };
}
