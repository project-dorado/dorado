using System.Net.Http;
using Dorado.Application.Services;
using Dorado.Infrastructure.External;

namespace Dorado.Tests.Application;

public class PodcastFeedParserTests
{
    private const string FeedUrl = "https://feeds.example.com/show.xml";

    private const string Fixture = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0" xmlns:itunes="http://www.itunes.com/dtds/podcast-1.0.dtd" xmlns:media="http://search.yahoo.com/mrss/">
          <channel>
            <title>Test &amp; Show</title>
            <itunes:author>Test Author</itunes:author>
            <description>&lt;p&gt;A &lt;b&gt;great&lt;/b&gt; show&lt;/p&gt;</description>
            <image><url>https://cdn.example.com/art.jpg</url></image>
            <item>
              <title>Episode One</title>
              <description>&lt;p&gt;Hello &amp;amp; welcome&lt;/p&gt;</description>
              <pubDate>Mon, 01 Jan 2024 12:00:00 GMT</pubDate>
              <itunes:duration>1:02:03</itunes:duration>
              <enclosure url="/audio/ep1.mp3" type="audio/mpeg" length="123" />
            </item>
            <item>
              <title>Episode One Duplicate</title>
              <enclosure url="/audio/ep1.mp3" type="audio/mpeg" />
            </item>
            <item>
              <title>Episode Two</title>
              <media:content medium="audio" url="https://cdn.example.com/ep2.mp3" />
              <itunes:duration>3723</itunes:duration>
            </item>
          </channel>
        </rss>
        """;

    [Fact]
    public void Parses_channel_and_normalizes_metadata()
    {
        var series = PodcastFeedParser.Parse(Fixture, FeedUrl);

        Assert.Equal("Test & Show", series.Title);
        Assert.Equal("Test Author", series.Author);
        Assert.Equal("A great show", series.Description);
        Assert.Equal("https://cdn.example.com/art.jpg", series.ArtworkUri);
    }

    [Fact]
    public void Resolves_relative_urls_dedupes_and_reads_media_content()
    {
        var series = PodcastFeedParser.Parse(Fixture, FeedUrl);

        Assert.Equal(2, series.Episodes.Count);
        Assert.Equal(2, series.EpisodeCount);

        var one = series.Episodes.Single(e => e.Title == "Episode One");
        Assert.Equal("https://feeds.example.com/audio/ep1.mp3", one.AudioUrl);
        Assert.Equal(new TimeSpan(1, 2, 3), one.Duration);
        Assert.Equal("Hello & welcome", one.Description);
        Assert.Equal(new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc), one.PublishedAtUtc);

        var two = series.Episodes.Single(e => e.Title == "Episode Two");
        Assert.Equal("https://cdn.example.com/ep2.mp3", two.AudioUrl);
        Assert.Equal(TimeSpan.FromSeconds(3723), two.Duration);
    }

    [Theory]
    [InlineData("3723", 3723)]
    [InlineData("62:03", 3723)]
    [InlineData("1:02:03", 3723)]
    public void Parses_duration_formats(string value, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), PodcastFeedParser.ParseDuration(value));
    }
}

public class PodcastFeedClientTests
{
    [Fact]
    public async Task Follows_html_alternate_link_to_the_real_feed()
    {
        var handler = new FakeHttpMessageHandler();
        handler.MapText(url => url.Contains("patreon.com/x"),
            """<html><head><link rel="alternate" type="application/rss+xml" href="/feed.xml"></head></html>""",
            "text/html");
        handler.MapText(
            url => url.Contains("feed.xml"),
            """<rss version="2.0"><channel><title>Patreon Show</title><item><title>Ep</title><enclosure url="https://cdn/ep.mp3"/></item></channel></rss>""");

        var client = new PodcastFeedClient(new HttpClient(handler));
        var series = await client.GetSeriesAsync("https://patreon.com/x");

        Assert.Equal("Patreon Show", series.Title);
        Assert.Single(series.Episodes);
        Assert.Contains("https://patreon.com/feed.xml", handler.RequestedUrls);
    }

    [Fact]
    public void Discovers_absolute_and_relative_feed_links()
    {
        var relative = PodcastFeedClient.DiscoverFeedLink(
            """<link rel="alternate" type="application/rss+xml" href="/rss">""", "https://site.example/blog");
        var absolute = PodcastFeedClient.DiscoverFeedLink(
            """<link type="application/atom+xml" href="https://cdn.example/feed.atom">""", "https://site.example/blog");

        Assert.Equal("https://site.example/rss", relative);
        Assert.Equal("https://cdn.example/feed.atom", absolute);
        Assert.True(PodcastFeedClient.LooksLikeHtml("<html><body></body></html>"));
    }
}
