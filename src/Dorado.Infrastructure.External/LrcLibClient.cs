using System.Text.Json;
using Dorado.Application.Models;

namespace Dorado.Infrastructure.External;

/// <summary>
/// LRCLIB client (lrclib.net): free, keyless lookup of synced and plain lyrics.
/// </summary>
public sealed class LrcLibClient
{
    private readonly HttpClient _http;

    public LrcLibClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    public async Task<LyricsResult?> FetchLyricsAsync(string artistName, string trackTitle, TimeSpan? duration, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(artistName) || string.IsNullOrWhiteSpace(trackTitle))
        {
            return null;
        }

        var getUrl = $"https://lrclib.net/api/get?artist_name={Uri.EscapeDataString(artistName.Trim())}&track_name={Uri.EscapeDataString(trackTitle.Trim())}";
        if (duration is { TotalSeconds: > 0 } value)
        {
            getUrl += $"&duration={(int)Math.Round(value.TotalSeconds)}";
        }

        var direct = await FetchSingleAsync(getUrl, cancellationToken).ConfigureAwait(false);
        if (direct is { Found: true })
        {
            return direct;
        }

        var searchUrl = $"https://lrclib.net/api/search?q={Uri.EscapeDataString($"{trackTitle.Trim()} {artistName.Trim()}")}";
        var candidates = await FetchSearchResultsAsync(searchUrl, cancellationToken).ConfigureAwait(false);
        return candidates.FirstOrDefault(candidate => candidate.Found);
    }

    private async Task<LyricsResult?> FetchSingleAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            return ParseLyrics(document.RootElement);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    private async Task<List<LyricsResult>> FetchSearchResultsAsync(string url, CancellationToken cancellationToken)
    {
        var results = new List<LyricsResult>();
        try
        {
            using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return results;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in document.RootElement.EnumerateArray())
                {
                    var parsed = ParseLyrics(element);
                    if (parsed != null)
                    {
                        results.Add(parsed);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
        }

        return results;
    }

    private static LyricsResult ParseLyrics(JsonElement root)
    {
        var result = new LyricsResult
        {
            TrackName = root.TryGetProperty("trackName", out var trackElement) && trackElement.ValueKind == JsonValueKind.String ? trackElement.GetString() ?? string.Empty : string.Empty,
            ArtistName = root.TryGetProperty("artistName", out var artistElement) && artistElement.ValueKind == JsonValueKind.String ? artistElement.GetString() ?? string.Empty : string.Empty,
            SyncedLyrics = root.TryGetProperty("syncedLyrics", out var syncedElement) && syncedElement.ValueKind == JsonValueKind.String ? syncedElement.GetString() : null,
            PlainLyrics = root.TryGetProperty("plainLyrics", out var plainElement) && plainElement.ValueKind == JsonValueKind.String ? plainElement.GetString() : null
        };

        if (string.IsNullOrWhiteSpace(result.PlainLyrics) && !string.IsNullOrWhiteSpace(result.SyncedLyrics))
        {
            result.PlainLyrics = StripSyncTimestamps(result.SyncedLyrics);
        }

        return result;
    }

    public static string StripSyncTimestamps(string syncedLyrics)
    {
        var lines = syncedLyrics.Split('\n');
        var cleaned = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            var content = line;
            // Strip one or more leading [mm:ss.xx] tags
            int index = 0;
            while (index < content.Length && content[index] == '[')
            {
                var closing = content.IndexOf(']', index);
                if (closing < 0)
                {
                    break;
                }

                var tag = content.Substring(index + 1, closing - index - 1);
                // Only treat as timestamp tags of the form mm:ss(.xx); keep other bracketed metadata as-is
                if (tag.Length >= 4 && char.IsDigit(tag[0]) && tag.Contains(':'))
                {
                    content = content.Substring(closing + 1);
                    index = 0;
                }
                else
                {
                    break;
                }
            }

            content = content.TrimEnd('\r');
            if (content.Length > 0)
            {
                cleaned.Add(content);
            }
        }

        return string.Join("\n", cleaned);
    }
}
