using System.Net;
using Dorado.Application.Models;
using DoradoCloud.Client;

namespace Dorado.Infrastructure.External;

/// <summary>
/// Supplies a <see cref="DoradoCloudClient"/> built from the *current* settings,
/// rebuilt whenever the configured base URL changes. The returned client uses the
/// SDK's <see cref="DoradoCloudAuthHandler"/>, which reads the credential from
/// <see cref="ICloudCredentialStore"/> per request and refreshes it on expiry —
/// so signing in or out takes effect without restarting the app, fixing the
/// stale-singleton behaviour of the previous registration.
///
/// Returns <c>null</c> when the cloud is disabled or no base URL is configured.
/// </summary>
public sealed class CloudClientProvider
{
    private readonly Func<AppSettings> _settings;
    private readonly ICloudCredentialStore _credentials;
    private readonly TimeSpan _timeout;
    private readonly HttpMessageHandler? _handler;
    private readonly object _gate = new();
    private string? _key;
    private DoradoCloudClient? _client;

    public CloudClientProvider(
        Func<AppSettings> settings,
        ICloudCredentialStore credentials,
        TimeSpan? timeout = null,
        HttpMessageHandler? handler = null)
    {
        _settings = settings;
        _credentials = credentials;
        _timeout = timeout ?? TimeSpan.FromSeconds(15);
        _handler = handler;
    }

    public DoradoCloudClient? Get()
    {
        var settings = _settings();
        if (!settings.CloudEnabled || string.IsNullOrWhiteSpace(settings.CloudBaseUrl))
        {
            return null;
        }

        lock (_gate)
        {
            if (_client is null || _key != settings.CloudBaseUrl)
            {
                _key = settings.CloudBaseUrl;
                _client = Build(settings);
            }

            return _client;
        }
    }

    private DoradoCloudClient Build(AppSettings settings)
    {
        var baseUri = new Uri(settings.CloudBaseUrl);
        var inner = _handler ?? new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All };
        var auth = new DoradoCloudAuthHandler(
            _credentials, baseUri, CloudSignInService.DefaultClientId, innerHandler: inner);

        var http = new HttpClient(auth)
        {
            BaseAddress = baseUri,
            Timeout = _timeout,
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Dorado/1.0 (+https://github.com/project-dorado/dorado)");
        return new DoradoCloudClient(http);
    }
}
