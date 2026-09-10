using Dorado.Application.Interfaces;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;

namespace Dorado.Application.Services;

public class PodcastService : IPodcastService
{
    private readonly IPlayerCoordinator _playerCoordinator;
    private readonly IPodcastFeedClient? _feedClient;
    private readonly ICloudDirectoryService? _directory;
    private readonly List<PodcastSeries> _podcasts = new();
    private readonly HttpClient _httpClient = new();

    public PodcastService(
        IPlayerCoordinator playerCoordinator,
        IPodcastFeedClient? feedClient = null,
        ICloudDirectoryService? directory = null)
    {
        _playerCoordinator = playerCoordinator;
        _feedClient = feedClient;
        _directory = directory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PodcastDirectoryEntry>> SearchDirectoryAsync(
        string query, int limit = 20, CancellationToken cancellationToken = default)
    {
        if (_directory is null || !_directory.IsEnabled || string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<PodcastDirectoryEntry>();
        }

        return await _directory.SearchPodcastsAsync(query, limit, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<PodcastSeries>> GetAllPodcastsAsync()
    {
        return Task.FromResult<IReadOnlyList<PodcastSeries>>(_podcasts.AsReadOnly());
    }

    public Task<PodcastSeries?> GetPodcastByIdAsync(Guid id)
    {
        var found = _podcasts.FirstOrDefault(p => p.Id == id);
        return Task.FromResult(found);
    }

    public async Task<PodcastSeries> SubscribeAsync(string feedUrl)
    {
        PodcastSeries series;
        if (_feedClient is not null)
        {
            series = await _feedClient.GetSeriesAsync(feedUrl);
        }
        else
        {
            var xml = await _httpClient.GetStringAsync(feedUrl);
            series = PodcastFeedParser.Parse(xml, feedUrl);
        }

        _podcasts.Add(series);
        return series;
    }

    public Task UnsubscribeAsync(Guid seriesId)
    {
        _podcasts.RemoveAll(p => p.Id == seriesId);
        return Task.CompletedTask;
    }

    public Task MarkEpisodePlayedAsync(Guid episodeId, bool isPlayed = true)
    {
        foreach (var p in _podcasts)
        {
            var ep = p.Episodes.FirstOrDefault(e => e.Id == episodeId);
            if (ep != null)
            {
                ep.IsPlayed = isPlayed;
                p.UnplayedCount = p.Episodes.Count(e => !e.IsPlayed);
                break;
            }
        }
        return Task.CompletedTask;
    }

    public async Task PlayEpisodeAsync(PodcastEpisode episode)
    {
        var track = new Track
        {
            Title = episode.Title,
            ArtistName = episode.SeriesTitle,
            AlbumTitle = "Podcasts",
            Duration = episode.Duration,
            FilePath = episode.AudioUrl,
            Genre = "Podcast",
            Rating = HeartRating.None
        };
        await _playerCoordinator.PlayTrackAsync(track);
        await MarkEpisodePlayedAsync(episode.Id, true);
    }
}
