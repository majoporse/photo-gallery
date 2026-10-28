using Gallery.Contracts.Uploads;
using Gallery.Infrastructure.Storage;
using Gallery.Infrastructure.Uploads;
using Gallery.Worker.Processing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Postgresql;

var builder = Host.CreateApplicationBuilder(args);

// A worker needs bytes and a queue. It must never need the admin password, the
// CORS origin, or a connection string for the gallery schema: rendering a JPEG
// is not an authenticated act, and the API owns the rows.
var connectionString = builder.Configuration.GetConnectionString("Gallery")
    ?? throw new InvalidOperationException("ConnectionStrings__Gallery is required.");

builder.Services.AddObjectStorage(builder.Configuration);
builder.Services.AddSingleton<UploadPipeline>();
builder.Services.AddScoped<IUploadResults, WolverineUploadBus>();

builder.UseWolverine(options =>
{
    options.UseUploadQueues(connectionString);
    options.ListenToPostgresqlQueue(UploadTransport.Commands);
});

var host = builder.Build();
await host.RunAsync();
