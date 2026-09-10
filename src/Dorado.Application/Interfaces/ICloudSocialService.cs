namespace Dorado.Application.Interfaces;

/// <summary>
/// Bridges the desktop's local Zune Card / play-history surface to the
/// Dorado Cloud Social and Identity modules. Implemented in the Infrastructure
/// layer (which owns the HTTP client); a no-op default keeps the app fully
/// functional offline.
/// </summary>
public interface ICloudSocialService
{
    /// <summary>True when the cloud is enabled and a bearer token is present.</summary>
    bool IsEnabled { get; }

    /// <summary>Posts a completed listen as a social activity (play-history sync).</summary>
    Task<bool> RecordListenAsync(string artist, string title, string? album, CancellationToken cancellationToken = default);

    /// <summary>Fetches a live Zune Card (counts, badges, recent activity) by handle.</summary>
    Task<ZuneCardSnapshot?> GetZuneCardAsync(string handle, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates the authenticated principal's profile (handle/display name/bio).</summary>
    Task<bool> UpsertProfileAsync(string handle, string displayName, string? bio, CancellationToken cancellationToken = default);

    /// <summary>Registers this desktop as a device on the account.</summary>
    Task<bool> RegisterDeviceAsync(string name, string platform, string? serial, string? appVersion, CancellationToken cancellationToken = default);

    /// <summary>Pushes the local settings envelope with optimistic concurrency.</summary>
    Task<bool> SyncSettingsAsync(string payloadJson, int? expectedVersion = null, CancellationToken cancellationToken = default);
}

/// <summary>Wire-shape mirror of the cloud <c>ZuneCardDto</c>.</summary>
public sealed class ZuneCardSnapshot
{
    public string Handle { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public int Followers { get; set; }
    public int Following { get; set; }
    public int Activities { get; set; }
    public IReadOnlyList<ZuneCardBadge> Badges { get; set; } = Array.Empty<ZuneCardBadge>();
    public IReadOnlyList<ZuneCardActivity> Recent { get; set; } = Array.Empty<ZuneCardActivity>();
}

public sealed class ZuneCardBadge
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset? EarnedAt { get; set; }
}

public sealed class ZuneCardActivity
{
    public Guid Id { get; set; }
    public string Handle { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Offline default: reports disabled and never calls the network.</summary>
public sealed class NullCloudSocialService : ICloudSocialService
{
    public bool IsEnabled => false;

    public Task<bool> RecordListenAsync(string artist, string title, string? album, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<ZuneCardSnapshot?> GetZuneCardAsync(string handle, CancellationToken cancellationToken = default)
        => Task.FromResult<ZuneCardSnapshot?>(null);

    public Task<bool> UpsertProfileAsync(string handle, string displayName, string? bio, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> RegisterDeviceAsync(string name, string platform, string? serial, string? appVersion, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> SyncSettingsAsync(string payloadJson, int? expectedVersion = null, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
