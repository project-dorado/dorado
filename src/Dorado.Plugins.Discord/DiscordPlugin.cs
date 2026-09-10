using Dorado.Plugins.Protocol.Dto;
using Dorado.Plugins.Sdk;

namespace Dorado.Plugins.Discord;

public sealed class DiscordPlugin : PluginBase
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DiscordIpcClient? _client;
    private CancellationTokenSource? _loopCts;
    private TrackDto? _current;
    private bool _isPlaying;
    private long? _startedAtMs;

    public override string Id => "com.dorado.discord";
    public override string Name => "Discord Rich Presence";
    public override string Version => "1.0.0";
    public override string Author => "Dorado";
    public override string Description => "Displays the current track on Discord.";

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        var clientId = await Context.GetSecureStorageAsync("client_id");
        if (string.IsNullOrWhiteSpace(clientId))
        {
            Logger.LogWarning("Discord is not configured. Set 'client_id' in plugin storage.");
        }
        else
        {
            _client = new DiscordIpcClient(clientId);
        }

        Subscribe<TrackChangedDto>("playback/trackChanged", OnTrackChangedAsync);
        Subscribe<PlaybackStateDto>("playback/stateChanged", OnStateChangedAsync);

        _loopCts = new CancellationTokenSource();
        _ = RunLoopAsync(_loopCts.Token);
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_client is not null)
            {
                if (!_client.IsConnected)
                {
                    try
                    {
                        if (await _client.TryConnectAsync(cancellationToken).ConfigureAwait(false))
                        {
                            await PushAsync(cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning("Discord connect failed: {0}", ex.Message);
                    }
                }
                else
                {
                    await PushAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task OnTrackChangedAsync(TrackChangedDto dto)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _current = dto.Track;
            _isPlaying = dto.IsPlaying;
            _startedAtMs = dto.Track is null ? null : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
        finally
        {
            _gate.Release();
        }

        await PushAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task OnStateChangedAsync(PlaybackStateDto dto)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _isPlaying = string.Equals(dto.State, "Playing", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            _gate.Release();
        }

        await PushAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task PushAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var client = _client;
            if (client is null || !client.IsConnected)
            {
                return;
            }

            var activity = DiscordActivityBuilder.Build(_current, _isPlaying, _startedAtMs);
            await client.SendActivityAsync(DiscordActivityBuilder.BuildSetActivity(activity, Environment.ProcessId), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        _loopCts?.Cancel();

        var client = _client;
        if (client is not null)
        {
            await client.SendActivityAsync(DiscordActivityBuilder.BuildSetActivity(null, Environment.ProcessId), cancellationToken).ConfigureAwait(false);
            await client.DisposeAsync().ConfigureAwait(false);
        }
    }
}
