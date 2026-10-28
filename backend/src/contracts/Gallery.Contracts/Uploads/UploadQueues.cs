namespace Gallery.Contracts.Uploads;

/// <summary>
/// The three queues both services have to agree on. They are the wire, so they
/// live next to the messages instead of being written out once per host.
/// </summary>
public static class UploadQueues
{
    /// <summary>API to worker: render this.</summary>
    public const string Commands = "uploads";

    /// <summary>Worker to API: here is the answer.</summary>
    public const string Results = "upload-results";

    /// <summary>API to notifier: this is what the owner should see.</summary>
    public const string Status = "upload-status";
}
