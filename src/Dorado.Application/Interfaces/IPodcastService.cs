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
}
