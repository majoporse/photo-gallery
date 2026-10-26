namespace Gallery.Infrastructure.Seeding;

public sealed class DemoSeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>
    /// Off unless a development profile opts in. A production migrate must never
    /// invent content.
    /// </summary>
    public bool Enabled { get; init; }

    public int Count { get; init; } = 10;

    public string CollectionSlug { get; init; } = "sample";

    public string CollectionName { get; init; } = "Sample";

    public string CollectionDescription { get; init; } =
        "Photographs from Lorem Picsum, added by the development seeder.";
}
