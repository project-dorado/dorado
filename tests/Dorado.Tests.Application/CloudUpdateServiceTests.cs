using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.Infrastructure.External;
using DoradoCloud.Shared.Contracts;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Covers the desktop OTA path: fetch the signed release, verify the detached
/// RS256 signature over the canonical manifest, and never trust an unverified
/// or unavailable release.
/// </summary>
public sealed class CloudUpdateServiceTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Check_VerifiesSignedRelease()
    {
        using var rsa = RSA.Create(2048);
        var manifest = NewManifest();
        var release = NewRelease(manifest, UpdateManifestCrypto.Sign(manifest, rsa));
        var service = NewService(handler: SignedHandler(release, rsa));

        var info = await service.CheckAsync("dorado");

        Assert.NotNull(info);
        Assert.True(info!.SignatureVerified);
        Assert.Equal("0.6.0", info.Version);
        Assert.Equal("https://example.invalid/dorado-0.6.0.zip", info.Url);
        Assert.Equal("RS256", info.Algorithm);
    }

    [Fact]
    public async Task Check_DoesNotTrustTamperedSignature()
    {
        using var rsa = RSA.Create(2048);
        var manifest = NewManifest();
        // Sign the correct manifest, then publish a release whose fields differ.
        var signature = UpdateManifestCrypto.Sign(manifest, rsa);
        var tampered = NewRelease(manifest, signature) with { Version = "9.9.9" };
        var service = NewService(handler: SignedHandler(tampered, rsa));

        var info = await service.CheckAsync("dorado");

        Assert.NotNull(info);
        Assert.False(info!.SignatureVerified);
    }

    [Fact]
    public async Task Check_ReturnsNullWhenNoRelease()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            JsonSerializer.Serialize(new UpdateCheckResponse("dorado", "stable", false, null, "none"), Web)));
        var service = NewService(handler: handler);

        Assert.Null(await service.CheckAsync("dorado"));
    }

    [Fact]
    public async Task Check_ReturnsNullWhenDisabled()
    {
        var service = new CloudUpdateService(() => new AppSettings());

        Assert.False(service.IsEnabled);
        Assert.Null(await service.CheckAsync("dorado"));
    }

    [Fact]
    public async Task Check_ReturnsNullOnCloudError()
    {
        var service = NewService(handler: new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        Assert.Null(await service.CheckAsync("dorado"));
    }

    private static CloudUpdateService NewService(HttpMessageHandler handler)
    {
        var settings = new AppSettings
        {
            CloudEnabled = true,
            CloudBaseUrl = "https://cloud.dorado.example/",
            CloudAccessToken = "tok",
        };
        return new CloudUpdateService(() => settings, handler);
    }

    private static UpdateManifest NewManifest() => new(
        "dorado", "stable", "0.6.0",
        "https://example.invalid/dorado-0.6.0.zip",
        Convert.ToHexString(new byte[32]).ToLowerInvariant(),
        DateTimeOffset.Parse("2026-09-12T10:00:00+00:00"),
        "release notes");

    private static UpdateReleaseDto NewRelease(UpdateManifest manifest, string signature) => new(
        manifest.App, manifest.Channel, manifest.Version, manifest.Url,
        manifest.Sha256, manifest.PublishedAt, manifest.Notes, signature, "RS256");

    private static HttpMessageHandler SignedHandler(UpdateReleaseDto release, RSA rsa)
    {
        var pem = rsa.ExportSubjectPublicKeyInfoPem();
        var checkJson = JsonSerializer.Serialize(
            new UpdateCheckResponse(release.App, release.Channel, true, release, null), Web);

        return new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/signing-key", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(pem, Encoding.UTF8, "application/x-pem-file"),
                };
            }

            return Json(HttpStatusCode.OK, checkJson);
        });
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
