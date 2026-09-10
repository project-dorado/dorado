using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Dorado.Domain.Models;

namespace Dorado.Application.Services;

/// <summary>
/// Namespace-agnostic RSS/Atom podcast feed parser. Normalizes the real-world quirks
/// legacy importers choke on: iTunes/RSS/Atom namespaces, HTML-laden descriptions,
/// relative enclosure URLs, <c>media:content</c> fallbacks, variable duration formats,
/// and duplicate episodes.
/// </summary>
public static partial class PodcastFeedParser
{
    public static PodcastSeries Parse(string xml, string feedUrl)
    {
        var document = XDocument.Parse(xml);
        var channel = Local(document, "channel").FirstOrDefault() ?? document.Root;
        var series = new PodcastSeries
        {
            FeedUrl = feedUrl,
            Title = Text(channel, "title") ?? "Untitled Podcast",
            Author = Text(channel, "author") ?? Text(channel, "creator") ?? "Unknown Author",
            Description = Clean(Text(channel, "summary") ?? Text(channel, "description")),
            ArtworkUri = ResolveArtwork(channel, feedUrl)
        };

        var seenAudio = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Local(document, "item").Concat(Local(document, "entry")))
        {
            var audioUrl = ResolveAudioUrl(item, feedUrl);
            if (string.IsNullOrWhiteSpace(audioUrl) || !seenAudio.Add(audioUrl))
            {
                continue;
            }

            series.Episodes.Add(new PodcastEpisode
            {
                SeriesId = series.Id,
                SeriesTitle = series.Title,
                Title = Text(item, "title") ?? "Untitled Episode",
                Description = Clean(Text(item, "summary") ?? Text(item, "description") ?? Text(item, "encoded")),
                PublishedAtUtc = ParseDate(Text(item, "pubDate") ?? Text(item, "published") ?? Text(item, "updated")),
                Duration = ParseDuration(Text(item, "duration")),
                AudioUrl = audioUrl,
                IsPlayed = false
            });
        }

        series.Episodes = series.Episodes.OrderByDescending(e => e.PublishedAtUtc).ToList();
        series.EpisodeCount = series.Episodes.Count;
        series.UnplayedCount = series.Episodes.Count(e => !e.IsPlayed);
        return series;
    }

    public static TimeSpan ParseDuration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TimeSpan.FromMinutes(30);
        }

        value = value.Trim();
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        var parts = value.Split(':');
        try
        {
            return parts.Length switch
            {
                3 => new TimeSpan(int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture), int.Parse(parts[2], CultureInfo.InvariantCulture)),
                2 => new TimeSpan(0, int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture)),
                _ => TimeSpan.FromMinutes(30)
            };
        }
        catch (FormatException)
        {
            return TimeSpan.FromMinutes(30);
        }
    }

    private static string? ResolveAudioUrl(XElement item, string feedUrl)
    {
        var enclosure = Local(item, "enclosure").FirstOrDefault();
        var url = enclosure?.Attribute("url")?.Value;

        url ??= Local(item, "content")
            .FirstOrDefault(c => string.Equals(c.Attribute("medium")?.Value, "audio", StringComparison.OrdinalIgnoreCase))
            ?.Attribute("url")?.Value;

        url ??= Local(item, "link")
            .FirstOrDefault(l => string.Equals(l.Attribute("rel")?.Value, "enclosure", StringComparison.OrdinalIgnoreCase))
            ?.Attribute("href")?.Value;

        return string.IsNullOrWhiteSpace(url) ? null : ResolveUrl(feedUrl, url);
    }

    private static string? ResolveArtwork(XElement? channel, string feedUrl)
    {
        if (channel is null)
        {
            return null;
        }

        var image = Local(channel, "image").FirstOrDefault();
        var href = image?.Attribute("href")?.Value ?? Text(image, "url");
        var itunesImage = Local(channel, "image").FirstOrDefault(e => e.Attribute("href") is not null);
        href ??= itunesImage?.Attribute("href")?.Value;

        return string.IsNullOrWhiteSpace(href) ? null : ResolveUrl(feedUrl, href);
    }

    private static string ResolveUrl(string baseUrl, string href)
    {
        if (IsHttpUrl(href, out var absolute))
        {
            return absolute;
        }

        return Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)
            && Uri.TryCreate(baseUri, href, out var combined)
            ? combined.ToString()
            : href;
    }

    private static bool IsHttpUrl(string value, out string absolute)
    {
        absolute = value;
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            absolute = uri.ToString();
            return true;
        }

        return false;
    }

    private static DateTime ParseDate(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed.UtcDateTime;
        }

        return DateTime.UtcNow;
    }

    private static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decoded = WebUtility.HtmlDecode(value);
        var stripped = TagRegex().Replace(decoded, " ");
        return WhitespaceRegex().Replace(stripped, " ").Trim();
    }

    private static IEnumerable<XElement> Local(XContainer? container, string localName)
        => container is null
            ? Enumerable.Empty<XElement>()
            : container.Descendants().Where(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));

    private static string? Text(XContainer? container, string localName)
    {
        if (container is null)
        {
            return null;
        }

        if (container is XElement element && string.Equals(element.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase) && element.HasElements == false)
        {
            return element.Value;
        }

        var found = container.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))
                    ?? container.Descendants().FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));
        return found?.Value;
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
