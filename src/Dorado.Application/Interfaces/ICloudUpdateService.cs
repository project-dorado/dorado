namespace Dorado.Application.Interfaces;

/// <summary>
/// Checks the Dorado Cloud signed update feed for a newer release and verifies
/// its detached RS256 signature against the published signing key. Implemented
/// in Infrastructure; the offline default reports no update.
/// </summary>
public interface ICloudUpdateService
{
    /// <summary>True when the cloud is enabled and configured.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Returns the latest published release for the app/channel, or null when
    /// none is available or the cloud is unreachable. <see cref="CloudUpdateInfo.SignatureVerified"/>
    /// must be true before the update is applied.
    /// </summary>
    Task<CloudUpdateInfo?> CheckAsync(string app, string channel = "stable", CancellationToken cancellationToken = default);
}

public sealed class CloudUpdateInfo
{
    public string App { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;

    /// <summary>True when the manifest's signature validated against the published key.</summary>
    public bool SignatureVerified { get; set; }
}

public sealed class NullCloudUpdateService : ICloudUpdateService
{
    public bool IsEnabled => false;

    public Task<CloudUpdateInfo?> CheckAsync(string app, string channel = "stable", CancellationToken cancellationToken = default)
        => Task.FromResult<CloudUpdateInfo?>(null);
}
