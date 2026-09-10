using System.Net.Http.Headers;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using DoradoCloud.Client;

namespace Dorado.Infrastructure.External;

/// <summary>
/// Consumes the Dorado Cloud signed update feed. Fetches the latest release,
/// then verifies its detached RS256 signature over the canonical manifest using
/// the same <c>UpdateManifestCrypto</c> contract the cloud publishes with. An
/// unverified manifest is returned with <c>SignatureVerified = false</c> so the
/// caller never applies it.
/// </summary>
public sealed class CloudUpdateService : ICloudUpdateService
{
    private readonly Func<AppSettings> _settings;
    private readonly HttpMessageHandler? _handler;
    private string? _cacheKey;
    private DoradoCloudClient? _client;

    public CloudUpdateService(Func<AppSettings> settings, HttpMessageHandler? handler = null)
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

    public async Task<CloudUpdateInfo?> CheckAsync(string app, string channel = "stable", CancellationToken cancellationToken = default)
    {
        var client = Client();
        if (client is null || string.IsNullOrWhiteSpace(app))
        {
            return null;
        }

        try
        {
            var check = await client.CheckForUpdateAsync(app, channel, cancellationToken).ConfigureAwait(false);
            if (check is null || !check.Available || check.Release is null)
            {
                return null;
            }

            var release = check.Release;
            var verified = await client.VerifyReleaseSignatureAsync(
                new DoradoCloud.Shared.Contracts.UpdateManifest(
                    release.App, release.Channel, release.Version, release.Url,
                    release.Sha256, release.PublishedAt, release.Notes),
                release.Signature,
                cancellationToken).ConfigureAwait(false);

            return new CloudUpdateInfo
            {
                App = release.App,
                Channel = release.Channel,
                Version = release.Version,
                Url = release.Url,
                Sha256 = release.Sha256,
                Notes = release.Notes,
                Algorithm = release.Algorithm,
                SignatureVerified = verified,
            };
        }
        catch
        {
            return null;
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
