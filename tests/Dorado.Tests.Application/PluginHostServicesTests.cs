using System.Text.Json;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;
using Dorado.Plugins.Host;

namespace Dorado.Tests.Application;

public class PluginHostServicesTests
{
    [Fact]
    public async Task Player_services_report_and_drive_playback()
    {
        var player = new FakePlayerCoordinator
        {
            State = PlaybackState.Paused,
            CurrentTrack = new Track { Title = "Voyager", ArtistName = "Daft Punk" }
        };

        var root = Path.Combine(Path.GetTempPath(), "dorado-host-services", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var services = new PluginHostServices(new PluginStorage(Path.Combine(root, "storage")), player: player);

            var stateJson = JsonSerializer.Serialize(await services.HandleAsync("p", "player/getState", null));
            Assert.Contains("Paused", stateJson);
            Assert.Contains("Voyager", stateJson);
            Assert.Contains("Daft Punk", stateJson);

            await services.HandleAsync("p", "player/play", null);
            Assert.Equal(1, player.PlayPauseCalls);

            await services.HandleAsync("p", "player/next", null);
            await services.HandleAsync("p", "player/previous", null);
            Assert.Equal(1, player.NextCalls);
            Assert.Equal(1, player.PreviousCalls);

            var seek = JsonSerializer.Deserialize<JsonElement>("{\"positionMs\":1500}");
            await services.HandleAsync("p", "player/seek", seek);
            Assert.Equal(TimeSpan.FromMilliseconds(1500), player.LastSeek);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* observed */ }
        }
    }

    [Fact]
    public async Task Unknown_service_throws_a_json_rpc_error()
    {
        var root = Path.Combine(Path.GetTempPath(), "dorado-host-services", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var services = new PluginHostServices(new PluginStorage(Path.Combine(root, "storage")));
            await Assert.ThrowsAsync<Dorado.Plugins.Protocol.Rpc.PluginRpcException>(
                () => services.HandleAsync("p", "does/not-exist", null));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* observed */ }
        }
    }
}
