using System.Diagnostics;
using System.Security.Cryptography;
using Dorado.Application.Interfaces;

namespace Dorado.Infrastructure.External;

/// <summary>
/// Runs the interactive OIDC sign-in: builds the PKCE authorize URL, opens the
/// user's browser, captures the code on a loopback listener, exchanges it for
/// tokens, and persists the access token + expiry in settings.
/// </summary>
public sealed class CloudSignInService : ICloudSignInService
{
    private readonly ISettingsStore _settings;
    private readonly IOAuthPkceService _pkce;
    private readonly Func<string, Task> _openBrowser;
    private readonly string _clientId;
    private readonly int _port;
    private readonly TimeSpan _timeout;

    public const string DefaultClientId = "dorado-desktop";
    public const string DefaultScope = "openid profile email dorado.api offline_access";

    public CloudSignInService(
        ISettingsStore settings,
        IOAuthPkceService pkce,
        Func<string, Task>? openBrowser = null,
        string clientId = DefaultClientId,
        int port = 7890,
        TimeSpan? timeout = null)
    {
        _settings = settings;
        _pkce = pkce;
        _openBrowser = openBrowser ?? OpenInBrowserAsync;
        _clientId = clientId;
        _port = port;
        _timeout = timeout ?? TimeSpan.FromMinutes(3);
    }

    public async Task<bool> SignInAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settings.Load();
        if (string.IsNullOrWhiteSpace(settings.CloudBaseUrl))
        {
            return false;
        }

        await using var listener = new LoopbackRedirectListener(_port);
        listener.Start();
        var pkce = _pkce.CreateChallenge();
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();

        var authorizeUrl = _pkce.BuildAuthorizeUrl(
            settings.CloudBaseUrl, _clientId, listener.RedirectUri, DefaultScope, pkce.Challenge, state);

        await _openBrowser(authorizeUrl).ConfigureAwait(false);

        var code = await listener.WaitForCodeAsync(_timeout, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(code) || (listener.State is not null && listener.State != state))
        {
            return false;
        }

        var tokens = await _pkce.ExchangeCodeAsync(
            settings.CloudBaseUrl, _clientId, code, pkce.Verifier, listener.RedirectUri, cancellationToken).ConfigureAwait(false);
        if (tokens is null)
        {
            return false;
        }

        settings.CloudEnabled = true;
        settings.CloudAccessToken = tokens.AccessToken;
        settings.CloudAccessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn);
        _settings.Save(settings);
        return true;
    }

    public void SignOut()
    {
        var settings = _settings.Load();
        settings.CloudAccessToken = string.Empty;
        settings.CloudAccessTokenExpiresAtUtc = null;
        _settings.Save(settings);
    }

    private static Task OpenInBrowserAsync(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            // Headless/non-desktop environments: the caller can surface the URL instead.
        }

        return Task.CompletedTask;
    }
}
