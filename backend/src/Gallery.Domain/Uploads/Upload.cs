namespace Gallery.Domain.Uploads;

/// <summary>
/// An attempt by the Admin to add a Photo. It is deliberately not a Photo: the
/// bytes may never arrive, may fail validation, or may never finish processing.
/// A Photo is only created from an Upload that succeeded.
/// </summary>
public sealed class Upload
{
    public const int MaxAttempts = 3;

    private Upload()
    {
    }

    public string Id { get; private set; } = string.Empty;
    public string CollectionId { get; private set; } = string.Empty;

    /// <summary>The id the Photo will receive if this Upload succeeds.</summary>
    public string PhotoId { get; private set; } = string.Empty;

    public string FileName { get; private set; } = string.Empty;
    public string ObjectKey { get; private set; } = string.Empty;

    /// <summary>What the client said it would send; the stored object is re-measured.</summary>
    public long DeclaredBytes { get; private set; }

    /// <summary>Reserved when the Upload is created, so ordering follows upload order.</summary>
    public int Position { get; private set; }

    public UploadStatus Status { get; private set; }
    public UploadRejection? Rejection { get; private set; }
    public string? Failure { get; private set; }
    public int Attempts { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public bool IsTerminal =>
        Status is UploadStatus.Succeeded or UploadStatus.Rejected or UploadStatus.Expired;

    public static Upload Create(
        string collectionId,
        string photoId,
        string fileName,
        string objectKey,
        long declaredBytes,
        int position,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(photoId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(declaredBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(position);

        return new Upload
        {
            Id = Ulid.NewUlid().ToString(),
            CollectionId = collectionId,
            PhotoId = photoId,
            FileName = fileName,
            ObjectKey = objectKey,
            DeclaredBytes = declaredBytes,
            Position = position,
            Status = UploadStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void MarkUploaded(DateTimeOffset now)
    {
        if (Status is not UploadStatus.Pending)
        {
            return;
        }

        Status = UploadStatus.Uploaded;
        UpdatedAt = now;
    }

    public void Queue(DateTimeOffset now)
    {
        if (Status is not (UploadStatus.Uploaded or UploadStatus.Pending))
        {
            return;
        }

        Status = UploadStatus.Queued;
        UpdatedAt = now;
    }

    public void StartProcessing(DateTimeOffset now)
    {
        if (IsTerminal)
        {
            return;
        }

        Status = UploadStatus.Processing;
        UpdatedAt = now;
    }

    public void Succeed(DateTimeOffset now)
    {
        Status = UploadStatus.Succeeded;
        Rejection = null;
        Failure = null;
        CompletedAt = now;
        UpdatedAt = now;
    }

    public void Reject(UploadRejection reason, DateTimeOffset now)
    {
        Status = UploadStatus.Rejected;
        Rejection = reason;
        CompletedAt = now;
        UpdatedAt = now;
    }

    public void Expire(DateTimeOffset now)
    {
        if (IsTerminal)
        {
            return;
        }

        Status = UploadStatus.Expired;
        CompletedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Returns work whose message was lost back to the queue.</summary>
    public void Requeue(DateTimeOffset now)
    {
        if (IsTerminal)
        {
            return;
        }

        Status = UploadStatus.Queued;
        UpdatedAt = now;
    }

    /// <summary>Records a retryable failure; returns true when no attempt remains.</summary>
    public bool RecordFailure(string message, DateTimeOffset now)
    {
        Attempts += 1;
        Failure = message.Length > 1000 ? message[..1000] : message;
        UpdatedAt = now;

        return Attempts >= MaxAttempts;
    }
}
