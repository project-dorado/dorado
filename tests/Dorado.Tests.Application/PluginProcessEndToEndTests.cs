using Dorado.Plugins.Host;
using Dorado.Plugins.Protocol.Dto;

namespace Dorado.Tests.Application;

/// <summary>
/// End-to-end over the real process boundary: spawns the built reference plugin
/// executables and exercises the actual stdio JSON-RPC handshake and event delivery
/// (the in-memory harness covers logic; this covers the transport itself).
/// </summary>
public class PluginProcessEndToEndTests
{
    [Theory]
    [InlineData("Dorado.Plugins.LastFm")]
    [InlineData("Dorado.Plugins.Discord")]
    public async Task Reference_plugin_process_handshakes_and_receives_events(string project)
    {
        var directory = FindPluginDirectory(project);
        if (directory is null)
        {
            // Plugin not built in this configuration; the CI Release run covers it.
            return;
        }

        var manifest = PluginManifest.Load(Path.Combine(directory, "plugin.json"));
        var installed = new InstalledPlugin { Manifest = manifest, InstallDirectory = directory };

        var root = Path.Combine(Path.GetTempPath(), "dorado-plugin-e2e", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var services = new PluginHostServices(new PluginStorage(Path.Combine(root, "storage")));
        var running = new RunningPlugin(
            installed,
            new ProcessPluginTransport(installed),
            "1.0.0",
            (method, parameters) => services.HandleAsync(manifest.Id, method, parameters));

        try
        {
            await running.StartAsync();
            Assert.Equal(PluginStatus.Running, running.Status);

            await running.SendEventAsync("playback/trackChanged", new TrackChangedDto
            {
                Track = new TrackDto { Title = "Subdivisions", Artist = "Rush" },
                IsPlaying = true
            });
            await running.SendEventAsync("playback/stateChanged", new PlaybackStateDto { State = "Playing", PositionMs = 0 });

            await Task.Delay(300);

            // The plugin survived both events and the channel is still healthy.
            Assert.Equal(PluginStatus.Running, running.Status);
        }
        finally
        {
            await running.StopAsync();
            await running.DisposeAsync();
            try { Directory.Delete(root, recursive: true); } catch { /* observed */ }
        }
    }

    private static string? FindPluginDirectory(string project)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Dorado.sln")))
        {
            root = root.Parent;
        }

        if (root is null)
        {
            return null;
        }

        foreach (var configuration in new[] { "Release", "Debug" })
        {
            var candidate = Path.Combine(root.FullName, "src", project, "bin", configuration, "net8.0");
            if (File.Exists(Path.Combine(candidate, "plugin.json"))
                && File.Exists(Path.Combine(candidate, project + ".dll")))
            {
                return candidate;
            }
        }

        return null;
    }
}
