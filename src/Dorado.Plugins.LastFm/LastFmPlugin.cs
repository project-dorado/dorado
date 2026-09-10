using Dorado.Plugins.Protocol.Dto;
using Dorado.Plugins.Sdk;

namespace Dorado.Plugins.LastFm;

public sealed class LastFmPlugin : PluginBase
{
    private readonly HttpClient _http = new();
    private LastFmApi? _api;
    private TrackDto? _current;
    private DateTimeOffset? _currentStartedAt;

    public override string Id => "com.dorado.lastfm";
    public override string Name => "Last.fm Scrobbler";
    public override string Version => "1.0.0";
    public override string Author => "Dorado";
    public override string Description => "Scrobbles played tracks to Last.fm.";

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        var apiKey = await Context.GetSecureStorageAsync("api_key");
        var apiSecret = await Context.GetSecureStorageAsync("api_secret");
        var sessionKey = await Context.GetSecureStorageAsync("session_key");

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret) || string.IsNullOrWhiteSpace(sessionKey))
        {
            Logger.LogWarning("Last.fm is not configured. Set api_key, api_secret, and session_key in plugin storage.");
        }
        else
        {
            _api = new LastFmApi(_http, apiKey, apiSecret, sessionKey);
            Logger.LogInformation("Last.fm scrobbler ready.");
        }

        Subscribe<TrackChangedDto>("playback/trackChanged", OnTrackChangedAsync);
        Subscribe<PlaybackStateDto>("playback/stateChanged", OnStateChangedAsync);
    }

    private async Task OnTrackChangedAsync(TrackChangedDto dto)
    {
        var now = DateTimeOffset.UtcNow;
        await ScrobbleCurrentIfDueAsync(now).ConfigureAwait(false);

        _current = dto.Track;
        _currentStartedAt = dto.Track is null ? null : now;

        if (_api is not null && dto.Track is not null && dto.IsPlaying)
        {
            await SafeAsync(() => _api.SendNowPlayingAsync(dto.Track, CancellationToken.None)).ConfigureAwait(false);
        }
    }

    private async Task OnStateChangedAsync(PlaybackStateDto dto)
    {
        if (!string.Equals(dto.State, "Playing", StringComparison.OrdinalIgnoreCase))
        {
            await ScrobbleCurrentIfDueAsync(DateTimeOffset.UtcNow).ConfigureAwait(false);
        }
    }

    private async Task ScrobbleCurrentIfDueAsync(DateTimeOffset now)
    {
        if (_api is null || _current is null || _currentStartedAt is null)
        {
            return;
        }

        var elapsed = now - _currentStartedAt.Value;
        var track = _current;
        var startedAt = _currentStartedAt.Value;
        _current = null;
        _currentStartedAt = null;

        if (!ScrobbleRules.ShouldScrobble(TimeSpan.FromMilliseconds(track.DurationMs), elapsed))
        {
            return;
        }

        await SafeAsync(() => _api.SendScrobbleAsync(track, startedAt, CancellationToken.None)).ConfigureAwait(false);
    }

    private async Task SafeAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError("Last.fm request failed.", ex);
        }
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken)
    {
        _http.Dispose();
        return Task.CompletedTask;
    }
}
