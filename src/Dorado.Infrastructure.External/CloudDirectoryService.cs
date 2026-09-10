using System.Net.Http.Headers;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using DoradoCloud.Client;

namespace Dorado.Infrastructure.External;

/// <summary>
/// Directory search over the Dorado Cloud Directory module (Podcast Index and
/// Radio-Browser). Mirrors <see cref="CloudSocialService"/>'s settings-aware,
/// self-healing client construction so a token/base-URL change is honoured.
/// </summary>
public sealed class CloudDirectoryService : ICloudDirectoryService
{
    private readonly Func<AppSettings> _settings;
    private readonly HttpMessageHandler? _handler;
    private string? _cacheKey;
    private DoradoCloudClient? _client;

    public CloudDirectoryService(Func<AppSettings> settings, HttpMessageHandler? handler = null)
    {
        _settings = settings;
        _handler = handler;
    }

    public bool IsEnabled
    {
        get
        {
            var settings = _settings();
            return settings.CloudEnabled && !string.IsNullOrWhiteSpace(settings.CloudBaseUrl);
        }
    }

    public async Task<IReadOnlyList<PodcastDirectoryEntry>> SearchPodcastsAsync(
        string query, int limit = 20, CancellationToken cancellationToken = default)
    {
        var client = Client();
        if (client is null || string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<PodcastDirectoryEntry>();
        }

        try
        {
            var response = await client.DirectoryPodcastSearchAsync(query, limit, cancellationToken).ConfigureAwait(false);
            return response?.Items.Select(item => new PodcastDirectoryEntry
            {
                FeedId = item.FeedId,
                Title = item.Title,
                Author = item.Author,
                Description = item.Description,
                ImageUrl = item.ImageUrl,
                FeedUrl = item.FeedUrl,
                Categories = item.Categories,
                Language = item.Language,
            }).ToList() ?? new List<PodcastDirectoryEntry>();
        }
        catch
        {
            return Array.Empty<PodcastDirectoryEntry>();
        }
    }

    public async Task<IReadOnlyList<RadioDirectoryEntry>> SearchRadioAsync(
        string? query = null, string? country = null, string? tag = null,
        int limit = 30, CancellationToken cancellationToken = default)
    {
        var client = Client();
        if (client is null)
        {
            return Array.Empty<RadioDirectoryEntry>();
        }

        try
        {
            var response = await client.DirectoryRadioSearchAsync(query, country, tag, limit, cancellationToken).ConfigureAwait(false);
            return response?.Items.Select(item => new RadioDirectoryEntry
            {
                StationId = item.StationId,
                Name = item.Name,
                Url = item.Url,
                Favicon = item.Favicon,
                Country = item.Country,
                CountryCode = item.CountryCode,
                Tags = item.Tags,
                Codec = item.Codec,
                Bitrate = item.Bitrate,
                Votes = item.Votes,
            }).ToList() ?? new List<RadioDirectoryEntry>();
        }
        catch
        {
            return Array.Empty<RadioDirectoryEntry>();
        }
    }

    private DoradoCloudClient? Client()
    {
        var settings = _settings();
        if (!settings.CloudEnabled || string.IsNullOrWhiteSpace(settings.CloudBaseUrl))
        {
            return null;
        }

        var key = $"{settings.CloudBaseUrl}|{settings.CloudAccessToken}";
        if (_client is null || _cacheKey != key)
        {
            _cacheKey = key;
            _client = BuildClient(settings);
        }

        return _client;
    }

    private DoradoCloudClient BuildClient(AppSettings settings)
    {
        var http = new HttpClient(
            _handler ?? new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.All })
        {
            BaseAddress = new Uri(settings.CloudBaseUrl),
            Timeout = TimeSpan.FromSeconds(15),
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Dorado/1.0 (+https://github.com/project-dorado/dorado)");
        if (!string.IsNullOrWhiteSpace(settings.CloudAccessToken))
        {
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", settings.CloudAccessToken);
        }

        return new DoradoCloudClient(http);
    }
}
