using System.Net;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.Infrastructure.External;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Covers the AcoustID scan-time enrichment: fingerprint gating, best-score
/// selection, and result parsing. The fingerprint and HTTP layers are stubs.
/// </summary>
public sealed class AcoustIdServiceTests
{
    [Fact]
    public async Task Lookup_ReturnsNullWhenNoApiKey()
    {
        var service = new AcoustIdService(
            () => new AppSettings { AcoustIdApiKey = string.Empty },
            new FakeFingerprints());

        Assert.False(service.IsConfigured);
        Assert.Null(await service.LookupAsync("/music/a.mp3"));
    }

    [Fact]
    public async Task Lookup_ReturnsBestScoringRecording()
    {
        var handler = new FakeHttpMessageHandler();
        handler.MapJson(
            url => url.Contains("acoustid.org"),
            "{\"status\":\"ok\",\"results\":[" +
            "{\"score\":0.71,\"recordings\":[{\"id\":\"rec-low\",\"title\":\"Low\",\"artists\":[{\"name\":\"A\"}],\"releasegroups\":[{\"title\":\"Al\"}]}]}," +
            "{\"score\":0.98,\"recordings\":[{\"id\":\"rec-high\",\"title\":\"High\",\"artists\":[{\"name\":\"Massive Attack\"}],\"releasegroups\":[{\"title\":\"Mezzanine\"}]}]}]}");

        var settings = new AppSettings { AcoustIdApiKey = "key-123" };
        var service = new AcoustIdService(() => settings, new FakeFingerprints(), handler);

        var match = await service.LookupAsync("/music/a.flac");

        Assert.NotNull(match);
        Assert.Equal("rec-high", match!.RecordingId);
        Assert.Equal("High", match.Title);
        Assert.Equal("Massive Attack", match.Artist);
        Assert.Equal("Mezzanine", match.Album);
        Assert.Equal(98, match.Score);
    }

    [Fact]
    public async Task Lookup_ReturnsNullWhenFingerprintUnavailable()
    {
        var settings = new AppSettings { AcoustIdApiKey = "key-123" };
        var service = new AcoustIdService(() => settings, new FakeFingerprints { Result = null });

        Assert.Null(await service.LookupAsync("/music/a.mp3"));
    }

    private sealed class FakeFingerprints : IFingerprintProvider
    {
        public (double Duration, string Fingerprint)? Result { get; set; } = (210, "AQADtEmS");

        public Task<(double Duration, string Fingerprint)?> GetFingerprintAsync(
            string filePath, CancellationToken cancellationToken = default)
            => Task.FromResult(Result);
    }
}
