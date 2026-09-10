using Dorado.Domain.Models;

namespace Dorado.Application.Interfaces;

/// <summary>Fetches and normalizes a podcast feed into a series.</summary>
public interface IPodcastFeedClient
{
    Task<PodcastSeries> GetSeriesAsync(string feedUrl, CancellationToken cancellationToken = default);
}
