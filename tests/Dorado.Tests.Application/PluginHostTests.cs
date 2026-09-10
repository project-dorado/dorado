using System.IO.Compression;
using Dorado.Application.Events;
using Dorado.Application.Interfaces;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;
using Dorado.Plugins.Host;
using Dorado.Plugins.Protocol.Dto;
using Dorado.Plugins.Protocol.Rpc;
using Dorado.Plugins.Sdk;

namespace Dorado.Tests.Application;

public class PluginManifestTests
{
    [Fact]
    public void Valid_manifest_has_no_errors()
    {
        var manifest = PluginManifest.Parse("""
            { "id": "com.dorado.discord", "name": "Discord RPC", "version": "1.0.0",
              "author": "Dorado", "description": "presence", "sdkVersion": "1.0",
              "entryPoint": "Dorado.Plugin.Discord.dll" }
            """);

        Assert.Empty(manifest.Validate());
        Assert.Equal("com.dorado.discord", manifest.Id);
    }

    [Fact]
    public void Manifest_flags_missing_fields_and_bad_ids()
    {
        var manifest = PluginManifest.Parse("""{ "id": "../evil", "sdkVersion": "9.9" }""");
        var errors = manifest.Validate();

        Assert.Contains(errors, e => e.Contains("'id'") && e.Contains("letters"));
        Assert.Contains(errors, e => e.Contains("'name'"));
        Assert.Contains(errors, e => e.Contains("'version'"));
        Assert.Contains(errors, e => e.Contains("'entryPoint'"));
        Assert.Contains(errors, e => e.Contains("'sdkVersion'"));
    }
}

public class PluginPackageInstallerTests
{
    [Fact]
    public async Task Installs_valid_package()
    {
        var root = NewTempDirectory();
        try
        {
            var package = Path.Combine(root, "demo.znp");
            CreatePackage(package, new Dictionary<string, string>
            {
                ["plugin.json"] = """{ "id": "test.pkg", "name": "Pkg", "version": "1.0.0", "entryPoint": "Test.dll" }""",
                ["Test.dll"] = "binary"
            });

            var installer = new PluginPackageInstaller(Path.Combine(root, "plugins"));
            var installed = await installer.InstallAsync(package);

            Assert.Equal("test.pkg", installed.Manifest.Id);
            Assert.True(File.Exists(Path.Combine(installed.InstallDirectory, "Test.dll")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Rejects_zip_slip_entries()
    {
        var root = NewTempDirectory();
        try
        {
            var package = Path.Combine(root, "evil.znp");
            CreatePackage(package, new Dictionary<string, string>
            {
                ["plugin.json"] = """{ "id": "test.evil", "name": "Evil", "version": "1.0.0", "entryPoint": "e.dll" }""",
                ["../escape.txt"] = "gotcha"
            });

            var installer = new PluginPackageInstaller(Path.Combine(root, "plugins"));
            await Assert.ThrowsAsync<InvalidDataException>(() => installer.InstallAsync(package));
            Assert.False(File.Exists(Path.Combine(root, "escape.txt")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void CreatePackage(string path, IReadOnlyDictionary<string, string> entries)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            var entry = archive.CreateEntry(name);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }
    }

    private static string NewTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dorado-plugin-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}

public class PluginHostTests
{
    [Fact]
    public async Task Host_starts_plugin_and_routes_events_and_services()
    {
        await using var harness = await PluginHarness.CreateAsync();
        var plugin = harness.Plugin;

        Assert.True(harness.Manager.Plugins.Single().Enabled);
        Assert.True(plugin.IsInitialized);

        // Host services proxied through the plugin context.
        Assert.Equal("abc", plugin.StorageValue);
        Assert.Contains(harness.Toasts, t => t.Title == "hello");
        Assert.Contains(harness.Logs, l => l.Message.Contains("plugin started"));

        // Player event delivery.
        await harness.Manager.DispatchEventAsync("playback/trackChanged", new TrackChangedDto
        {
            Track = new TrackDto { Title = "Subdivisions", Artist = "Rush" },
            IsPlaying = true
        });

        var received = await plugin.TrackChanged.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Subdivisions", received.Track!.Title);
    }

    [Fact]
    public async Task Event_bridge_translates_player_events()
    {
        await using var harness = await PluginHarness.CreateAsync();
        using var bridge = new PluginEventBridge(harness.Player, harness.Manager);
        bridge.Attach();

        harness.Player.CurrentTrack = new Track
        {
            Title = "One More Time",
            ArtistName = "Daft Punk",
            AlbumTitle = "Discovery",
            Duration = TimeSpan.FromSeconds(320),
            Rating = HeartRating.Favorite
        };
        harness.Player.State = PlaybackState.Playing;
        harness.Player.RaiseTrack(harness.Player.CurrentTrack);

        var trackEvent = await harness.Plugin.TrackChanged.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("One More Time", trackEvent.Track!.Title);
        Assert.Equal("Favorite", trackEvent.Track!.Rating);
        Assert.True(trackEvent.IsPlaying);

        harness.Player.RaiseRating(Guid.NewGuid(), HeartRating.Dislike);
        var ratingEvent = await harness.Plugin.RatingChanged.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Dislike", ratingEvent.Rating);
    }

    [Fact]
    public async Task Enabled_state_persists_across_reload()
    {
        var root = Path.Combine(Path.GetTempPath(), "dorado-plugin-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var options = new PluginManagerOptions
            {
                PluginsDirectory = Path.Combine(root, "plugins"),
                ConfigDirectory = Path.Combine(root, "config")
            };
            WriteManifest(options.PluginsDirectory, "test.persist", "Persist");

            var manager = new PluginManager(options, _ => new NoopTransport(), new PluginHostServices(new PluginStorage(Path.Combine(root, "storage"))));
            manager.LoadInstalled();
            await manager.SetEnabledAsync("test.persist", true);

            var reloaded = new PluginManager(options, _ => new NoopTransport(), new PluginHostServices(new PluginStorage(Path.Combine(root, "storage"))));
            reloaded.LoadInstalled();

            Assert.True(reloaded.Plugins.Single().Enabled);
            await manager.DisposeAsync();
            await reloaded.DisposeAsync();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    internal static void WriteManifest(string pluginsDirectory, string id, string name)
    {
        var dir = Path.Combine(pluginsDirectory, id);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, PluginManifest.FileName), $$"""
            { "id": "{{id}}", "name": "{{name}}", "version": "1.0.0", "entryPoint": "Test.dll" }
            """);
    }
}

/// <summary>In-memory plugin harness: runs a <see cref="TestPlugin"/> over the real JSON-RPC host path.</summary>
internal sealed class PluginHarness : IAsyncDisposable
{
    private readonly string _root;
    private readonly CancellationTokenSource _runtimeCts = new();
    private readonly ILineTransport _pluginTransport;
    private Task _runtimeTask = Task.CompletedTask;

    private PluginHarness(string root, ILineTransport pluginTransport)
    {
        _root = root;
        _pluginTransport = pluginTransport;
    }

    public TestPlugin Plugin { get; } = new();

    public FakePlayerCoordinator Player { get; } = new();

    public PluginManager Manager { get; private set; } = null!;

    public List<(string Id, string Level, string Message)> Logs { get; } = new();

    public List<(string Title, string Message)> Toasts { get; } = new();

    public static async Task<PluginHarness> CreateAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "dorado-plugin-tests", Guid.NewGuid().ToString("N"));
        var pluginsDirectory = Path.Combine(root, "plugins");
        var configDirectory = Path.Combine(root, "config");
        Directory.CreateDirectory(root);

        PluginHostTests.WriteManifest(pluginsDirectory, "test.bridge", "Bridge");

        var (hostTransport, pluginTransport) = InMemoryLineTransport.CreatePair();
        var harness = new PluginHarness(root, pluginTransport);

        var storage = new PluginStorage(Path.Combine(root, "storage"));
        var services = new PluginHostServices(
            storage,
            library: null,
            logSink: (id, level, message) => harness.Logs.Add((id, level, message)),
            toastSink: (title, message) => harness.Toasts.Add((title, message)));

        harness.Manager = new PluginManager(
            new PluginManagerOptions { PluginsDirectory = pluginsDirectory, ConfigDirectory = configDirectory },
            _ => hostTransport,
            services);

        harness._runtimeTask = PluginRuntime.RunAsync(harness.Plugin, pluginTransport, harness._runtimeCts.Token);

        harness.Manager.LoadInstalled();
        await harness.Manager.SetEnabledAsync("test.bridge", true);
        await harness.Plugin.InitializedTask.WaitAsync(TimeSpan.FromSeconds(5));
        return harness;
    }

    public async ValueTask DisposeAsync()
    {
        await Manager.DisposeAsync();
        _runtimeCts.Cancel();
        try { await _runtimeTask; } catch (OperationCanceledException) { }
        await _pluginTransport.DisposeAsync();
        _runtimeCts.Dispose();

        try { Directory.Delete(_root, recursive: true); } catch { /* observed */ }
    }
}

internal sealed class TestPlugin : PluginBase
{
    public override string Id => "test.bridge";
    public override string Name => "Bridge Test";
    public override string Version => "1.0.0";
    public override string Author => "tests";
    public override string Description => "test plugin";

    public bool IsInitialized { get; private set; }
    public string? StorageValue { get; private set; }

    public TaskCompletionSource InitializedTcs { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<TrackChangedDto> TrackChanged { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<RatingChangedDto> RatingChanged { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task InitializedTask => InitializedTcs.Task;

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        Subscribe<TrackChangedDto>("playback/trackChanged", dto =>
        {
            TrackChanged.TrySetResult(dto);
            return Task.CompletedTask;
        });
        Subscribe<RatingChangedDto>("rating/changed", dto =>
        {
            RatingChanged.TrySetResult(dto);
            return Task.CompletedTask;
        });

        Logger.LogInformation("plugin started");

        await Context.SetSecureStorageAsync("token", "abc");
        StorageValue = await Context.GetSecureStorageAsync("token");
        await Context.ShowToastAsync("hello", "world");

        IsInitialized = true;
        InitializedTcs.TrySetResult();
    }
}

/// <summary>Minimal IPlayerCoordinator used to drive the event bridge in tests.</summary>
internal sealed class FakePlayerCoordinator : IPlayerCoordinator
{
    public PlaybackState State { get; set; } = PlaybackState.Stopped;
    public Track? CurrentTrack { get; set; }
    public TimeSpan CurrentPosition { get; set; }
    public TimeSpan Duration => CurrentTrack?.Duration ?? TimeSpan.Zero;
    public double Volume { get; set; } = 0.5;
    public bool IsMuted { get; set; }
    public bool Shuffle { get; set; }
    public bool Repeat { get; set; }
    public double CrossfadeDurationSeconds { get; set; }
    public bool IsCrossfading => false;
    public bool GaplessEnabled { get; set; } = true;
    public bool VolumeLevelingEnabled { get; set; } = true;
    public bool IsSimulatedPlayback => false;
    public IReadOnlyList<Track> Queue => Array.Empty<Track>();

    public Task PlayTrackAsync(Track track, IEnumerable<Track>? contextQueue = null) => Task.CompletedTask;
    public Task PlayPauseAsync() => Task.CompletedTask;
    public Task StopAsync() => Task.CompletedTask;
    public Task NextAsync() => Task.CompletedTask;
    public Task PreviousAsync() => Task.CompletedTask;
    public Task SeekAsync(TimeSpan position) => Task.CompletedTask;
    public Task SetRatingAsync(Guid trackId, HeartRating rating) => Task.CompletedTask;
    public void Enqueue(IEnumerable<Track> tracks) { }
    public void PlayNext(IEnumerable<Track> tracks) { }

#pragma warning disable CS0067
    public event EventHandler<TrackChangedEventArgs>? TrackChanged;
    public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;
    public event EventHandler<HeartRatingChangedEventArgs>? RatingChanged;
#pragma warning restore CS0067

    public void RaiseTrack(Track track)
        => TrackChanged?.Invoke(this, new TrackChangedEventArgs(track, CurrentPosition));

    public void RaiseRating(Guid trackId, HeartRating rating)
        => RatingChanged?.Invoke(this, new HeartRatingChangedEventArgs(trackId, rating));
}

internal sealed class NoopTransport : ILineTransport
{
    public bool IsRunning => false;
#pragma warning disable CS0067
    public event EventHandler<int?>? Exited;
#pragma warning restore CS0067
    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task WriteLineAsync(string line, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<string?> ReadLineAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    public Task StopAsync() => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
