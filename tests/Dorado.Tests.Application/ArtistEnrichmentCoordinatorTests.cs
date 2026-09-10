using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.Application.Services;
using Dorado.Infrastructure.External;
using Dorado.Infrastructure.Persistence;
using Xunit;

namespace Dorado.Tests.Application;

public class ArtistEnrichmentCoordinatorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;
    private readonly FakeHttpMessageHandler _handler;
    private readonly JsonSettingsStore _settingsStore;

    public ArtistEnrichmentCoordinatorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dorado-enrich-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "settings.json");
        _settingsStore = new JsonSettingsStore(_settingsPath);
        _handler = new FakeHttpMessageHandler();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private ArtistEnrichmentCoordinator CreateCoordinator()
    {
        var metadata = new ExternalMetadataService(_handler, TimeSpan.Zero);
        var cache = new ArtworkCacheService(_handler, Path.Combine(_tempDir, "cache"), TimeSpan.Zero);
        return new ArtistEnrichmentCoordinator(metadata, cache, _settingsStore);
    }

    private static void MapHappyPath(FakeHttpMessageHandler handler)
    {
        handler.MapJson(
            u => u.Contains("musicbrainz.org/ws/2/artist", StringComparison.Ordinal),
            """{"artists":[{"id":"mbid-rush","name":"Rush","score":100}]}""");
        handler.MapJson(
            u => u.Contains("en.wikipedia.org", StringComparison.Ordinal),
            """{"type":"standard","extract":"Rush was a Canadian progressive rock band formed in 1968."}""");
        handler.MapJson(
            u => u.Contains("webservice.fanart.tv", StringComparison.Ordinal),
            """{"artistbackground":[{"url":"https://assets.fanart.tv/rush-bg1.jpg"}]}""");
        handler.MapImage(
            u => u.Contains("assets.fanart.tv", StringComparison.Ordinal),
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x42 });
    }

    [Fact]
    public async Task RequestEnrichment_DoesNothing_WhenDisabled()
    {
        _settingsStore.Save(new AppSettings { MusicBrainzEnabled = false });
        using var coordinator = CreateCoordinator();
        MapHappyPath(_handler);

        var eventFired = new TaskCompletionSource<ArtistEnrichmentSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.EnrichmentCompleted += (_, snapshot) => eventFired.TrySetResult(snapshot);

        coordinator.RequestEnrichment("Rush");
        var completed = await Task.WhenAny(eventFired.Task, Task.Delay(1500)) == eventFired.Task;

        Assert.False(completed);
        Assert.Equal(0, _handler.RequestCount);
    }

    [Fact]
    public async Task RequestEnrichment_CompletesWithBiographyAndBackdrop()
    {
        _settingsStore.Save(new AppSettings
        {
            MusicBrainzEnabled = true,
            AutoFetchMetadata = true,
            AutoDownloadArtistArt = true,
            FanartTvApiKey = "test-key"
        });
        MapHappyPath(_handler);
        using var coordinator = CreateCoordinator();

        var eventFired = new TaskCompletionSource<ArtistEnrichmentSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.EnrichmentCompleted += (_, snapshot) => eventFired.TrySetResult(snapshot);

        coordinator.RequestEnrichment("Rush");
        var winner = await Task.WhenAny(eventFired.Task, Task.Delay(5000));
        var completed = winner == eventFired.Task;

        Assert.True(completed, "EnrichmentCompleted did not fire in time.");
        var snapshot = eventFired.Task.Result;
        Assert.Equal("Rush", snapshot.ArtistName);
        Assert.Contains("progressive rock", snapshot.Biography);
        Assert.Equal("Wikipedia", snapshot.BiographySource);
        Assert.Single(snapshot.BackdropLocalPaths);
        Assert.True(File.Exists(snapshot.BackdropLocalPaths[0]));

        var cached = coordinator.GetCached("rush");
        Assert.NotNull(cached);
        Assert.Equal(snapshot.Biography, cached!.Biography);
    }

    [Fact]
    public async Task RequestEnrichment_SkipsBackdrops_WhenKeyMissing()
    {
        _settingsStore.Save(new AppSettings
        {
            MusicBrainzEnabled = true,
            AutoFetchMetadata = true,
            AutoDownloadArtistArt = true,
            FanartTvApiKey = string.Empty
        });
        MapHappyPath(_handler);
        using var coordinator = CreateCoordinator();

        var eventFired = new TaskCompletionSource<ArtistEnrichmentSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.EnrichmentCompleted += (_, snapshot) => eventFired.TrySetResult(snapshot);

        coordinator.RequestEnrichment("Rush");
        var winner = await Task.WhenAny(eventFired.Task, Task.Delay(5000));

        Assert.True(winner == eventFired.Task, "EnrichmentCompleted did not fire in time.");
        var snapshot = eventFired.Task.Result;
        Assert.NotNull(snapshot.Biography);
        Assert.Empty(snapshot.BackdropLocalPaths);
        Assert.Equal(0, _handler.RequestedUrls.Count(u => u.Contains("fanart.tv", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task GetLyricsAsync_RespectsGatingAndCaches()
    {
        _settingsStore.Save(new AppSettings { LrcLibEnabled = false });
        using var coordinator = CreateCoordinator();

        var gated = await coordinator.GetLyricsAsync("Rush", "Subdivisions", TimeSpan.FromSeconds(334));
        Assert.Null(gated);
        Assert.Equal(0, _handler.RequestCount);

        _settingsStore.Save(new AppSettings
        {
            MusicBrainzEnabled = true,
            AutoFetchMetadata = true,
            LrcLibEnabled = true
        });
        _handler.MapJson(
            u => u.Contains("lrclib.net/api/get", StringComparison.Ordinal),
            """{"id":7,"trackName":"Subdivisions","artistName":"Rush","duration":334,"instrumental":false,"plainLyrics":"Sprawling on the fringes of the city","syncedLyrics":null}""");

        var first = await coordinator.GetLyricsAsync("Rush", "Subdivisions", TimeSpan.FromSeconds(334));
        var second = await coordinator.GetLyricsAsync("Rush", "Subdivisions", TimeSpan.FromSeconds(334));

        Assert.NotNull(first);
        Assert.True(first!.Found);
        Assert.Equal("Sprawling on the fringes of the city", first.PlainLyrics);
        Assert.Same(first, second);
        var lrcLibCalls = _handler.RequestedUrls.Count(u => u.Contains("lrclib.net", StringComparison.Ordinal));
        Assert.Equal(1, lrcLibCalls);
    }
}
