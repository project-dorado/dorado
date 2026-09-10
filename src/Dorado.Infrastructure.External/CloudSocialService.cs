using System.Net.Http.Headers;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using DoradoCloud.Client;
using DoradoCloud.Shared.Contracts;

namespace Dorado.Infrastructure.External;

/// <summary>
/// Bridges the desktop's local play history and Zune Card to the Dorado Cloud
/// Social and Identity modules.
///
/// The client is rebuilt whenever the base URL or bearer token changes (the
/// user signs in at runtime), so a token acquired after startup is honoured.
/// Every call degrades to a false/null result on error — playback must never
/// fail because the cloud is unreachable.
/// </summary>
public sealed class CloudSocialService : ICloudSocialService
{
    private readonly Func<AppSettings> _settings;
    private readonly HttpMessageHandler? _handler;
    private string? _cacheKey;
    private DoradoCloudClient? _client;

    public CloudSocialService(Func<AppSettings> settings, HttpMessageHandler? handler = null)
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

    public async Task<bool> RecordListenAsync(string artist, string title, string? album, CancellationToken cancellationToken = default)
    {
        var client = Client();
        if (client is null || string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            artist,
            track = title,
            album,
            at = DateTimeOffset.UtcNow,
        });

        try
        {
            var activity = await client.PostActivityAsync(new PostActivityRequest("listen", payload), cancellationToken).ConfigureAwait(false);
            return activity is not null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ZuneCardSnapshot?> GetZuneCardAsync(string handle, CancellationToken cancellationToken = default)
    {
        var client = Client();
        if (client is null || string.IsNullOrWhiteSpace(handle))
        {
            return null;
        }

        try
        {
            var card = await client.GetZuneCardAsync(handle, cancellationToken).ConfigureAwait(false);
            return card is null ? null : ToSnapshot(card);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpsertProfileAsync(string handle, string displayName, string? bio, CancellationToken cancellationToken = default)
    {
        var client = Client();
        if (client is null)
        {
            return false;
        }

        try
        {
            var profile = await client.UpsertMyProfileAsync(
                new UpsertProfileRequest(handle, displayName, bio), cancellationToken).ConfigureAwait(false);
            return profile is not null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> RegisterDeviceAsync(string name, string platform, string? serial, string? appVersion, CancellationToken cancellationToken = default)
    {
        var client = Client();
        if (client is null)
        {
            return false;
        }

        try
        {
            var device = await client.RegisterDeviceAsync(
                new RegisterDeviceRequest(name, platform, serial, appVersion), cancellationToken).ConfigureAwait(false);
            return device is not null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SyncSettingsAsync(string payloadJson, int? expectedVersion = null, CancellationToken cancellationToken = default)
    {
        var client = Client();
        if (client is null)
        {
            return false;
        }

        try
        {
            var settings = await client.PutSettingsAsync(
                new PutSettingsRequest(payloadJson, expectedVersion), cancellationToken).ConfigureAwait(false);
            return settings is not null;
        }
        catch
        {
            return false;
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

    private static ZuneCardSnapshot ToSnapshot(ZuneCardDto dto) => new()
    {
        Handle = dto.Handle,
        DisplayName = dto.DisplayName,
        Bio = dto.Bio,
        Followers = dto.Followers,
        Following = dto.Following,
        Activities = dto.Activities,
        Badges = dto.Badges.Select(b => new ZuneCardBadge
        {
            Code = b.Code,
            Name = b.Name,
            Description = b.Description,
            EarnedAt = b.EarnedAt,
        }).ToList(),
        Recent = dto.Recent.Select(a => new ZuneCardActivity
        {
            Id = a.Id,
            Handle = a.Handle,
            Kind = a.Kind,
            PayloadJson = a.PayloadJson,
            CreatedAt = a.CreatedAt,
        }).ToList(),
    };
}
