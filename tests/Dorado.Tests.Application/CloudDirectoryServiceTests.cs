using System.Net;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.Application.Services;
using Dorado.Infrastructure.External;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Covers the cloud directory (Podcast Index / Radio-Browser) bridge and the
/// <see cref="PodcastService.SearchDirectoryAsync"/> delegation.
/// </summary>
public sealed class CloudDirectoryServiceTests
{
    [Fact]
    public async Task SearchPodcasts_ParsesItemsAndBuildsPath()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            return Json(HttpStatusCode.OK,
                """{"query":"tech","total":1,"configured":true,"attribution":"Podcast Index","items":[{"feedId":"f1","title":"Tech Talk","author":"A","description":"d","imageUrl":"","feedUrl":"https://feed","categories":"tech","language":"en"}]}""");
        });
        var service = NewService(handler);

        var results = await service.SearchPodcastsAsync("tech", limit: 5);

        Assert.Single(results);
        Assert.Equal("Tech Talk", results[0].Title);
        Assert.Equal("https://feed", results[0].FeedUrl);
        Assert.NotNull(captured);
        Assert.Contains("q=tech", captured!.RequestUri!.Query);
        Assert.Contains("limit=5", captured.RequestUri.Query);
    }

    [Fact]
    public async Task SearchRadio_ParsesItems()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"query":"jazz","total":1,"configured":true,"attribution":"Radio-Browser","items":[{"stationId":"s1","name":"Jazz FM","url":"https://s","favicon":"","country":"UK","countryCode":"GB","tags":"jazz","codec":"MP3","bitrate":128,"votes":10}]}"""));
        var service = NewService(handler);

        var results = await service.SearchRadioAsync("jazz", tag: "smooth");

        Assert.Single(results);
        Assert.Equal("Jazz FM", results[0].Name);
        Assert.Equal(128, results[0].Bitrate);
    }

    [Fact]
    public async Task Disabled_ReturnsEmpty()
    {
        var service = new CloudDirectoryService(() => new AppSettings());

        Assert.False(service.IsEnabled);
        Assert.Empty(await service.SearchPodcastsAsync("x"));
        Assert.Empty(await service.SearchRadioAsync("x"));
    }

    [Fact]
    public async Task CloudError_ReturnsEmpty()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        var service = NewService(handler);

        Assert.Empty(await service.SearchPodcastsAsync("x"));
    }

    [Fact]
    public async Task PodcastService_SearchDirectory_DelegatesWhenEnabled()
    {
        var directory = new RecordingCloudDirectory { Enabled = true };
        var service = new PodcastService(new PlaybackQueueCoordinator(), feedClient: null, directory: directory);

        var results = await service.SearchDirectoryAsync("tech");

        Assert.Single(results);
        Assert.Equal("recorded", results[0].Title);
    }

    [Fact]
    public async Task PodcastService_SearchDirectory_EmptyWhenDisabled()
    {
        var directory = new RecordingCloudDirectory { Enabled = false };
        var service = new PodcastService(new PlaybackQueueCoordinator(), feedClient: null, directory: directory);

        Assert.Empty(await service.SearchDirectoryAsync("tech"));
    }

    [Fact]
    public async Task PodcastService_SearchDirectory_EmptyWhenNoDirectoryConfigured()
    {
        var service = new PodcastService(new PlaybackQueueCoordinator());

        Assert.Empty(await service.SearchDirectoryAsync("tech"));
    }

    private static CloudDirectoryService NewService(HttpMessageHandler handler)
    {
        var settings = new AppSettings
        {
            CloudEnabled = true,
            CloudBaseUrl = "https://cloud.dorado.example/",
            CloudAccessToken = "tok",
        };

        return new CloudDirectoryService(() => settings, handler);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
}

internal sealed class RecordingCloudDirectory : ICloudDirectoryService
{
    public bool Enabled { get; set; }

    public bool IsEnabled => Enabled;

    public Task<IReadOnlyList<PodcastDirectoryEntry>> SearchPodcastsAsync(
        string query, int limit = 20, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PodcastDirectoryEntry>>(
            new[] { new PodcastDirectoryEntry { Title = "recorded", FeedUrl = "https://feed" } });

    public Task<IReadOnlyList<RadioDirectoryEntry>> SearchRadioAsync(
        string? query = null, string? country = null, string? tag = null,
        int limit = 30, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<RadioDirectoryEntry>>(
            new[] { new RadioDirectoryEntry { Name = "recorded" } });
}
