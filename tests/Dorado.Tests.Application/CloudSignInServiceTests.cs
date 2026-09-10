using System.Net;
using System.Net.Sockets;
using System.Text;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.Infrastructure.External;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Covers the full interactive sign-in: PKCE authorize URL → loopback callback →
/// token exchange → persisted settings. The "browser" is a stub that hits the
/// loopback listener, and the token endpoint is a stub HTTP handler.
/// </summary>
public sealed class CloudSignInServiceTests
{
    [Fact]
    public async Task SignIn_PersistsTokenFromLoopbackCallback()
    {
        var settings = new FakeSettingsStore(new AppSettings
        {
            CloudEnabled = false,
            CloudBaseUrl = "https://cloud.dorado.example/",
        });
        var tokenHandler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"access_token":"access-123","token_type":"Bearer","expires_in":3600,"refresh_token":"rt"}""",
                Encoding.UTF8, "application/json"),
        });
        var pkce = new OAuthPkceService(tokenHandler);

        // The browser "approves" by calling the redirect URI captured from the URL.
        // It must return promptly (real browsers launch and return); the GET then
        // fires after the loopback listener is waiting.
        Func<string, Task> browser = authorizeUrl =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var query = new Uri(authorizeUrl).Query;
                    var redirect = Uri.UnescapeDataString(Param(query, "redirect_uri"));
                    var state = Uri.UnescapeDataString(Param(query, "state"));
                    await Task.Delay(100);
                    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                    await http.GetAsync($"{redirect}?code=auth-code&state={state}");
                }
                catch
                {
                    // surfaced by the assertion below
                }
            });
            return Task.CompletedTask;
        };

        // FreePort has a small bind race; retry on a fresh port if the listener
        // did not come up.
        var ok = false;
        for (var attempt = 0; attempt < 3 && !ok; attempt++)
        {
            settings.Current = new AppSettings { CloudEnabled = false, CloudBaseUrl = "https://cloud.dorado.example/" };
            var service = new CloudSignInService(settings, pkce, browser, port: FreePort(), timeout: TimeSpan.FromSeconds(10));
            ok = await service.SignInAsync();
        }

        Assert.True(ok);
        Assert.True(settings.Current.CloudEnabled);
        Assert.Equal("access-123", settings.Current.CloudAccessToken);
        Assert.NotNull(settings.Current.CloudAccessTokenExpiresAtUtc);
    }

    [Fact]
    public async Task SignIn_ReturnsFalseWhenBaseUrlMissing()
    {
        var settings = new FakeSettingsStore(new AppSettings());
        var service = new CloudSignInService(settings, new OAuthPkceService(), _ => Task.CompletedTask);

        Assert.False(await service.SignInAsync());
    }

    [Fact]
    public void SignOut_ClearsToken()
    {
        var settings = new FakeSettingsStore(new AppSettings { CloudEnabled = true, CloudAccessToken = "tok" });
        var service = new CloudSignInService(settings, new OAuthPkceService());

        service.SignOut();

        Assert.Equal(string.Empty, settings.Current.CloudAccessToken);
        Assert.Null(settings.Current.CloudAccessTokenExpiresAtUtc);
    }

    private static string Param(string query, string key) =>
        query.TrimStart('?').Split('&')
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2 && p[0] == key)
            .Select(p => p[1])
            .FirstOrDefault() ?? string.Empty;

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

internal sealed class FakeSettingsStore : ISettingsStore
{
    public FakeSettingsStore(AppSettings initial) => Current = initial;

    public AppSettings Current { get; set; }

    public AppSettings Load() => Current;

    public void Save(AppSettings settings) => Current = settings;
}
