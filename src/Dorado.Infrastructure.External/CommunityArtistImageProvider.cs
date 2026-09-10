namespace Dorado.Infrastructure.External;

/// <summary>
/// Fallback artist-artwork provider for a user-configured community image host
/// (e.g. a restored `ZuneArtistImages`-style mirror). Clean-room: no assets are
/// bundled; the user points at a base URL and images are resolved as
/// <c>{base}/{artist}.jpg|png</c>.
/// </summary>
public sealed class CommunityArtistImageProvider
{
    private static readonly string[] Extensions = { ".jpg", ".png", ".jpeg" };

    private readonly HttpClient _http;

    public CommunityArtistImageProvider(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient();
    }

    public static IReadOnlyList<string> CandidateUrls(string? baseUrl, string artistName)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(artistName))
        {
            return Array.Empty<string>();
        }

        var root = baseUrl.Trim().TrimEnd('/');
        var name = Uri.EscapeDataString(artistName.Trim());
        return Extensions.Select(extension => $"{root}/{name}{extension}").ToArray();
    }

    public async Task<IReadOnlyList<string>> GetBackgroundUrlsAsync(string? baseUrl, string artistName, CancellationToken cancellationToken = default)
    {
        foreach (var url in CandidateUrls(baseUrl, artistName))
        {
            try
            {
                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType?.StartsWith("image", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return new[] { url };
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
            {
                // Try the next candidate extension.
            }
        }

        return Array.Empty<string>();
    }
}
