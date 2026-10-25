using System.Security.Claims;
using Gallery.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Gallery.Api.Admin;

public static class AdminSessionEndpoints
{
    public sealed record LoginRequest(string Username, string Password);

    public sealed record SessionResponse(string Username);

    public static RouteGroupBuilder MapAdminSessionEndpoints(this RouteGroupBuilder admin)
    {
        admin.MapPost("/session", (
                LoginRequest request,
                AdminSessionService sessions,
                IOptions<AuthOptions> auth,
                HttpContext context) =>
            {
                var token = sessions.IssueToken(request.Username, request.Password);

                if (token is null)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Invalid credentials");
                }

                context.Response.Cookies.Append(
                    AdminCookies.Name,
                    token,
                    AdminCookies.Create(auth.Value.Lifetime, auth.Value.CookieSecure));

                return Results.NoContent();
            })
            .WithName("AdminLogin")
            .AllowAnonymous()
            .RequireRateLimiting("login")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        admin.MapDelete("/session", (HttpContext context) =>
            {
                context.Response.Cookies.Delete(AdminCookies.Name, new CookieOptions { Path = "/" });
                return Results.NoContent();
            })
            .WithName("AdminLogout")
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent);

        admin.MapGet("/session", (ClaimsPrincipal principal, IOptions<AdminOptions> options) =>
                Results.Ok(new SessionResponse(options.Value.Username)))
            .WithName("AdminSession")
            .Produces<SessionResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return admin;
    }
}
