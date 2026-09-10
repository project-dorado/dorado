using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Dorado.Application.Interfaces;
using Dorado.Application.Services;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;
using Dorado.Infrastructure.Devices;
using Dorado.Plugins.Protocol.Rpc;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Exercises the LAN sync endpoint (<see cref="SyncEndpointHost"/>) over
/// JSON-RPC: hello/pair/manifest/pull/push. Includes a real TCP round-trip
/// through <see cref="SyncTcpServer"/> to prove the socket framing.
/// </summary>
public sealed class SyncEndpointTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Hello_ReturnsServiceInfoInCamelCase()
    {
        var host = NewHost();

        var raw = await host.HandleAsync(SyncProtocol.MethodHello, Params(new SyncHelloRequest("dev", "Phone", "android", "0.1.0")));

        var json = JsonSerializer.Serialize(raw, Web);
        Assert.Contains("\"serviceType\":\"_dorado-sync._tcp\"", json);
        Assert.Contains("\"port\":8787", json);
        var response = Deserialize<SyncHelloResponse>(raw);
        Assert.Equal("Dorado Desktop", response!.ServerName);
        Assert.True(response.PairingRequired);
    }

    [Fact]
    public async Task Pair_RejectsWrongCode_AcceptsCorrectCode()
    {
        var host = NewHost("123456");

        var rejected = Deserialize<SyncPairResponse>(
            await host.HandleAsync(SyncProtocol.MethodPair, Params(new SyncPairRequest("000000", "dev"))));
        Assert.False(rejected!.Paired);
        Assert.Contains("invalid", rejected.Reason!, StringComparison.OrdinalIgnoreCase);

        var accepted = Deserialize<SyncPairResponse>(
            await host.HandleAsync(SyncProtocol.MethodPair, Params(new SyncPairRequest("123456", "dev"))));
        Assert.True(accepted!.Paired);
        Assert.NotNull(accepted.SessionToken);
    }

    [Fact]
    public async Task Manifest_ComputesAddsForEmptyDevice()
    {
        var host = NewHost();

        var response = Deserialize<SyncManifestResponse>(await host.HandleAsync(SyncProtocol.MethodManifest, Params(Request())));

        Assert.Equal("SERIAL-1", response!.DeviceSerialNumber);
        Assert.Equal(1, response.AddCount);
        Assert.Equal(0, response.RemoveCount);
        Assert.Equal(0, response.KeepCount);
        Assert.Single(response.Items);
        Assert.Equal("ADD", response.Items[0].Action);
        Assert.Equal("MUSIC", response.Items[0].Category);
    }

    [Fact]
    public async Task Manifest_KeepsExistingDeviceContent()
    {
        var host = NewHost();
        var library = Library();
        var existingId = library.Tracks[0].Id.ToString("D");

        var request = Request() with
        {
            Contents = new[] { new SyncDeviceContentDto(existingId, "MUSIC", "Track A", 0) },
        };

        var response = Deserialize<SyncManifestResponse>(await host.HandleAsync(SyncProtocol.MethodManifest, Params(request)));

        Assert.Equal(0, response!.AddCount);
        Assert.Equal(1, response.KeepCount);
    }

    [Fact]
    public async Task Pull_ReturnsAddItemsWithMetadataRatingAndTranscodeTarget()
    {
        var host = NewHost();
        await host.HandleAsync(SyncProtocol.MethodManifest, Params(Request()));

        var response = Deserialize<SyncPullResponse>(await host.HandleAsync(SyncProtocol.MethodPull, Params(new SyncPullRequest("SERIAL-1"))));

        Assert.Equal("SERIAL-1", response!.DeviceSerialNumber);
        Assert.Single(response.Items);
        var item = response.Items[0];
        Assert.Equal("Track A", item.Title);
        Assert.Equal("Artist A", item.Artist);
        Assert.Equal("Album A", item.Album);
        Assert.Equal("HEART", item.Rating);
        // The track file is .wma, which the device cannot play → transcode to AAC.
        Assert.Equal("m4a", item.TranscodeTarget);
    }

    [Fact]
    public async Task Push_ReportsAcceptedCount()
    {
        var stored = new List<SyncPushItemDto>();
        var host = new SyncEndpointHost(new SyncEngine(), _ => Task.FromResult(Library()), pushHandler: push =>
        {
            stored.AddRange(push.Items);
            return push.Items.Count;
        });

        var response = Deserialize<SyncPushResponse>(await host.HandleAsync(
            SyncProtocol.MethodPush,
            Params(new SyncPushRequest("SERIAL-1", new[]
            {
                new SyncPushItemDto("e1", "MUSIC", "Track A", 5, "HEART"),
                new SyncPushItemDto("e2", "MUSIC", "Track B", 2, "BROKEN"),
            }))));

        Assert.Equal(2, response!.Accepted);
        Assert.Equal(2, stored.Count);
        Assert.Equal("HEART", stored[0].Rating);
    }

    [Fact]
    public async Task TcpServer_RoundTripsHelloAndManifestOverSocket()
    {
        var host = NewHost("123456");
        var port = FreePort();
        await using var server = new SyncTcpServer(host, IPAddress.Loopback, port);
        server.Start();

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        var transport = new NetworkStreamLineTransport(client.GetStream());
        await using var channel = new JsonRpcChannel(transport);
        await transport.StartAsync();
        channel.Start();

        var hello = await channel.CallAsync(SyncProtocol.MethodHello, new SyncHelloRequest("dev", "Phone", "android", "0.1.0"));
        Assert.NotNull(hello);
        var helloResponse = JsonSerializer.Deserialize<SyncHelloResponse>(JsonSerializer.Serialize(hello, Web), Web);
        Assert.Equal("Dorado Desktop", helloResponse!.ServerName);

        var manifest = await channel.CallAsync(SyncProtocol.MethodManifest, Request());
        Assert.NotNull(manifest);
        var manifestResponse = JsonSerializer.Deserialize<SyncManifestResponse>(JsonSerializer.Serialize(manifest, Web), Web);
        Assert.Equal(1, manifestResponse!.AddCount);
    }

    private static JsonElement Params(object value) => JsonSerializer.SerializeToElement(value, Web);

    private static T? Deserialize<T>(object? element)
    {
        if (element is JsonElement json)
        {
            return json.Deserialize<T>(Web);
        }

        return element is null ? default : JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(element, Web), Web);
    }

    private static SyncEndpointHost NewHost(string? pairingCode = null)
        => new(new SyncEngine(), _ => Task.FromResult(Library()), pairingCode: pairingCode);

    private static SyncManifestRequest Request() => new(
        DeviceSerialNumber: "SERIAL-1",
        DeviceName: "Pixel",
        CapacityBytes: 16L * 1024 * 1024 * 1024,
        SystemBytes: 1L * 1024 * 1024 * 1024,
        GuestSession: false,
        Rules: new SyncRulesDto("All Music (Automatic Sync)", "3 Newest Episodes", "All Videos & Pictures", "Newest 25 Items"),
        Contents: Array.Empty<SyncDeviceContentDto>());

    private static SyncInput Library() => new()
    {
        Tracks = new[]
        {
            new Track
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Title = "Track A",
                ArtistName = "Artist A",
                AlbumTitle = "Album A",
                Duration = TimeSpan.FromSeconds(180),
                FilePath = "/music/track-a.wma",
                Rating = HeartRating.Favorite,
            },
        },
    };

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
