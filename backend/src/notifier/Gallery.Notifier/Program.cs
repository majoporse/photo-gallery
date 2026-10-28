using System.Text;
using Gallery.Contracts.Uploads;
using Gallery.Notifier;
using Gallery.Notifier.Admin;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Wolverine;
using Wolverine.Postgresql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<UploadStatusHub>();

// The notifier needs the key, never the password: it validates a session, it
// does not issue one.
var signingKey = builder.Configuration["Auth:SigningKey"];
if (string.IsNullOrWhiteSpace(signingKey))
{
    // Same reason as the API: the container stays constructible without it, and
    // whatever checks readiness is what fails.
    signingKey = "notifier-placeholder-signing-key-32bytes";
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Auth:Issuer"] ?? "photo-gallery",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Auth:Audience"] ?? "photo-gallery-admin",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };

        // The API put the token in a cookie; the browser is not sending headers.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue(AdminCookies.Name, out var token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

var allowedOrigin = builder.Configuration["Cors:AllowedOrigin"];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (!string.IsNullOrWhiteSpace(allowedOrigin))
    {
        policy.WithOrigins(allowedOrigin).AllowCredentials().AllowAnyHeader().AllowAnyMethod();
    }
}));

// Only wired when there is a database to hold the queue. Tests and container
// builds never connect, and neither should start a listener.
var connectionString = builder.Configuration.GetConnectionString("Gallery");

if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.UseWolverine(options =>
    {
        options.UsePostgresqlPersistenceAndTransport(
                connectionString,
                "gallery",
                transportSchema: "wolverine_queues")
            .AutoProvision()
            .EnableQueueNotifications();

        options.ListenToPostgresqlQueue(UploadQueues.Status);

        // Routing is explicit; nothing may be handled in this process by accident.
        options.Policies.DisableConventionalLocalRouting();
    });
}

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/v1/health", () => Results.Ok());
app.MapControllers();

app.Run();

public partial class Program;
