namespace Gallery.Domain.Photos;

/// <summary>
/// A single image in the archive. The uploaded file is the Original; everything
/// visitors see is a Derivative generated from it.
/// </summary>
public sealed class Photo
{
    private Photo()
    {
    }

    public string Id { get; private set; } = string.Empty;
    public string CollectionId { get; private set; } = string.Empty;
    public int Position { get; private set; }

    public string? Title { get; private set; }
    public string? Caption { get; private set; }
    public string? AltText { get; private set; }
    public DateTimeOffset? TakenAt { get; private set; }

    public int Width { get; private set; }
    public int Height { get; private set; }
    public string OriginalKey { get; private set; } = string.Empty;
    public string OriginalSha256 { get; private set; } = string.Empty;
    public string DominantColor { get; private set; } = string.Empty;
    public string ExifJson { get; private set; } = "{}";

    public int? FeaturedRank { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public static Photo Create(
        string collectionId,
        string originalKey,
        string originalSha256,
        int width,
        int height,
        string exifJson,
        string dominantColor,
        DateTimeOffset now,
        int position = 0,
        DateTimeOffset? takenAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalSha256);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegative(position);

        return new Photo
        {
            Id = Ulid.NewUlid().ToString(),
            CollectionId = collectionId,
            Position = position,
            Width = width,
            Height = height,
            OriginalKey = originalKey,
            OriginalSha256 = originalSha256,
            DominantColor = dominantColor,
            ExifJson = string.IsNullOrWhiteSpace(exifJson) ? "{}" : exifJson,
            TakenAt = takenAt,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Describe(string? title, string? caption, string? altText, DateTimeOffset? takenAt, DateTimeOffset now)
    {
        Title = Normalise(title);
        Caption = Normalise(caption);
        AltText = Normalise(altText);
        TakenAt = takenAt;
        UpdatedAt = now;
    }

    /// <summary>Null clears the photo from the home page.</summary>
    public void Feature(int? rank, DateTimeOffset now)
    {
        if (rank is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rank));
        }

        FeaturedRank = rank;
        UpdatedAt = now;
    }

    public void MoveTo(string collectionId, int position, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        ArgumentOutOfRangeException.ThrowIfNegative(position);

        CollectionId = collectionId;
        Position = position;
        UpdatedAt = now;
    }

    public void Reposition(int position, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);

        Position = position;
        UpdatedAt = now;
    }

    public void SoftDelete(DateTimeOffset now)
    {
        if (DeletedAt is not null)
        {
            return;
        }

        DeletedAt = now;
        UpdatedAt = now;
    }

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
