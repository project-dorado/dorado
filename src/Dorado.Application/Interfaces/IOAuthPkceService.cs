namespace Dorado.Application.Interfaces;

/// <summary>
/// OAuth 2.0 Authorization Code + PKCE (RFC 7636) helpers for signing in to the
/// Dorado Cloud Identity (OpenIddict) module. The desktop opens the authorize
/// URL in the user's browser, captures the code on a loopback redirect, and
/// exchanges it for tokens — no client secret is embedded.
/// </summary>
public interface IOAuthPkceService
{
    /// <summary>Generates a fresh verifier/challenge pair (S256).</summary>
    OAuthPkceChallenge CreateChallenge();

    /// <summary>Builds the <c>/connect/authorize</c> URL for the browser.</summary>
    string BuildAuthorizeUrl(
        string baseUrl,
        string clientId,
        string redirectUri,
        string scope,
        string codeChallenge,
        string? state = null);

    /// <summary>Exchanges an authorization code for tokens at <c>/connect/token</c>.</summary>
    Task<OAuthTokenSet?> ExchangeCodeAsync(
        string baseUrl,
        string clientId,
        string code,
        string codeVerifier,
        string redirectUri,
        CancellationToken cancellationToken = default);
}

public sealed record OAuthPkceChallenge(string Verifier, string Challenge);

public sealed record OAuthTokenSet(string AccessToken, string TokenType, int ExpiresIn, string? RefreshToken);

/// <summary>
/// Orchestrates the interactive sign-in: PKCE challenge → browser → loopback
/// callback → token exchange → persist. Implemented in Infrastructure.
/// </summary>
public interface ICloudSignInService
{
    /// <summary>Runs the full sign-in flow and stores the token; returns true on success.</summary>
    Task<bool> SignInAsync(CancellationToken cancellationToken = default);

    /// <summary>Clears the stored credentials.</summary>
    void SignOut();
}
