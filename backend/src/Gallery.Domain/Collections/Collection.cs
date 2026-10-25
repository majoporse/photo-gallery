namespace Gallery.Domain.Collections;

/// <summary>
/// A named, ordered group of photos. The only grouping concept in the domain:
/// a photo belongs to exactly one collection.
/// </summary>
public sealed class Collection
{
    private Collection()
    {
    }

    public string Id { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int Position { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public static Collection Create(string name, string slug, DateTimeOffset now, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        if (!IsValidSlug(slug))
        {
            throw new ArgumentException(
                "Slug must be lowercase letters, digits and single hyphens, 1-120 characters.",
                nameof(slug));
        }

        return new Collection
        {
            Id = Ulid.NewUlid().ToString(),
            Name = name.Trim(),
            Slug = slug,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Position = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>Slugs are permanent: they are the public URL and may be indexed.</summary>
    public void Rename(string name, string? description, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UpdatedAt = now;
    }

    public void MoveTo(int position, DateTimeOffset now)
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

    public static bool IsValidSlug(string slug) =>
        slug.Length is > 0 and <= 120
        && slug[0] != '-'
        && slug[^1] != '-'
        && !slug.Contains("--", StringComparison.Ordinal)
        && slug.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-');
}
