using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Dorado.Application.Models;
using Dorado.Infrastructure.External;
using Dorado.Infrastructure.Persistence;
using Xunit;

namespace Dorado.Tests.Application;

public class OnlineEnrichmentTests
{
    private static HttpResponseMessage NotFound() => new(HttpStatusCode.NotFound);

    [Fact]
    public async Task MusicBrainzClient_ParsesArtistSearch()
    {
        var handler = new FakeHttpMessageHandler();
        handler.MapJson(
            u => u.Contains("musicbrainz.org", StringComparison.Ordinal),
            """{"artists":[{"id":"8f5f9b4c-7db3-4a8a-b6d4-6e12c1c2a111","name":"Rush","score":100}]}""");
        var client = new MusicBrainzClient(new HttpClient(handler));

        var result = await client.SearchArtistAsync("Rush", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("8f5f9b4c-7db3-4a8a-b6d4-6e12c1c2a111", result!.Value.MbId);
        Assert.Equal(100, result!.Value.Score);
    }

    [Fact]
    public async Task MusicBrainzClient_SelectsHighestScoringReleaseGroup()
    {
        var handler = new FakeHttpMessageHandler();
        handler.MapJson(
            u => u.Contains("musicbrainz.org", StringComparison.Ordinal),
            """{"release-groups":[{"id":"low-score-rg","title":"Signals (Deluxe)","score":43},{"id":"best-rg","title":"Signals","score":100}]}""");
        var client = new MusicBrainzClient(new HttpClient(handler));

        var result = await client.SearchReleaseGroupAsync("Rush", "Signals", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("best-rg", result!.Value.MbId);
        Assert.Equal("Signals", result!.Value.Title);
    }

    [Fact]
    public async Task FanartTvClient_SkipsRequestWithoutApiKey()
    {
        var handler = new FakeHttpMessageHandler();
        var client = new FanartTvClient(new HttpClient(handler));

        var backgrounds = await client.FetchArtistBackgroundsAsync("some-mbid", string.Empty, CancellationToken.None);

        Assert.Empty(backgrounds);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task FanartTvClient_ParsesBackgrounds()
    {
        var handler = new FakeHttpMessageHandler();
        handler.MapJson(
            u => u.Contains("webservice.fanart.tv", StringComparison.Ordinal),
            """{"name":"Rush","artistbackground":[{"url":"https://assets.fanart.tv/bg1.jpg"},{"url":"https://assets.fanart.tv/bg2.jpg"}]}""");
        var client = new FanartTvClient(new HttpClient(handler));

        var backgrounds = await client.FetchArtistBackgroundsAsync("mbid-1", "test-key", CancellationToken.None);

        Assert.Equal(2, backgrounds.Count);
        Assert.Contains("https://assets.fanart.tv/bg2.jpg", backgrounds);
    }

    [Fact]
    public void StripSyncTimestamps_RemovesLeadingTags()
    {
        const string synced = "[00:12.34]Subdivisions\r\n[00:20.00]In the high school halls";
        var plain = LrcLibClient.StripSyncTimestamps(synced);

        Assert.Equal("Subdivisions\nIn the high school halls", plain);
    }

    [Fact]
    public async Task LrcLibClient_FallsBackToSearchWhenDirectMisses()
    {
        var handler = new FakeHttpMessageHandler();
        handler.MapStatus(u => u.Contains("/api/get", StringComparison.Ordinal), HttpStatusCode.NotFound);
        handler.MapJson(
            u => u.Contains("/api/search", StringComparison.Ordinal),
            """[{"id":1,"trackName":"Obscure Track","artistName":"Rush","plainLyrics":null,"syncedLyrics":"[00:01.00]Lyric line one"}]""");
        var client = new LrcLibClient(new HttpClient(handler));

        var result = await client.FetchLyricsAsync("Rush", "Obscure Track", TimeSpan.FromSeconds(200), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.Found);
        Assert.Equal("Lyric line one", result.PlainLyrics);
    }

    [Fact]
    public async Task ExternalMetadataService_FetchesArtistMetadataWithBiography()
    {
        var handler = new FakeHttpMessageHandler();
        handler.MapJson(
            u => u.Contains("musicbrainz.org", StringComparison.Ordinal),
            """{"artists":[{"id":"mbid-rush","name":"Rush","score":100}]}""");
        handler.MapJson(
            u => u.Contains("en.wikipedia.org", StringComparison.Ordinal),
            """{"type":"standard","title":"Rush (band)","extract":"Rush was a Canadian progressive rock band formed in 1968."}""");
        var service = new ExternalMetadataService(handler, TimeSpan.Zero);

        var metadata = await service.FetchArtistMetadataAsync("Rush");

        Assert.NotNull(metadata);
        Assert.Equal("mbid-rush", metadata!.MusicBrainzId);
        Assert.Contains("progressive rock", metadata.Biography);
        Assert.Equal("Wikipedia", metadata.BiographySource);
    }

    [Fact]
    public async Task ExternalMetadataService_CachesRepeatCalls()
    {
        var handler = new FakeHttpMessageHandler();
        handler.MapJson(
            u => u.Contains("musicbrainz.org", StringComparison.Ordinal),
            """{"artists":[{"id":"mbid-rush","name":"Rush","score":100}]}""");
        handler.MapJson(
            u => u.Contains("en.wikipedia.org", StringComparison.Ordinal),
            """{"type":"standard","extract":"Rush was a Canadian progressive rock band."}""");
        var service = new ExternalMetadataService(handler, TimeSpan.Zero);

        await service.FetchArtistMetadataAsync("Rush");
        var second = await service.FetchArtistMetadataAsync("Rush");

        Assert.NotNull(second);
        var musicBrainzCalls = handler.RequestedUrls.Count(u => u.Contains("musicbrainz.org", StringComparison.Ordinal));
        Assert.Equal(1, musicBrainzCalls);
    }

    [Fact]
    public async Task ExternalMetadataService_BuildsCoverArtUrlForAlbumMatch()
    {
        var handler = new FakeHttpMessageHandler();
        handler.MapJson(
            u => u.Contains("musicbrainz.org", StringComparison.Ordinal),
            """{"release-groups":[{"id":"rg-signals","title":"Signals","score":100}]}""");
        var service = new ExternalMetadataService(handler, TimeSpan.Zero);

        var artwork = await service.FindAlbumArtworkAsync("Rush", "Signals", 1982);

        Assert.NotNull(artwork);
        Assert.Equal("rg-signals", artwork!.MusicBrainzReleaseGroupId);
        Assert.Equal("https://coverartarchive.org/release-group/rg-signals/front-250", artwork.ArtworkUrl);
    }

    [Fact]
    public async Task ExternalMetadataService_FetchesBackgroundUrlsOnlyWithKey()
    {
        var handler = new FakeHttpMessageHandler();
        handler.MapJson(
            u => u.Contains("musicbrainz.org", StringComparison.Ordinal),
            """{"artists":[{"id":"mbid-rush","name":"Rush","score":100}]}""");
        handler.MapJson(
            u => u.Contains("webservice.fanart.tv", StringComparison.Ordinal),
            """{"artistbackground":[{"url":"https://assets.fanart.tv/bg1.jpg"}]}""");
        var service = new ExternalMetadataService(handler, TimeSpan.Zero);

        var withKey = await service.FetchArtistBackgroundUrlsAsync("Rush", "test-key");
        var withoutKey = await service.FetchArtistBackgroundUrlsAsync("Rush", string.Empty);

        Assert.Single(withKey);
        Assert.Empty(withoutKey);
    }

    [Fact]
    public async Task ArtworkCacheService_DownloadsOnceThenServesFromDisk()
    {
        var handler = new FakeHttpMessageHandler();
        var fakeJpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x10, 0x20 };
        handler.MapImage(_ => true, fakeJpeg);
        var tempDir = Path.Combine(Path.GetTempPath(), $"dorado-cache-{Guid.NewGuid():N}");
        try
        {
            var cache = new ArtworkCacheService(handler, tempDir, TimeSpan.Zero);
            const string url = "https://assets.fanart.tv/bg1.jpg";

            var first = await cache.GetOrDownloadAsync(url);
            var second = await cache.GetOrDownloadAsync(url);

            Assert.NotNull(first);
            Assert.True(File.Exists(first));
            Assert.Equal(first, second);
            Assert.Equal(1, handler.RequestCount);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ArtworkCacheService_RejectsNonImagePayloads()
    {
        var handler = new FakeHttpMessageHandler();
        handler.MapJson(_ => true, """{"error":"not found"}""");
        var tempDir = Path.Combine(Path.GetTempPath(), $"dorado-cache-{Guid.NewGuid():N}");
        try
        {
            var cache = new ArtworkCacheService(handler, tempDir, TimeSpan.Zero);

            var path = await cache.GetOrDownloadAsync("https://assets.fanart.tv/missing.jpg");

            Assert.Null(path);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void JsonSettingsStore_RoundTripsAllFields()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"dorado-settings-{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonSettingsStore(tempFile);
            var settings = new AppSettings
            {
                MusicFolderPath = "/home/john/Music",
                CrossfadeEnabled = false,
                CrossfadeDurationSeconds = 4.5,
                LrcLibEnabled = false,
                FanartTvApiKey = "key-123",
                MusicBrainzEnabled = false,
                SelectedAccentName = "Zune Orange"
            };

            store.Save(settings);
            var loaded = store.Load();

            Assert.Equal(settings.MusicFolderPath, loaded.MusicFolderPath);
            Assert.Equal(settings.CrossfadeEnabled, loaded.CrossfadeEnabled);
            Assert.Equal(settings.CrossfadeDurationSeconds, loaded.CrossfadeDurationSeconds);
            Assert.Equal(settings.LrcLibEnabled, loaded.LrcLibEnabled);
            Assert.Equal("key-123", loaded.FanartTvApiKey);
            Assert.False(loaded.MusicBrainzEnabled);
            Assert.Equal("Zune Orange", loaded.SelectedAccentName);

            // Phase 3 parity fields
            loaded.PodcastKeepEpisodes = "Nothing";
            loaded.PodcastAutoDownload = false;
            loaded.IngestExtensions = "mp3,m4a";
            loaded.UsageDataOptIn = true;
            loaded.AutoCheckForUpdates = false;
            loaded.PhotoFolderPath = "/home/john/Pictures";
            loaded.SlideshowShuffle = false;
            loaded.SlideshowRepeat = false;
            loaded.DeletePhotosAfterReverseSync = true;
            loaded.ShowRatings = false;
            loaded.FirstConnectCompletedSerials.Add("ABC123");
            loaded.FirstConnectCompletedSerials.Add("DEF456");

            store.Save(loaded);
            var reloaded = store.Load();

            Assert.Equal("Nothing", reloaded.PodcastKeepEpisodes);
            Assert.False(reloaded.PodcastAutoDownload);
            Assert.Equal("mp3,m4a", reloaded.IngestExtensions);
            Assert.True(reloaded.UsageDataOptIn);
            Assert.False(reloaded.AutoCheckForUpdates);
            Assert.Equal("/home/john/Pictures", reloaded.PhotoFolderPath);
            Assert.False(reloaded.SlideshowShuffle);
            Assert.False(reloaded.SlideshowRepeat);
            Assert.True(reloaded.DeletePhotosAfterReverseSync);
            Assert.False(reloaded.ShowRatings);
            Assert.Equal(new[] { "ABC123", "DEF456" }, reloaded.FirstConnectCompletedSerials);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
