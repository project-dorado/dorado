using System.Text.Json;

namespace Dorado.Infrastructure.External;

/// <summary>
/// Minimal MusicBrainz Web Service v2 client (search endpoints only, JSON format).
/// </summary>
public sealed class MusicBrainzClient
{
    private readonly HttpClient _http;

    public MusicBrainzClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    public async Task<(string? MbId, int Score)?> SearchArtistAsync(string artistName, CancellationToken cancellationToken)
    {
        var url = $"https://musicbrainz.org/ws/2/artist/?query=artist:%22{Uri.EscapeDataString(artistName)}%22&fmt=json&limit=1";
        using var document = await GetJsonDocumentAsync(url, cancellationToken).ConfigureAwait(false);
        if (document == null)
        {
            return null;
        }

        if (!document.RootElement.TryGetProperty("artists", out var artists) || artists.GetArrayLength() == 0)
        {
            return null;
        }

        var first = artists[0];
        if (!first.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var score = 0;
        if (first.TryGetProperty("score", out var scoreElement) && scoreElement.TryGetInt32(out var parsedScore))
        {
            score = parsedScore;
        }

        return (idElement.GetString(), score);
    }

    public async Task<(string? MbId, string? Title, int Score)?> SearchReleaseGroupAsync(string artistName, string albumTitle, CancellationToken cancellationToken)
    {
        var query = $"releasegroup:%22{Uri.EscapeDataString(albumTitle)}%22%20AND%20artist:%22{Uri.EscapeDataString(artistName)}%22";
        var url = $"https://musicbrainz.org/ws/2/release-group/?query={query}&fmt=json&limit=5";
        using var document = await GetJsonDocumentAsync(url, cancellationToken).ConfigureAwait(false);
        if (document == null)
        {
            return null;
        }

        if (!document.RootElement.TryGetProperty("release-groups", out var groups) || groups.GetArrayLength() == 0)
        {
            return null;
        }

        (string MbId, string Title, int Score) best = default;
        foreach (var group in groups.EnumerateArray())
        {
            var id = group.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String ? idElement.GetString() : null;
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            var title = group.TryGetProperty("title", out var titleElement) && titleElement.ValueKind == JsonValueKind.String ? titleElement.GetString() : null;
            var score = group.TryGetProperty("score", out var scoreElement) && scoreElement.TryGetInt32(out var parsedScore) ? parsedScore : 0;

            if (best.MbId == null || score > best.Score)
            {
                best = (id, title ?? string.Empty, score);
            }
        }

        return best.MbId == null ? null : best;
    }

    public async Task<(string? MbId, string? Title, string? ArtistName, long? LengthMs, int Score)?> SearchRecordingAsync(string artistName, string trackTitle, CancellationToken cancellationToken)
    {
        var query = $"recording:%22{Uri.EscapeDataString(trackTitle)}%22%20AND%20artist:%22{Uri.EscapeDataString(artistName)}%22";
        var url = $"https://musicbrainz.org/ws/2/recording/?query={query}&fmt=json&limit=3";
        using var document = await GetJsonDocumentAsync(url, cancellationToken).ConfigureAwait(false);
        if (document == null)
        {
            return null;
        }

        if (!document.RootElement.TryGetProperty("recordings", out var recordings) || recordings.GetArrayLength() == 0)
        {
            return null;
        }

        (string MbId, string Title, string ArtistName, long? LengthMs, int Score) best = default;
        foreach (var recording in recordings.EnumerateArray())
        {
            var id = recording.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String ? idElement.GetString() : null;
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            var title = recording.TryGetProperty("title", out var titleElement) && titleElement.ValueKind == JsonValueKind.String ? titleElement.GetString() : null;
            var length = recording.TryGetProperty("length", out var lengthElement) && lengthElement.TryGetInt64(out var ms) ? ms : (long?)null;
            var score = recording.TryGetProperty("score", out var scoreElement) && scoreElement.TryGetInt32(out var parsedScore) ? parsedScore : 0;
            var creditArtistName = string.Empty;
            if (recording.TryGetProperty("artist-credit", out var credit) && credit.GetArrayLength() > 0)
            {
                var first = credit[0];
                if (first.TryGetProperty("artist", out var artistElement) && artistElement.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String)
                {
                    creditArtistName = nameElement.GetString() ?? string.Empty;
                }
            }

            if (best.MbId == null || score > best.Score)
            {
                best = (id, title ?? string.Empty, creditArtistName, length, score);
            }
        }

        return best.MbId == null ? null : best;
    }

    /// <summary>Resolves an artist's related artists (collaborations, band membership) by name.</summary>
    public async Task<IReadOnlyList<string>> LookupRelatedArtistsAsync(
        string artistName,
        int limit,
        CancellationToken cancellationToken)
    {
        var seed = await SearchArtistAsync(artistName, cancellationToken).ConfigureAwait(false);
        if (seed?.MbId is not { Length: > 0 } mbid)
        {
            return Array.Empty<string>();
        }

        var url = $"https://musicbrainz.org/ws/2/artist/{Uri.EscapeDataString(mbid)}?inc=artist-rels&fmt=json";
        using var document = await GetJsonDocumentAsync(url, cancellationToken).ConfigureAwait(false);
        if (document == null || !document.RootElement.TryGetProperty("relations", out var relations))
        {
            return Array.Empty<string>();
        }

        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { artistName };
        foreach (var relation in relations.EnumerateArray())
        {
            if (!relation.TryGetProperty("artist", out var artist)
                || !artist.TryGetProperty("name", out var nameElement)
                || nameElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var name = nameElement.GetString();
            if (!string.IsNullOrWhiteSpace(name) && seen.Add(name))
            {
                names.Add(name);
                if (names.Count >= limit)
                {
                    break;
                }
            }
        }

        return names;
    }

    private async Task<JsonDocument?> GetJsonDocumentAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return JsonDocument.Parse(json);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            return null;
        }
    }
}
