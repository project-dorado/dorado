using System.Text.Json;

namespace Dorado.Infrastructure.External;

/// <summary>
/// Wikipedia REST API client used to fetch plain-text artist biographies.
/// </summary>
public sealed class WikipediaClient
{
    private readonly HttpClient _http;

    public WikipediaClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    public async Task<string?> FetchSummaryExtractAsync(string pageTitle, CancellationToken cancellationToken)
    {
        var slug = Uri.EscapeDataString(pageTitle.Trim().Replace(' ', '_'));
        var url = $"https://en.wikipedia.org/api/rest_v1/page/summary/{slug}?redirect=true";
        try
        {
            using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("type", out var typeElement) && typeElement.ValueEquals("disambiguation"))
            {
                return null;
            }

            if (root.TryGetProperty("extract", out var extractElement) && extractElement.ValueKind == JsonValueKind.String)
            {
                var extract = extractElement.GetString();
                return string.IsNullOrWhiteSpace(extract) ? null : extract.Trim();
            }

            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            return null;
        }
    }
}
