using System.Text.Json;
using Dorado.Application.Interfaces;
using Dorado.Domain.Enums;
using Dorado.Plugins.Protocol.Rpc;

namespace Dorado.Plugins.Host;

/// <summary>
/// Implements the host-side services plugins may invoke: logging, secure storage,
/// library queries, and toasts.
/// </summary>
public sealed class PluginHostServices
{
    private readonly IMediaLibraryService? _library;
    private readonly IPlayerCoordinator? _player;
    private readonly PluginStorage _storage;
    private readonly Action<string, string, string>? _logSink;
    private readonly Action<string, string>? _toastSink;

    public PluginHostServices(
        PluginStorage storage,
        IMediaLibraryService? library = null,
        IPlayerCoordinator? player = null,
        Action<string, string, string>? logSink = null,
        Action<string, string>? toastSink = null)
    {
        _storage = storage;
        _library = library;
        _player = player;
        _logSink = logSink;
        _toastSink = toastSink;
    }

    public async Task<object?> HandleAsync(string pluginId, string method, JsonElement? parameters)
    {
        switch (method)
        {
            case "logger/log":
            {
                var level = GetString(parameters, "level") ?? "info";
                var message = GetString(parameters, "message") ?? string.Empty;
                _logSink?.Invoke(pluginId, level, message);
                return new { ok = true };
            }

            case "storage/get":
            {
                var key = GetString(parameters, "key") ?? string.Empty;
                var value = await _storage.GetAsync(pluginId, key).ConfigureAwait(false);
                return new { value };
            }

            case "storage/set":
            {
                var key = GetString(parameters, "key") ?? string.Empty;
                var value = GetString(parameters, "value") ?? string.Empty;
                await _storage.SetAsync(pluginId, key, value).ConfigureAwait(false);
                return new { ok = true };
            }

            case "library/queryTracks":
            {
                if (_library is null)
                {
                    return new { tracks = Array.Empty<object>() };
                }

                var query = GetString(parameters, "query") ?? string.Empty;
                var tracks = await _library.SearchAsync(query).ConfigureAwait(false);
                return new
                {
                    tracks = tracks.Take(50).Select(track => new
                    {
                        id = track.Id,
                        title = track.Title,
                        artist = track.ArtistName,
                        album = track.AlbumTitle,
                        durationMs = (long)track.Duration.TotalMilliseconds,
                        rating = RatingName(track.Rating),
                        artworkUri = track.ArtworkUri,
                        musicBrainzTrackId = track.MusicBrainzTrackId,
                        musicBrainzArtistId = track.MusicBrainzArtistId
                    }).ToArray()
                };
            }

            case "ui/showToast":
            {
                var title = GetString(parameters, "title") ?? "Plugin";
                var message = GetString(parameters, "message") ?? string.Empty;
                _toastSink?.Invoke(title, message);
                return new { ok = true };
            }

            case "player/getState":
                return new
                {
                    state = _player?.State.ToString() ?? "Stopped",
                    isPlaying = _player?.State == PlaybackState.Playing,
                    title = _player?.CurrentTrack?.Title,
                    artist = _player?.CurrentTrack?.ArtistName,
                    positionMs = (long)(_player?.CurrentPosition.TotalMilliseconds ?? 0)
                };

            case "player/play":
                if (_player is { State: not PlaybackState.Playing })
                {
                    await _player.PlayPauseAsync().ConfigureAwait(false);
                }

                return new { ok = true };

            case "player/pause":
                if (_player is { State: PlaybackState.Playing })
                {
                    await _player.PlayPauseAsync().ConfigureAwait(false);
                }

                return new { ok = true };

            case "player/next":
                if (_player is not null) await _player.NextAsync().ConfigureAwait(false);
                return new { ok = true };

            case "player/previous":
                if (_player is not null) await _player.PreviousAsync().ConfigureAwait(false);
                return new { ok = true };

            case "player/seek":
            {
                var positionMs = GetLong(parameters, "positionMs") ?? 0;
                if (_player is not null) await _player.SeekAsync(TimeSpan.FromMilliseconds(positionMs)).ConfigureAwait(false);
                return new { ok = true };
            }

            default:
                throw new PluginRpcException(-32601, $"Method not found: {method}");
        }
    }

    private static string? GetString(JsonElement? parameters, string name)
    {
        if (parameters is { } element && element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value))
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Null => null,
                _ => value.ToString()
            };
        }

        return null;
    }

    private static long? GetLong(JsonElement? parameters, string name)
    {
        if (parameters is { } element && element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value))
        {
            return value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetInt64(out var number) => number,
                JsonValueKind.String when long.TryParse(value.GetString(), out var parsed) => parsed,
                _ => null
            };
        }

        return null;
    }

    private static string RatingName(HeartRating rating) => rating switch
    {
        HeartRating.Favorite => "Favorite",
        HeartRating.Dislike => "Dislike",
        _ => "None"
    };
}
