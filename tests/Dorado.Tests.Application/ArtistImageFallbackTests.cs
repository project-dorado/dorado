using System.Net;
using System.Net.Http;
using Dorado.Infrastructure.External;

namespace Dorado.Tests.Application;

public class CommunityArtistImageProviderTests
{
    [Fact]
    public void Candidate_urls_normalize_and_escape()
    {
        var urls = CommunityArtistImageProvider.CandidateUrls("https://cdn.example/art/", "Daft Punk");

        Assert.Contains("https://cdn.example/art/Daft%20Punk.jpg", urls);
        Assert.Contains("https://cdn.example/art/Daft%20Punk.png", urls);
        Assert.Empty(CommunityArtistImageProvider.CandidateUrls("", "Rush"));
        Assert.Empty(CommunityArtistImageProvider.CandidateUrls("https://cdn.example/art", "  "));
    }

    [Fact]
    public async Task Falls_back_to_the_next_extension_when_jpg_missing()
    {
        var handler = new FakeHttpMessageHandler();
        handler.MapStatus(url => url.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase), HttpStatusCode.NotFound);
        handler.MapImage(url => url.EndsWith(".png", StringComparison.OrdinalIgnoreCase), new byte[] { 1, 2, 3 });

        var provider = new CommunityArtistImageProvider(new HttpClient(handler));
        var urls = await provider.GetBackgroundUrlsAsync("https://cdn.example/art", "Rush");

        Assert.Single(urls);
        Assert.EndsWith("Rush.png", urls[0]);
    }

    [Fact]
    public async Task Returns_empty_when_nothing_matches()
    {
        var handler = new FakeHttpMessageHandler();
        handler.MapStatus(_ => true, HttpStatusCode.NotFound);

        var provider = new CommunityArtistImageProvider(new HttpClient(handler));
        var urls = await provider.GetBackgroundUrlsAsync("https://cdn.example/art", "Nobody");

        Assert.Empty(urls);
    }

    [Fact]
    public async Task Missing_configuration_returns_empty_without_requests()
    {
        var handler = new FakeHttpMessageHandler();
        var provider = new CommunityArtistImageProvider(new HttpClient(handler));

        Assert.Empty(await provider.GetBackgroundUrlsAsync(null, "Rush"));
        Assert.Equal(0, handler.RequestCount);
    }
}
