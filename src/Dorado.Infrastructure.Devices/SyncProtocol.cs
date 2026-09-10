namespace Dorado.Infrastructure.Devices;

/// <summary>
/// Wire contract for phone ↔ Dorado desktop sync, mirroring
/// <c>dorado-hd/app/src/main/java/com/heretek/dorado_hd/sync/SyncProtocol.kt</c>.
///
/// Envelopes are JSON-RPC 2.0 over a line-framed TCP stream, using the same
/// framing as the plugin host (<see cref="Dorado.Plugins.Protocol.Rpc.JsonRpcChannel"/>).
/// Discovery is mDNS (<see cref="ServiceType"/>); the default port is
/// <see cref="DefaultPort"/>. Payloads use camelCase property names
/// (System.Text.Json Web defaults) so the Kotlin and C# clients agree without
/// ad-hoc mapping.
/// </summary>
public static class SyncProtocol
{
    public const string JsonRpc = "2.0";
    public const string ServiceType = "_dorado-sync._tcp";
    public const int DefaultPort = 8787;

    public const string MethodHello = "sync.hello";
    public const string MethodPair = "sync.pair";
    public const string MethodManifest = "sync.manifest";
    public const string MethodPull = "sync.pull";
    public const string MethodPush = "sync.push";
}

// ---- sync.hello ---------------------------------------------------------

public sealed record SyncHelloRequest(
    string DeviceId,
    string DeviceName,
    string Platform,
    string AppVersion);

public sealed record SyncHelloResponse(
    string ServerName,
    string ServiceVersion,
    string ServiceType,
    int Port,
    bool PairingRequired);

// ---- sync.pair ----------------------------------------------------------

public sealed record SyncPairRequest(string PairingCode, string DeviceId);

public sealed record SyncPairResponse(bool Paired, string? SessionToken, string? Reason);

// ---- sync.manifest ------------------------------------------------------

/// <summary>The device snapshot the phone sends so the desktop can compute the plan.</summary>
public sealed record SyncManifestRequest(
    string DeviceSerialNumber,
    string DeviceName,
    long CapacityBytes,
    long SystemBytes,
    bool GuestSession,
    SyncRulesDto Rules,
    IReadOnlyList<SyncDeviceContentDto> Contents);

public sealed record SyncRulesDto(string Music, string Podcasts, string Videos, string Pictures);

public sealed record SyncDeviceContentDto(string EntityId, string Category, string Title, long SizeBytes);

public sealed record SyncManifestItemDto(
    string Action,
    string Category,
    string EntityId,
    string Title,
    long SizeBytes,
    string? Detail);

/// <summary>The computed "what will sync" plan, reviewable on the phone before applying.</summary>
public sealed record SyncManifestResponse(
    string DeviceSerialNumber,
    bool IsGuestSession,
    IReadOnlyList<SyncManifestItemDto> Items,
    int AddCount,
    int RemoveCount,
    int KeepCount,
    long TotalAddBytes,
    long TotalRemoveBytes);

// ---- sync.pull (desktop → phone) ----------------------------------------

public sealed record SyncPullRequest(string DeviceSerialNumber, bool IncludeAddItems = true);

public sealed record SyncPullItemDto(
    string EntityId,
    string Category,
    string Title,
    string? Artist,
    string? Album,
    string? Rating,
    long SizeBytes,
    string? SourcePath,
    string? Detail,
    /// <summary>Device-playable container the desktop should transfer, or null when copied verbatim
    /// (see <c>Dorado.Domain.Models.MediaFormats</c>).</summary>
    string? TranscodeTarget = null);

public sealed record SyncPullResponse(string DeviceSerialNumber, IReadOnlyList<SyncPullItemDto> Items);

// ---- sync.push (phone → desktop: play counts, ratings) ------------------

public sealed record SyncPushRequest(string DeviceSerialNumber, IReadOnlyList<SyncPushItemDto> Items);

public sealed record SyncPushItemDto(
    string EntityId,
    string Category,
    string Title,
    int? PlayCount,
    string? Rating);

public sealed record SyncPushResponse(int Accepted);
