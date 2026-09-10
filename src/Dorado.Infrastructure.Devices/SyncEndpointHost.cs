using System.Collections.Concurrent;
using System.Text.Json;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.Domain.Models;

namespace Dorado.Infrastructure.Devices;

/// <summary>
/// Implements the <c>sync.*</c> JSON-RPC surface (see <see cref="SyncProtocol"/>)
/// over a line transport. The desktop is the server: it owns the library and the
/// sync rules, receives the phone's device snapshot, and returns the computed
/// plan the phone reviews before transferring.
///
/// Payloads are (de)serialized with System.Text.Json Web defaults (camelCase) so
/// the Kotlin client's field names match the C# records 1:1.
/// </summary>
public sealed class SyncEndpointHost
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private readonly ISyncEngine _engine;
    private readonly Func<CancellationToken, Task<SyncInput>> _libraryProvider;
    private readonly Func<SyncPushRequest, int>? _pushHandler;
    private readonly ConcurrentDictionary<string, SyncPlan> _plans = new(StringComparer.OrdinalIgnoreCase);

    public SyncEndpointHost(
        ISyncEngine engine,
        Func<CancellationToken, Task<SyncInput>> libraryProvider,
        Func<SyncPushRequest, int>? pushHandler = null,
        string serverName = "Dorado Desktop",
        string? pairingCode = null)
    {
        _engine = engine;
        _libraryProvider = libraryProvider;
        _pushHandler = pushHandler;
        ServerName = serverName;
        PairingCode = pairingCode ?? GeneratePairingCode();
    }

    public string ServerName { get; }

    /// <summary>The 6-digit code the phone must present to pair.</summary>
    public string PairingCode { get; }

    public const string ServiceVersion = "0.1.0";

    /// <summary>
    /// Dispatches a sync method. The result is returned as a
    /// <see cref="JsonElement"/> serialized with Web (camelCase) defaults, so the
    /// JSON-RPC channel's own serializer writes Kotlin-compatible field names.
    /// </summary>
    public async Task<object?> HandleAsync(string method, JsonElement? parameters)
    {
        object result = method switch
        {
            SyncProtocol.MethodHello => Hello(parameters),
            SyncProtocol.MethodPair => Pair(parameters),
            SyncProtocol.MethodManifest => await ManifestAsync(parameters, CancellationToken.None).ConfigureAwait(false),
            SyncProtocol.MethodPull => await PullAsync(parameters, CancellationToken.None).ConfigureAwait(false),
            SyncProtocol.MethodPush => Push(parameters),
            _ => throw new InvalidOperationException($"unknown method '{method}'."),
        };

        return ToElement(result);
    }

    private SyncHelloResponse Hello(JsonElement? parameters)
    {
        // The hello request is informational; accept it even if empty.
        _ = Deserialize<SyncHelloRequest>(parameters);
        return new SyncHelloResponse(
            ServerName: ServerName,
            ServiceVersion: ServiceVersion,
            ServiceType: SyncProtocol.ServiceType,
            Port: SyncProtocol.DefaultPort,
            PairingRequired: true);
    }

    private SyncPairResponse Pair(JsonElement? parameters)
    {
        var request = Deserialize<SyncPairRequest>(parameters)
            ?? throw new ArgumentException("sync.pair requires a payload.");

        if (!string.Equals(request.PairingCode?.Trim(), PairingCode, StringComparison.Ordinal))
        {
            return new SyncPairResponse(false, null, "invalid pairing code");
        }

        return new SyncPairResponse(true, $"dorado-sync-{Guid.NewGuid():N}", null);
    }

    private async Task<SyncManifestResponse> ManifestAsync(JsonElement? parameters, CancellationToken cancellationToken)
    {
        var request = Deserialize<SyncManifestRequest>(parameters)
            ?? throw new ArgumentException("sync.manifest requires a payload.");

        var settings = new AppSettings
        {
            MusicSyncRule = request.Rules.Music,
            PodcastSyncRule = request.Rules.Podcasts,
            VideoSyncRule = request.Rules.Videos,
            PicturesSyncRule = request.Rules.Pictures,
        };

        var group = _engine.BuildDefaultGroup(request.DeviceSerialNumber, settings, request.GuestSession);
        var transport = new RemoteDeviceTransport(
            request.DeviceSerialNumber, request.DeviceName, request.CapacityBytes, request.SystemBytes, request.Contents);
        var library = await _libraryProvider(cancellationToken).ConfigureAwait(false);
        var plan = _engine.BuildPlan(group, library, transport);
        _plans[request.DeviceSerialNumber] = plan;

        var items = plan.Items.Select(item => new SyncManifestItemDto(
            SyncMapping.Action(item.Action),
            SyncMapping.Category(item.Category),
            item.EntityId.ToString("D"),
            item.Title,
            item.SizeBytes,
            item.Detail)).ToList();

        return new SyncManifestResponse(
            plan.DeviceSerialNumber,
            plan.IsGuestSession,
            items,
            plan.AddCount,
            plan.RemoveCount,
            plan.KeepCount,
            plan.TotalAddBytes,
            plan.TotalRemoveBytes);
    }

    private async Task<SyncPullResponse> PullAsync(JsonElement? parameters, CancellationToken cancellationToken)
    {
        var request = Deserialize<SyncPullRequest>(parameters)
            ?? throw new ArgumentException("sync.pull requires a payload.");

        if (!_plans.TryGetValue(request.DeviceSerialNumber, out var plan))
        {
            throw new InvalidOperationException("No manifest has been computed for this device; call sync.manifest first.");
        }

        var library = await _libraryProvider(cancellationToken).ConfigureAwait(false);
        var tracksById = library.Tracks.ToDictionary(t => t.Id);
        var podcastById = library.PodcastEpisodes.ToDictionary(p => p.Id);

        var items = plan.Items
            .Where(item => item.Action == TransferAction.Add)
            .Select(item =>
            {
                tracksById.TryGetValue(item.EntityId, out var track);
                podcastById.TryGetValue(item.EntityId, out var episode);
                var source = track?.FilePath ?? episode?.AudioUrl ?? item.SourcePath;
                return new SyncPullItemDto(
                    item.EntityId.ToString("D"),
                    SyncMapping.Category(item.Category),
                    item.Title,
                    track?.ArtistName,
                    track?.AlbumTitle ?? episode?.SeriesTitle,
                    track is null ? null : SyncMapping.Rating(track.Rating),
                    item.SizeBytes,
                    item.SourcePath,
                    item.Detail,
                    TranscodeTargetFor(source));
            })
            .ToList();

        return new SyncPullResponse(request.DeviceSerialNumber, items);
    }

    private SyncPushResponse Push(JsonElement? parameters)
    {
        var request = Deserialize<SyncPushRequest>(parameters)
            ?? throw new ArgumentException("sync.push requires a payload.");

        var accepted = _pushHandler?.Invoke(request) ?? request.Items.Count;
        return new SyncPushResponse(accepted);
    }

    private static T? Deserialize<T>(JsonElement? parameters)
        => parameters is null ? default : parameters.Value.Deserialize<T>(WebOptions);

    /// <summary>
    /// The container the desktop should transfer for a device, using the shared
    /// <see cref="Dorado.Domain.Models.MediaFormats"/> contract: null means the
    /// source is already device-playable (copy verbatim).
    /// </summary>
    private static string? TranscodeTargetFor(string? sourcePathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(sourcePathOrUrl))
        {
            return null;
        }

        var extension = System.IO.Path.GetExtension(sourcePathOrUrl.Split('?')[0]).TrimStart('.');
        return extension.Length == 0 ? null : Dorado.Domain.Models.MediaFormats.TranscodeTargetFor(extension);
    }

    private static string GeneratePairingCode()
        => Random.Shared.Next(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Serializes a result with Web defaults (used by the TCP host/bridge).</summary>
    public static JsonElement ToElement(object value) => JsonSerializer.SerializeToElement(value, WebOptions);
}
