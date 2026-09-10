using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dorado.Plugins.Protocol.Dto;

namespace Dorado.Plugins.LastFm;

/// <summary>Thin Last.fm Web Services client (api_sig signed form posts).</summary>
public sealed class LastFmApi
{
    public const string Endpoint = "https://ws.audioscrobbler.com/2.0/";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly string _sessionKey;

    public LastFmApi(HttpClient http, string apiKey, string apiSecret, string sessionKey)
    {
        _http = http;
        _apiKey = apiKey;
        _apiSecret = apiSecret;
        _sessionKey = sessionKey;
    }

    public static string Sign(IReadOnlyDictionary<string, string> parameters, string apiSecret)
    {
        var builder = new StringBuilder();
        foreach (var pair in parameters
                     .Where(p => p.Key is not ("format" or "callback"))
                     .OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            builder.Append(pair.Key).Append(pair.Value);
        }

        builder.Append(apiSecret);
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public Task SendNowPlayingAsync(TrackDto track, CancellationToken cancellationToken)
        => PostAsync("track.updateNowPlaying", TrackParameters(track), cancellationToken);

    public Task SendScrobbleAsync(TrackDto track, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        var parameters = TrackParameters(track);
        parameters["timestamp"] = startedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        return PostAsync("track.scrobble", parameters, cancellationToken);
    }

    private static Dictionary<string, string> TrackParameters(TrackDto track)
    {
        var parameters = new Dictionary<string, string>
        {
            ["artist"] = track.Artist,
            ["track"] = track.Title
        };

        if (!string.IsNullOrWhiteSpace(track.Album))
        {
            parameters["album"] = track.Album;
        }

        if (track.DurationMs > 0)
        {
            parameters["duration"] = (track.DurationMs / 1000).ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(track.MusicBrainzTrackId))
        {
            parameters["mbid"] = track.MusicBrainzTrackId!;
        }

        return parameters;
    }

    private async Task PostAsync(string method, Dictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        parameters["method"] = method;
        parameters["api_key"] = _apiKey;
        parameters["sk"] = _sessionKey;
        parameters["format"] = "json";
        parameters["api_sig"] = Sign(parameters, _apiSecret);

        using var content = new FormUrlEncodedContent(parameters);
        using var response = await _http.PostAsync(Endpoint, content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}
