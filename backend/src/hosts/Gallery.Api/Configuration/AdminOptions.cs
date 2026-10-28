namespace Gallery.Api.Configuration;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";
    public const string Name = "admin";

    public string Username { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
}
