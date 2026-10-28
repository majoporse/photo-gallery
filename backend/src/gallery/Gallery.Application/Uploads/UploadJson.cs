using System.Text.Json;
using Gallery.Contracts.Uploads;

namespace Gallery.Application.Uploads;

/// <summary>
/// The wire format shared by the writer that notifies and the API that relays.
/// Property names and enum values must not drift between those two processes.
/// </summary>
public static class UploadJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(UploadView view) => JsonSerializer.Serialize(view, Options);

    public static UploadView? Deserialize(string json) =>
        JsonSerializer.Deserialize<UploadView>(json, Options);
}
