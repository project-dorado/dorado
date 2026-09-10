using System.Net;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.Domain.Models;
using Dorado.Infrastructure.External;
using DoradoCloud.Client;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Wires up the cloud-backed decorator against a deterministic
/// <see cref="HttpMessageHandler"/> stub so the cloud-vs-fallback decision is
/// observable without a live network. These tests are the regression net for
/// Phase 3's "cloud when enabled, fallback when disabled / failing" contract.
/// </summary>
public sealed class CloudBackedMetadataServiceTests
{
    [Fact]
    public async Task FetchArtistMetadataAsync_FallsBackWhenCloudDisabled()
    {
        var inner = new RecordingMetadataService();
        var client = NewCloud(new StubHandler(_ => NotFound()));
        var settings = NewSettings(cloudEnabled: false);
        var sut = new CloudBackedMetadataService(inner, client, () => settings);

        var result = await sut.FetchArtistMetadataAsync("Massive Attack");

        Assert.Null(result);
        Assert.Equal(1, inner.ArtistMetadataCalls);
    }

    [Fact]
    public async Task FetchArtistMetadataAsync_UsesCloudWhenEnabled()
    {
        var inner = new RecordingMetadataService();
        var client = NewCloud(new StubHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/catalog/search"))
            {
                return Ok("{\"query\":\"Massive Attack\",\"type\":\"artist\",\"total\":1,\"attribution\":\"MusicBrainz\",\"items\":[{\"type\":\"artist\",\"mbid\":\"abc-123\",\"title\":\"Massive Attack\",\"artist\":\"Massive Attack\",\"date\":\"\",\"coverArtUrl\":null}]}");
            }
            if (req.RequestUri!.AbsolutePath.EndsWith("/catalog/artists/abc-123"))
            {
                return Ok("{\"mbid\":\"abc-123\",\"name\":\"Massive Attack\",\"sortName\":\"Massive Attack\",\"country\":\"GB\",\"disambiguation\":\"UK trip-hop collective\",\"coverArtUrl\":null}");
            }
            return NotFound();
        }));
        var settings = NewSettings(cloudEnabled: true);
        var sut = new CloudBackedMetadataService(inner, client, () => settings);

        var result = await sut.FetchArtistMetadataAsync("Massive Attack");

        Assert.NotNull(result);
        Assert.Equal("abc-123", result!.MusicBrainzId);
        Assert.Equal("Massive Attack", result.Name);
        Assert.Equal(0, inner.ArtistMetadataCalls);
    }

    [Fact]
    public async Task FetchArtistMetadataAsync_FallsBackWhenCloudReturnsError()
    {
        var inner = new RecordingMetadataService { ArtistMetadataResult = new ArtistMetadataResult { Name = "fallback" } };
        var client = NewCloud(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var settings = NewSettings(cloudEnabled: true);
        var sut = new CloudBackedMetadataService(inner, client, () => settings);

        var result = await sut.FetchArtistMetadataAsync("Anyone");

        Assert.NotNull(result);
        Assert.Equal("fallback", result!.Name);
        Assert.Equal(1, inner.ArtistMetadataCalls);
    }

    [Fact]
    public async Task FindAlbumArtworkAsync_UsesCloudArtworkWhenFound()
    {
        var inner = new RecordingMetadataService();
        var client = NewCloud(new StubHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/catalog/search"))
            {
                return Ok("{\"query\":\"DJ Shadow Endtroducing\",\"type\":\"release-group\",\"total\":1,\"attribution\":\"MusicBrainz\",\"items\":[{\"type\":\"release-group\",\"mbid\":\"rg-1\",\"title\":\"Endtroducing\",\"artist\":\"DJ Shadow\",\"date\":\"1996\",\"coverArtUrl\":null}]}");
            }
            if (req.RequestUri!.AbsolutePath.EndsWith("/artwork/front/rg-1"))
            {
                var bytes = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
                };
                bytes.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                return bytes;
            }
            return NotFound();
        }));
        var settings = NewSettings(cloudEnabled: true);
        var sut = new CloudBackedMetadataService(inner, client, () => settings);

        var result = await sut.FindAlbumArtworkAsync("DJ Shadow", "Endtroducing");

        Assert.NotNull(result);
        Assert.Equal("rg-1", result!.MusicBrainzReleaseGroupId);
        Assert.Contains("/v1/artwork/front/rg-1", result.ArtworkUrl);
        Assert.Equal(0, inner.AlbumArtworkCalls);
    }

    private static AppSettings NewSettings(bool cloudEnabled) => new()
    {
        CloudEnabled = cloudEnabled,
        CloudBaseUrl = cloudEnabled ? "https://cloud.dorado.example/" : string.Empty,
    };

    private static DoradoCloudClient NewCloud(HttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("https://cloud.dorado.example/") });

    private static HttpResponseMessage Ok(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    private static HttpResponseMessage NotFound()
        => new(HttpStatusCode.NotFound);
}

internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(_handler(request));
}

internal sealed class RecordingMetadataService : IExternalMetadataService
{
    public ArtistMetadataResult? ArtistMetadataResult { get; set; }
    public int ArtistMetadataCalls { get; private set; }
    public int AlbumArtworkCalls { get; private set; }

    public Task<ArtistMetadataResult?> FetchArtistMetadataAsync(string artistName, CancellationToken cancellationToken = default)
    {
        ArtistMetadataCalls++;
        return Task.FromResult(ArtistMetadataResult);
    }

    public Task<IReadOnlyList<string>> FetchArtistBackgroundUrlsAsync(string artistName, string fanartTvApiKey, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    public Task<IReadOnlyList<string>> FetchFallbackArtistBackgroundUrlsAsync(string artistName, string? communityBaseUrl, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    public Task<AlbumArtworkResult?> FindAlbumArtworkAsync(string artistName, string albumTitle, int? year = null, CancellationToken cancellationToken = default)
    {
        AlbumArtworkCalls++;
        return Task.FromResult<AlbumArtworkResult?>(null);
    }

    public Task<IReadOnlyList<TrackMatchCandidate>> FindTrackMatchesAsync(string artistName, string albumTitle, IReadOnlyList<Track> tracks, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TrackMatchCandidate>>(Array.Empty<TrackMatchCandidate>());

    public Task<LyricsResult?> FetchLyricsAsync(string artistName, string trackTitle, TimeSpan? duration = null, CancellationToken cancellationToken = default)
        => Task.FromResult<LyricsResult?>(null);
}
