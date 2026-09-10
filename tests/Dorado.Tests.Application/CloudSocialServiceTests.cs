using System.Net;
using System.Text.Json;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.Application.Services;
using Dorado.Domain.Models;
using Dorado.Infrastructure.External;
using Dorado.Tests.Application.TestFakes;
using DoradoCloud.Client;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Covers the desktop → cloud social/identity bridge: play-history posting,
/// live Zune Card fetch, device registration, and settings sync. Uses a stub
/// HTTP handler (see <see cref="StubHandler"/>) so no network is required.
/// </summary>
public sealed class CloudSocialServiceTests
{
    [Fact]
    public async Task RecordListen_PostsActivityWithBearer()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            return Json(HttpStatusCode.Created, """{"id":"3fa85f64-5717-4562-b3fc-2c963f66afa6","handle":"","kind":"listen","payloadJson":"{}","createdAt":"2026-01-01T00:00:00+00:00"}""");
        });
        var service = NewService(handler);

        var ok = await service.RecordListenAsync("Artist A", "Track A", "Album A");

        Assert.True(ok);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.EndsWith("/v1/social/me/activities", captured.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", captured.Headers.Authorization!.Scheme);
        Assert.Equal("tok", captured.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task RecordListen_ReturnsFalseWhenDisabled()
    {
        var service = new CloudSocialService(() => new AppSettings());

        Assert.False(service.IsEnabled);
        Assert.False(await service.RecordListenAsync("a", "t", null));
    }

    [Fact]
    public async Task RecordListen_ReturnsFalseOnCloudError()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var service = NewService(handler);

        Assert.False(await service.RecordListenAsync("a", "t", "al"));
    }

    [Fact]
    public async Task GetZuneCard_ParsesSnapshot()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"handle":"jane","displayName":"Jane","bio":"b","followers":2,"following":1,"activities":3,"badges":[{"code":"connector","name":"Connector","description":"d","earnedAt":"2026-01-01T00:00:00+00:00"}],"recent":[{"id":"11111111-1111-1111-1111-111111111111","handle":"jane","kind":"listen","payloadJson":"{}","createdAt":"2026-01-02T00:00:00+00:00"}]}"""));
        var service = NewService(handler);

        var card = await service.GetZuneCardAsync("jane");

        Assert.NotNull(card);
        Assert.Equal("jane", card!.Handle);
        Assert.Equal(2, card.Followers);
        Assert.Single(card.Badges);
        Assert.Single(card.Recent);
    }

    [Fact]
    public async Task RegisterDevice_PostsToIdentity()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            return Json(HttpStatusCode.Created,
                """{"id":"3fa85f64-5717-4562-b3fc-2c963f66afa6","name":"Desktop","platform":"windows","serial":null,"appVersion":"0.5.0","createdAt":"2026-01-01T00:00:00+00:00","lastSeenAt":"2026-01-01T00:00:00+00:00"}""");
        });
        var service = NewService(handler);

        var ok = await service.RegisterDeviceAsync("Desktop", "windows", null, "0.5.0");

        Assert.True(ok);
        Assert.EndsWith("/v1/identity/me/devices", captured!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task SyncSettings_PutsEnvelope()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json(HttpStatusCode.OK, """{"payloadJson":"{}","version":2,"updatedAt":"2026-01-01T00:00:00+00:00"}""");
        });
        var service = NewService(handler);

        var ok = await service.SyncSettingsAsync("{\"theme\":\"dark\"}", expectedVersion: 1);

        Assert.True(ok);
        Assert.Equal(HttpMethod.Put, captured!.Method);
        Assert.EndsWith("/v1/identity/me/settings", captured.RequestUri!.AbsolutePath);
        Assert.Contains("expectedVersion", body!);
    }

    // ---- playback hook ---------------------------------------------------

    [Fact]
    public async Task RecordTrackPlayed_MirrorsListenToCloudWhenEnabled()
    {
        var cloud = new RecordingCloudSocial { Enabled = true };
        var service = new UserStatsService(new FakeMediaLibraryService(), reviewService: null, cloudSocial: cloud);

        await service.RecordTrackPlayedAsync(new Track { Title = "Teardrop", ArtistName = "Massive Attack", AlbumTitle = "Mezzanine" });

        Assert.Single(cloud.Listens);
        Assert.Equal(("Massive Attack", "Teardrop", "Mezzanine"), cloud.Listens[0]);
    }

    [Fact]
    public async Task RecordTrackPlayed_DoesNotCallCloudWhenDisabled()
    {
        var cloud = new RecordingCloudSocial { Enabled = false };
        var service = new UserStatsService(new FakeMediaLibraryService(), reviewService: null, cloudSocial: cloud);

        await service.RecordTrackPlayedAsync(new Track { Title = "T", ArtistName = "A", AlbumTitle = "Al" });

        Assert.Empty(cloud.Listens);
    }

    [Fact]
    public async Task RecordTrackPlayed_SurvivesCloudFailure()
    {
        var cloud = new ThrowingCloudSocial();
        var service = new UserStatsService(new FakeMediaLibraryService(), reviewService: null, cloudSocial: cloud);

        // Must not throw despite the cloud failure.
        await service.RecordTrackPlayedAsync(new Track { Title = "T", ArtistName = "A", AlbumTitle = "Al" });
    }

    private static CloudSocialService NewService(HttpMessageHandler handler, AppSettings? settings = null)
    {
        var s = settings ?? new AppSettings
        {
            CloudEnabled = true,
            CloudBaseUrl = "https://cloud.dorado.example/",
            CloudAccessToken = "tok",
        };

        return new CloudSocialService(() => s, handler);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
}

internal sealed class RecordingCloudSocial : ICloudSocialService
{
    public bool Enabled { get; set; }
    public ZuneCardSnapshot? Card { get; set; }
    public List<(string Artist, string Title, string? Album)> Listens { get; } = new();

    public bool IsEnabled => Enabled;

    public Task<bool> RecordListenAsync(string artist, string title, string? album, CancellationToken cancellationToken = default)
    {
        Listens.Add((artist, title, album));
        return Task.FromResult(true);
    }

    public Task<ZuneCardSnapshot?> GetZuneCardAsync(string handle, CancellationToken cancellationToken = default)
        => Task.FromResult(Card);

    public Task<bool> UpsertProfileAsync(string handle, string displayName, string? bio, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> RegisterDeviceAsync(string name, string platform, string? serial, string? appVersion, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> SyncSettingsAsync(string payloadJson, int? expectedVersion = null, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}

internal sealed class ThrowingCloudSocial : ICloudSocialService
{
    public bool IsEnabled => true;

    public Task<bool> RecordListenAsync(string artist, string title, string? album, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("cloud down");

    public Task<ZuneCardSnapshot?> GetZuneCardAsync(string handle, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("cloud down");

    public Task<bool> UpsertProfileAsync(string handle, string displayName, string? bio, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("cloud down");

    public Task<bool> RegisterDeviceAsync(string name, string platform, string? serial, string? appVersion, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("cloud down");

    public Task<bool> SyncSettingsAsync(string payloadJson, int? expectedVersion = null, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("cloud down");
}
