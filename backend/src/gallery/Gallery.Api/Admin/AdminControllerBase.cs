using Gallery.Application.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Gallery.Api.Admin;

/// <summary>
/// Admin routes are authenticated and pass the CSRF guard; only the session
/// actions opt out of authentication, and they still pass the guard.
/// </summary>
[ApiController]
[Authorize]
[ServiceFilter<AdminRequestGuard>]
public abstract class AdminControllerBase : ControllerBase
{
    protected ObjectResult Fail(AdminError error) => error switch
    {
        AdminError.NotFound => Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found"),

        AdminError.SlugTaken => Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "That slug is already in use"),

        AdminError.InvalidSlug => ValidationProblemResult(new Dictionary<string, string[]>
        {
            ["slug"] = ["Lowercase letters, digits and single hyphens, 1-120 characters."],
        }),

        AdminError.InvalidOrder => ValidationProblemResult(new Dictionary<string, string[]>
        {
            ["ids"] = ["Must contain every current id exactly once."],
        }),

        AdminError.InvalidUpload => ValidationProblemResult(new Dictionary<string, string[]>
        {
            ["bytes"] = ["The file must be larger than zero and no more than 50 MB."],
        }),

        AdminError.NotUploaded => Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "The file has not reached storage yet"),

        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };

    protected ObjectResult ValidationProblemResult(Dictionary<string, string[]> errors)
    {
        var modelState = new ModelStateDictionary();

        foreach (var (field, messages) in errors)
        {
            foreach (var message in messages)
            {
                modelState.AddModelError(field, message);
            }
        }

        var problems = HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();

        return new ObjectResult(problems.CreateValidationProblemDetails(HttpContext, modelState))
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" },
        };
    }
}
