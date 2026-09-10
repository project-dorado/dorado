using System.Text.Json;

namespace Dorado.Infrastructure.External;

/// <summary>
/// Fanart.tv Web Service v3 client for high-resolution artist background imagery (requires a free personal API key).
/// </summary>
public sealed class FanartTvClient
{
    private readonly HttpClient _http;

    public FanartTvClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    public async Task<List<string>> FetchArtistBackgroundsAsync(string artistMbid, string apiKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(artistMbid) || string.IsNullOrWhiteSpace(apiKey))
        {
            return new List<string>();
        }

        var url = $"https://webservice.fanart.tv/v3/music/{Uri.EscapeDataString(artistMbid)}?api_key={Uri.EscapeDataString(apiKey)}";
        try
        {
            using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new List<string>();
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            var backgrounds = new List<string>();
            if (document.RootElement.TryGetProperty("artistbackground", out var backgroundElement) && backgroundElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in backgroundElement.EnumerateArray())
                {
                    if (item.TryGetProperty("url", out var urlElement) && urlElement.ValueKind == JsonValueKind.String && urlElement.GetString() is { Length: > 0 } imageUrl)
                    {
                        backgrounds.Add(imageUrl);
                    }
                }
            }

            return backgrounds;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            return new List<string>();
        }
    }
}
