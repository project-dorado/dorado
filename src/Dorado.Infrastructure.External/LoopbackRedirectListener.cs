using System.Net;
using System.Text;

namespace Dorado.Infrastructure.External;

/// <summary>
/// A loopback HTTP listener that captures the OAuth <c>?code=</c> the browser is
/// redirected to during interactive sign-in. Binds 127.0.0.1 only and serves a
/// short confirmation page.
/// </summary>
public sealed class LoopbackRedirectListener : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly string _path;
    private bool _started;

    public LoopbackRedirectListener(int port = 7890, string path = "/callback")
    {
        _path = path.StartsWith('/') ? path : "/" + path;
        RedirectUri = $"http://127.0.0.1:{port}{_path}";
        // Bind the root prefix so the exact redirect path (no trailing slash) matches.
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    }

    /// <summary>The redirect URI to register with the authorization request.</summary>
    public string RedirectUri { get; }

    /// <summary>The <c>state</c> echoed back by the authorization server.</summary>
    public string? State { get; private set; }

    /// <summary>Starts listening. Idempotent; call before opening the browser.</summary>
    public void Start()
    {
        if (!_started)
        {
            _listener.Start();
            _started = true;
        }
    }

    /// <summary>Waits for the redirect and returns the authorization code (or null on timeout/mismatch).</summary>
    public async Task<string?> WaitForCodeAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        Start();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            var contextTask = _listener.GetContextAsync();
            var completed = await Task.WhenAny(contextTask, Task.Delay(Timeout.Infinite, cts.Token)).ConfigureAwait(false);
            if (completed != contextTask)
            {
                return null;
            }

            var context = await contextTask.ConfigureAwait(false);
            var query = context.Request.QueryString;
            var code = query["code"];
            State = query["state"];

            if (!string.Equals(context.Request.Url?.AbsolutePath, _path, StringComparison.OrdinalIgnoreCase))
            {
                await RespondAsync(context, 404, "Not found.").ConfigureAwait(false);
                return null;
            }

            await RespondAsync(context, 200, "You're signed in to Dorado. You can close this tab.").ConfigureAwait(false);
            return code;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (HttpListenerException)
        {
            return null;
        }
    }

    private static async Task RespondAsync(HttpListenerContext context, int status, string message)
    {
        var html = $"<!doctype html><html><body style=\"background:#11090F;color:#fff;font-family:system-ui,sans-serif;display:grid;place-items:center;height:100vh;margin:0\"><p>{message}</p></body></html>";
        var bytes = Encoding.UTF8.GetBytes(html);
        context.Response.StatusCode = status;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        context.Response.Close();
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            if (_started)
            {
                _listener.Stop();
            }
            _listener.Close();
        }
        catch
        {
            // already stopped
        }

        return ValueTask.CompletedTask;
    }
}
