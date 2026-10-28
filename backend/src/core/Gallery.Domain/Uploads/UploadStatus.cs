namespace Gallery.Domain.Uploads;

public enum UploadStatus
{
    /// <summary>Bytes have not arrived yet; a presigned URL was issued.</summary>
    Pending,

    /// <summary>The client says the bytes landed, but processing has not been queued.</summary>
    Uploaded,

    /// <summary>Queued for the worker. A sweeper re-publishes it if no answer arrives.</summary>
    Queued,

    Succeeded,

    Rejected,

    /// <summary>The bytes never arrived, or the Upload was abandoned.</summary>
    Expired,
}
