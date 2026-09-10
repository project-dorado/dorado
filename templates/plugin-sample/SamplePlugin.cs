using Dorado.Plugins.Protocol.Dto;
using Dorado.Plugins.Sdk;

namespace Dorado.Plugin.Sample;

public sealed class SamplePlugin : PluginBase
{
    public override string Id => "com.example.sample";
    public override string Name => "Sample Plugin";
    public override string Version => "1.0.0";
    public override string Author => "You";
    public override string Description => "Logs playback events.";

    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Sample plugin starting.");

        Subscribe<TrackChangedDto>("playback/trackChanged", dto =>
        {
            Logger.LogInformation("Now playing: {0} — {1}", dto.Track?.Artist ?? "?", dto.Track?.Title ?? "?");
            return Task.CompletedTask;
        });

        Subscribe<PlaybackStateDto>("playback/stateChanged", dto =>
        {
            Logger.LogInformation("State: {0}", dto.State);
            return Task.CompletedTask;
        });

        return Task.CompletedTask;
    }
}
