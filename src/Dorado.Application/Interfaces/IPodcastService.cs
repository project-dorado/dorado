using Dorado.Domain.Models;

namespace Dorado.Application.Interfaces;

public interface IPodcastService
{
    Task<IReadOnlyList<PodcastSeries>> GetAllPodcastsAsync();
    Task<PodcastSeries?> GetPodcastByIdAsync(Guid id);
    Task<PodcastSeries> SubscribeAsync(string feedUrl);
    Task UnsubscribeAsync(Guid seriesId);
    Task MarkEpisodePlayedAsync(Guid episodeId, bool isPlayed = true);
    Task PlayEpisodeAsync(PodcastEpisode episode);

    /// <summary>
    /// Discovery search across the Dorado Cloud Directory (Podcast Index).
    /// Returns an empty list when the cloud is disabled or unreachable — the
    /// subscribe-by-URL path remains fully local.
    /// </summary>
    Task<IReadOnlyList<PodcastDirectoryEntry>> SearchDirectoryAsync(
        string query, int limit = 20, CancellationToken cancellationToken = default);
}
