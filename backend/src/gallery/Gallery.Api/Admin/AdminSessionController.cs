using Gallery.Api.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Gallery.Api.Admin;

[Route("api/v1/admin/session")]
public sealed class AdminSessionController(
    AdminSessionService sessions,
    IOptions<AuthOptions> auth,
    IOptions<AdminOptions> admin) : AdminControllerBase
{
    public sealed record LoginRequest(string Username, string Password);

    public sealed record SessionResponse(string Username);

    [HttpPost]
    [Consumes("application/json")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [EndpointName("AdminLogin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public ActionResult Login([FromBody] LoginRequest request)
    {
        var token = sessions.IssueToken(request.Username, request.Password);

        if (token is null)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");
        }

        Response.Cookies.Append(
            AdminCookies.Name,
            token,
            AdminCookies.Create(auth.Value.Lifetime, auth.Value.CookieSecure));

        return NoContent();
    }

    [HttpDelete]
    [AllowAnonymous]
    [EndpointName("AdminLogout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult Logout()
    {
        Response.Cookies.Delete(AdminCookies.Name, new CookieOptions { Path = "/" });
        return NoContent();
    }

    [HttpGet]
    [EndpointName("AdminSession")]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public ActionResult<SessionResponse> Session() =>
        Ok(new SessionResponse(admin.Value.Username));
}
