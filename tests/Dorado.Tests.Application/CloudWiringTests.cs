using System.Net;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.Infrastructure.External;
using DoradoCloud.Client;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Covers the Phase 1 client↔cloud wiring: the settings-backed credential store,
/// the settings-aware client provider (rebuilds on base-URL change, reflects
/// token changes live), and the <c>CloudPreferCloud=false</c> ordering.
/// </summary>
public sealed class CloudWiringTests
{
    [Fact]
    public async Task CredentialStore_ReturnsNullWhenNoToken()
    {
        var store = new SettingsCloudCredentialStore(new FakeSettingsStore(new AppSettings()));

        Assert.Null(await store.GetAsync());
    }

    [Fact]
    public async Task CredentialStore_RoundTripsAndNormalizesExpiryToUtc()
    {
        var fake = new FakeSettingsStore(new AppSettings());
        var store = new SettingsCloudCredentialStore(fake);
        var expiry = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

        await store.StoreAsync(new CloudCredential("access-1", "refresh-1", expiry));

        Assert.Equal("access-1", fake.Current.CloudAccessToken);
        Assert.Equal("refresh-1", fake.Current.CloudRefreshToken);
        Assert.Equal(DateTimeKind.Utc, fake.Current.CloudAccessTokenExpiresAtUtc!.Value.Kind);

        var read = await store.GetAsync();
        Assert.Equal("access-1", read!.AccessToken);
        Assert.Equal("refresh-1", read.RefreshToken);
        Assert.Equal(expiry, read.ExpiresAtUtc);
    }

    [Fact]
    public void ClientProvider_ReturnsNullWhenCloudDisabled()
    {
        var provider = new CloudClientProvider(
            () => new AppSettings { CloudEnabled = false, CloudBaseUrl = "https://cloud.example/" },
            new InMemoryCloudCredentialStore());

        Assert.Null(provider.Get());
    }

    [Fact]
    public void ClientProvider_ReturnsNullWhenBaseUrlMissing()
    {
        var provider = new CloudClientProvider(
            () => new AppSettings { CloudEnabled = true, CloudBaseUrl = string.Empty },
            new InMemoryCloudCredentialStore());

        Assert.Null(provider.Get());
    }

    [Fact]
    public async Task ClientProvider_AttachesBearerAndReflectsSignOutLive()
    {
        var fake = new FakeSettingsStore(new AppSettings
        {
            CloudEnabled = true,
            CloudBaseUrl = "https://cloud.example/",
            CloudAccessToken = "tok-abc",
            CloudAccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1),
        });
        var capture = new CapturingHandler();
        var provider = new CloudClientProvider(
            () => fake.Current,
            new SettingsCloudCredentialStore(fake),
            handler: capture);

        var client = provider.Get();
        Assert.NotNull(client);

        await client!.PingModuleAsync("identity");
        Assert.Equal("Bearer tok-abc", capture.AuthHeaders[0]);

        // Sign-out takes effect on the *existing* client without a rebuild.
        fake.Current.CloudAccessToken = string.Empty;
        fake.Current.CloudRefreshToken = string.Empty;
        await client.PingModuleAsync("identity");
        Assert.Null(capture.AuthHeaders[1]);
    }

    [Fact]
    public async Task ClientProvider_RebuildsWhenBaseUrlChanges()
    {
        var fake = new FakeSettingsStore(new AppSettings
        {
            CloudEnabled = true,
            CloudBaseUrl = "https://one.example/",
        });
        var capture = new CapturingHandler();
        var provider = new CloudClientProvider(
            () => fake.Current,
            new SettingsCloudCredentialStore(fake),
            handler: capture);

        var first = provider.Get();
        fake.Current.CloudBaseUrl = "https://two.example/";
        var second = provider.Get();

        Assert.NotSame(first, second);
        await second!.PingModuleAsync("identity");
        Assert.StartsWith("https://two.example/", capture.Urls[0]);
    }

    [Fact]
    public async Task CloudBackedMetadata_PrefersInnerWhenCloudPreferCloudIsFalse()
    {
        var inner = new RecordingMetadataService
        {
            ArtistMetadataResult = new ArtistMetadataResult { Name = "local-wins" },
        };
        var cloud = new DoradoCloudClient(new HttpClient(new StubHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"query\":\"X\",\"type\":\"artist\",\"total\":1,\"attribution\":\"MB\",\"items\":[{\"type\":\"artist\",\"mbid\":\"m1\",\"title\":\"X\",\"artist\":\"X\",\"date\":\"\",\"coverArtUrl\":null}]}"),
                }))
            { BaseAddress = new Uri("https://cloud.example/") });
        var settings = new AppSettings
        {
            CloudEnabled = true,
            CloudBaseUrl = "https://cloud.example/",
            CloudPreferCloud = false,
        };

        var sut = new CloudBackedMetadataService(inner, cloud, () => settings);
        var result = await sut.FetchArtistMetadataAsync("X");

        Assert.Equal("local-wins", result!.Name);
        Assert.Equal(1, inner.ArtistMetadataCalls);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<string?> AuthHeaders { get; } = new();
        public List<string> Urls { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AuthHeaders.Add(request.Headers.Authorization?.ToString());
            Urls.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"module\":\"identity\",\"status\":\"ok\",\"version\":\"1.0.0\"}"),
            });
        }
    }
}
