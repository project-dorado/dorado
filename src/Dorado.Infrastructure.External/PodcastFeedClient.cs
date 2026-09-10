using System.Text.RegularExpressions;
using Dorado.Application.Interfaces;
using Dorado.Application.Services;
using Dorado.Domain.Models;

namespace Dorado.Infrastructure.External;

/// <summary>
/// Fetches podcast feeds and normalizes common hosting quirks. Some providers
/// (Patreon, Anchor, personal sites) return an HTML landing page at the feed URL
/// whose <c>&lt;link rel="alternate" type="application/rss+xml"&gt;</c> points at the
/// real feed; this client follows that indirection before parsing.
/// </summary>
public sealed partial class PodcastFeedClient : IPodcastFeedClient
{
    private readonly HttpClient _http;

    public PodcastFeedClient(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient();
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Dorado/1.0 (+https://github.com/project-dorado/dorado)");
        }
    }

    public async Task<PodcastSeries> GetSeriesAsync(string feedUrl, CancellationToken cancellationToken = default)
    {
        var xml = await _http.GetStringAsync(feedUrl, cancellationToken).ConfigureAwait(false);

        if (LooksLikeHtml(xml))
        {
            var discovered = DiscoverFeedLink(xml, feedUrl);
            if (discovered is not null)
            {
                xml = await _http.GetStringAsync(discovered, cancellationToken).ConfigureAwait(false);
            }
        }

        return PodcastFeedParser.Parse(xml, feedUrl);
    }

    public static bool LooksLikeHtml(string content)
        => content.TrimStart().StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase)
           || content.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase)
           || content.Contains("<rss", StringComparison.OrdinalIgnoreCase) == false
              && content.Contains("<feed", StringComparison.OrdinalIgnoreCase) == false
              && content.Contains("<channel", StringComparison.OrdinalIgnoreCase) == false;

    public static string? DiscoverFeedLink(string html, string baseUrl)
    {
        foreach (Match match in LinkRegex().Matches(html))
        {
            var tag = match.Value;
            if (!tag.Contains("application/rss+xml", StringComparison.OrdinalIgnoreCase)
                && !tag.Contains("application/atom+xml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var hrefMatch = HrefRegex().Match(tag);
            if (!hrefMatch.Success)
            {
                continue;
            }

            var href = hrefMatch.Groups[1].Value;
            if (Uri.TryCreate(href, UriKind.Absolute, out var absolute)
                && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
            {
                return absolute.ToString();
            }

            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) && Uri.TryCreate(baseUri, href, out var combined))
            {
                return combined.ToString();
            }
        }

        return null;
    }

    [GeneratedRegex("<link\\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LinkRegex();

    [GeneratedRegex("href\\s*=\\s*[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase)]
    private static partial Regex HrefRegex();
}
