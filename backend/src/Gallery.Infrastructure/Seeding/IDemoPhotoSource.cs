namespace Gallery.Infrastructure.Seeding;

/// <summary>
/// Where demo photographs come from. An interface so tests never touch the
/// network and the source can be swapped without touching the seeder.
/// </summary>
public interface IDemoPhotoSource
{
    /// <summary>Null when this photograph could not be fetched.</summary>
    Task<byte[]?> FetchAsync(int index, CancellationToken cancellationToken);
}

public sealed class PicsumPhotoSource(HttpClient http) : IDemoPhotoSource
{
    public async Task<byte[]?> FetchAsync(int index, CancellationToken cancellationToken)
    {
        try
        {
            // Fixed seeds: a re-seed produces the same set, which keeps screenshots
            // and visual checks comparable.
            using var response = await http.GetAsync(
                $"https://picsum.photos/seed/gallery-{index}/1600/1067",
                cancellationToken);

            return response.IsSuccessStatusCode
                ? await response.Content.ReadAsByteArrayAsync(cancellationToken)
                : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout, not a shutdown.
            return null;
        }
    }
}
