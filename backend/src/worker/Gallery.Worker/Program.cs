using Gallery.Contracts.Uploads;
using Gallery.Worker.Application.Processing;
using Gallery.Worker.Infrastructure.Messaging;
using Gallery.Worker.Infrastructure.Storage;
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
builder.Services.AddScoped<IUploadResults, WolverineUploadResults>();

builder.UseWolverine(options =>
{
    options.UseUploadQueues(connectionString);
    options.ListenToPostgresqlQueue(UploadTransport.Commands);

    // [WolverineHandler] marks a type, not an assembly: the handler lives in
    // Gallery.Worker.Application, so this host has to say the assembly exists.
    // The attribute and this line are the whole registration; there is no
    // naming convention to rename out from under the queue.
    options.Discovery.IncludeAssembly(typeof(ProcessUploadHandler).Assembly);
});

var host = builder.Build();
await host.RunAsync();
