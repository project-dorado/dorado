using System.Net;
using System.Text;
using Dorado.Infrastructure.External;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Covers the PKCE implementation against the RFC 7636 test vector, the
/// authorize-URL composition, and the token exchange.
/// </summary>
public sealed class OAuthPkceServiceTests
{
    [Fact]
    public void ComputeChallenge_MatchesRfc7636Vector()
    {
        // RFC 7636 Appendix B.
        const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", OAuthPkceService.ComputeChallenge(verifier));
    }

    [Fact]
    public void CreateChallenge_ProducesVerifierAndMatchingChallenge()
    {
        var service = new OAuthPkceService();
        var challenge = service.CreateChallenge();

        Assert.Equal(43, challenge.Verifier.Length);
        Assert.DoesNotContain('+', challenge.Verifier);
        Assert.DoesNotContain('/', challenge.Verifier);
        Assert.DoesNotContain('=', challenge.Verifier);
        Assert.Equal(OAuthPkceService.ComputeChallenge(challenge.Verifier), challenge.Challenge);
    }

    [Fact]
    public void BuildAuthorizeUrl_IncludesPkceParameters()
    {
        var service = new OAuthPkceService();

        var url = service.BuildAuthorizeUrl(
            "https://cloud.example/", "dorado-desktop", "http://127.0.0.1:7890/callback",
            "openid profile email dorado.api", "CHALLENGE", "STATE123");

        Assert.StartsWith("https://cloud.example/connect/authorize?", url);
        Assert.Contains("response_type=code", url);
        Assert.Contains("client_id=dorado-desktop", url);
        Assert.Contains("redirect_uri=http%3A%2F%2F127.0.0.1%3A7890%2Fcallback", url);
        Assert.Contains("code_challenge=CHALLENGE", url);
        Assert.Contains("code_challenge_method=S256", url);
        Assert.Contains("state=STATE123", url);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ParsesTokens()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"access_token":"tok","token_type":"Bearer","expires_in":3600,"refresh_token":"rt"}""",
                Encoding.UTF8, "application/json"),
        });
        var service = new OAuthPkceService(handler);

        var tokens = await service.ExchangeCodeAsync("https://cloud.example/", "dorado-desktop", "code", "verifier", "http://127.0.0.1:7890/callback");

        Assert.NotNull(tokens);
        Assert.Equal("tok", tokens!.AccessToken);
        Assert.Equal("Bearer", tokens.TokenType);
        Assert.Equal(3600, tokens.ExpiresIn);
        Assert.Equal("rt", tokens.RefreshToken);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ReturnsNullOnRejection()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var service = new OAuthPkceService(handler);

        Assert.Null(await service.ExchangeCodeAsync("https://cloud.example/", "dorado-desktop", "code", "verifier", "http://127.0.0.1:7890/callback"));
    }
}
