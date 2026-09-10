using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dorado.Tests.Application;

/// <summary>
/// Deterministic in-memory HTTP handler for offline tests of the online enrichment pipeline.
/// Routes are matched against request URL prefixes in registration order.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly List<(Func<string, bool> Predicate, Func<HttpResponseMessage> Response)> _routes = new();

    public List<string> RequestedUrls { get; } = new();

    public int RequestCount => RequestedUrls.Count;

    public void MapJson(Func<string, bool> predicate, string json)
    {
        _routes.Add((predicate, () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        }));
    }

    public void MapImage(Func<string, bool> predicate, byte[] bytes)
    {
        _routes.Add((predicate, () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg") }
            }
        }));
    }

    public void MapStatus(Func<string, bool> predicate, HttpStatusCode status)
    {
        _routes.Add((predicate, () => new HttpResponseMessage(status)));
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.ToString() ?? string.Empty;
        lock (RequestedUrls)
        {
            RequestedUrls.Add(url);
        }

        foreach (var (predicate, response) in _routes)
        {
            if (predicate(url))
            {
                return Task.FromResult(response());
            }
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
