using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dorado.Application.Interfaces;

namespace Dorado.Infrastructure.External;

/// <summary>
/// RFC 7636 PKCE implementation for the Dorado Cloud OpenIddict server.
/// Challenge = BASE64URL(SHA256(verifier)); the verifier is 43 base64url chars
/// (32 random bytes). Token exchange speaks the standard
/// <c>application/x-www-form-urlencoded</c> protocol.
/// </summary>
public sealed class OAuthPkceService : IOAuthPkceService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpMessageHandler? _handler;

    public OAuthPkceService(HttpMessageHandler? handler = null)
    {
        _handler = handler;
    }

    public OAuthPkceChallenge CreateChallenge()
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        return new OAuthPkceChallenge(verifier, ComputeChallenge(verifier));
    }

    /// <summary>BASE64URL(SHA256(ASCII(verifier))) — the S256 transformation.</summary>
    public static string ComputeChallenge(string verifier)
        => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    public string BuildAuthorizeUrl(
        string baseUrl,
        string clientId,
        string redirectUri,
        string scope,
        string codeChallenge,
        string? state = null)
    {
        var query = new List<string>
        {
            "response_type=code",
            $"client_id={Uri.EscapeDataString(clientId)}",
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}",
            $"scope={Uri.EscapeDataString(scope)}",
            $"code_challenge={Uri.EscapeDataString(codeChallenge)}",
            "code_challenge_method=S256",
        };
        if (!string.IsNullOrEmpty(state))
        {
            query.Add($"state={Uri.EscapeDataString(state)}");
        }

        return $"{baseUrl.TrimEnd('/')}/connect/authorize?{string.Join("&", query)}";
    }

    public async Task<OAuthTokenSet?> ExchangeCodeAsync(
        string baseUrl,
        string clientId,
        string code,
        string codeVerifier,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        using var http = new HttpClient(_handler ?? new SocketsHttpHandler())
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(20),
        };

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["redirect_uri"] = redirectUri,
        });

        try
        {
            var response = await http.PostAsync("connect/token", form, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(Json, cancellationToken).ConfigureAwait(false);
            return token?.AccessToken is null
                ? null
                : new OAuthTokenSet(token.AccessToken, token.TokenType ?? "Bearer", token.ExpiresIn, token.RefreshToken);
        }
        catch
        {
            return null;
        }
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("token_type")] string? TokenType,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken);
}
