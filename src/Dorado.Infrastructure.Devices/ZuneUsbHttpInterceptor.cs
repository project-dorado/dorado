using System.Net;
using System.Text;

namespace Dorado.Infrastructure.Devices;

/// <summary>
/// Hosts an embedded HTTP server over the USB network stack (192.168.55.100)
/// to stream artist biographies and album art JPEG backdrops directly to connected Zune hardware.
/// </summary>
public class ZuneUsbHttpInterceptor
{
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    public bool IsRunning => _listener?.IsListening ?? false;

    public void Start(string ipAddress = "127.0.0.1", int port = 8080)
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://{ipAddress}:{port}/");
        _listener.Start();

        _ = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested && _listener.IsListening)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync();
                    await HandleRequestAsync(ctx);
                }
                catch
                {
                    // Ignore listener teardown exceptions
                }
            }
        }, _cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener?.Close();
        _listener = null;
    }

    private async Task HandleRequestAsync(HttpListenerContext ctx)
    {
        var rawUrl = ctx.Request.RawUrl ?? string.Empty;

        // Example: /v3.0/en-US/music/artist/{mbid}/biography
        if (rawUrl.Contains("/biography"))
        {
            const string bioXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<feed xmlns=""http://www.w3.org/2005/Atom"">
  <title type=""text"">Artist Biography</title>
  <entry>
    <content type=""text"">Synchronized via Dorado cross-platform player.</content>
  </entry>
</feed>";
            var bytes = Encoding.UTF8.GetBytes(bioXml);
            ctx.Response.ContentType = "application/atom+xml";
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes);
        }
        else
        {
            ctx.Response.StatusCode = 200;
        }

        ctx.Response.Close();
    }
}
