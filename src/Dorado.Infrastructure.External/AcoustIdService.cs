using System.Text.Json;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;

namespace Dorado.Infrastructure.External;

/// <summary>
/// AcoustID lookup: fingerprints the file (via <see cref="IFingerprintProvider"/>)
/// and queries the public AcoustID service for the best-scoring recording match.
/// Used at scan time to enrich missing metadata and to detect the same recording
/// filed under multiple paths (acoustic dedup).
/// </summary>
public sealed class AcoustIdService : IAcoustIdService
{
    private readonly Func<AppSettings> _settings;
    private readonly IFingerprintProvider _fingerprints;
    private readonly HttpMessageHandler? _handler;

    public AcoustIdService(
        Func<AppSettings> settings,
        IFingerprintProvider fingerprints,
        HttpMessageHandler? handler = null)
    {
        _settings = settings;
        _fingerprints = fingerprints;
        _handler = handler;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings().AcoustIdApiKey);

    public async Task<AcoustIdMatch?> LookupAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var settings = _settings();
        if (string.IsNullOrWhiteSpace(settings.AcoustIdApiKey) || string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        var fingerprint = await _fingerprints.GetFingerprintAsync(filePath, cancellationToken).ConfigureAwait(false);
        if (fingerprint is null)
        {
            return null;
        }

        var (duration, value) = fingerprint.Value;
        var url =
            "https://api.acoustid.org/v2/lookup" +
            $"?client={Uri.EscapeDataString(settings.AcoustIdApiKey)}" +
            "&meta=recordings+releasegroups" +
            $"&duration={(int)Math.Round(duration)}" +
            $"&fingerprint={Uri.EscapeDataString(value)}";

        try
        {
            using var http = new HttpClient(_handler ?? new SocketsHttpHandler()) { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Dorado/1.0 (+https://github.com/project-dorado/dorado)");
            using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ParseBest(json);
        }
        catch
        {
            return null;
        }
    }

    internal static AcoustIdMatch? ParseBest(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        AcoustIdMatch? best = null;
        var bestScore = -1.0;
        foreach (var result in results.EnumerateArray())
        {
            var score = result.TryGetProperty("score", out var s) && s.TryGetDouble(out var parsed) ? parsed : 0.0;
            if (score < bestScore || !result.TryGetProperty("recordings", out var recordings) || recordings.GetArrayLength() == 0)
            {
                continue;
            }

            var recording = recordings[0];
            var recordingId = recording.TryGetProperty("id", out var id) ? id.GetString() : null;
            var title = recording.TryGetProperty("title", out var t) ? t.GetString() : null;
            var artist = recording.TryGetProperty("artists", out var artists) && artists.GetArrayLength() > 0
                && artists[0].TryGetProperty("name", out var an)
                ? an.GetString()
                : null;
            var album = recording.TryGetProperty("releasegroups", out var groups) && groups.GetArrayLength() > 0
                && groups[0].TryGetProperty("title", out var gt)
                ? gt.GetString()
                : null;

            best = new AcoustIdMatch(recordingId, title, artist, album, (int)Math.Round(score * 100));
            bestScore = score;
        }

        return best;
    }
}
