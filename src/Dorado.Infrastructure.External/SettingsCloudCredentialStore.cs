using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using DoradoCloud.Client;

namespace Dorado.Infrastructure.External;

/// <summary>
/// <see cref="ICloudCredentialStore"/> backed by the persisted <see cref="AppSettings"/>
/// envelope, so a refreshed access/refresh token survives restarts and is shared
/// with the interactive sign-in service (which owns the initial grant).
/// </summary>
public sealed class SettingsCloudCredentialStore : ICloudCredentialStore
{
    private readonly ISettingsStore _settings;

    public SettingsCloudCredentialStore(ISettingsStore settings) => _settings = settings;

    public ValueTask<CloudCredential?> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settings.Load();
        if (string.IsNullOrWhiteSpace(settings.CloudAccessToken))
        {
            return ValueTask.FromResult<CloudCredential?>(null);
        }

        var expiry = settings.CloudAccessTokenExpiresAtUtc is { } utc
            ? new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc))
            : (DateTimeOffset?)null;

        return ValueTask.FromResult<CloudCredential?>(
            new CloudCredential(settings.CloudAccessToken, settings.CloudRefreshToken, expiry));
    }

    public ValueTask StoreAsync(CloudCredential credential, CancellationToken cancellationToken = default)
    {
        var settings = _settings.Load();
        settings.CloudAccessToken = credential.AccessToken;
        settings.CloudRefreshToken = credential.RefreshToken ?? string.Empty;
        settings.CloudAccessTokenExpiresAtUtc = credential.ExpiresAtUtc?.UtcDateTime;
        settings.CloudEnabled = true;
        _settings.Save(settings);
        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settings.Load();
        settings.CloudAccessToken = string.Empty;
        settings.CloudRefreshToken = string.Empty;
        settings.CloudAccessTokenExpiresAtUtc = null;
        _settings.Save(settings);
        return ValueTask.CompletedTask;
    }
}
