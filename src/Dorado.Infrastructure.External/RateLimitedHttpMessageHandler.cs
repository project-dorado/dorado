using System.Net;

namespace Dorado.Infrastructure.External;

/// <summary>
/// Enforces a minimum spacing between outbound HTTP requests on a per-handler basis.
/// Used to honour MusicBrainz (1 req/s) and Cover Art Archive (1 req/s) courtesy policies.
/// </summary>
public sealed class RateLimitedHttpMessageHandler : DelegatingHandler
{
    private readonly TimeSpan _minimumInterval;
    private readonly object _gate = new();
    private DateTimeOffset _earliestNextSendUtc = DateTimeOffset.MinValue;

    public RateLimitedHttpMessageHandler(HttpMessageHandler? innerHandler, TimeSpan minimumInterval)
        : base(innerHandler ?? new HttpClientHandler())
    {
        _minimumInterval = minimumInterval < TimeSpan.Zero ? TimeSpan.Zero : minimumInterval;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        TimeSpan delay;
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            delay = _earliestNextSendUtc > now ? _earliestNextSendUtc - now : TimeSpan.Zero;
            _earliestNextSendUtc = now + delay + _minimumInterval;
        }

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
