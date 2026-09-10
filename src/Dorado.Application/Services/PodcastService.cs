using System.Xml.Linq;
using Dorado.Application.Interfaces;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;

namespace Dorado.Application.Services;

public class PodcastService : IPodcastService
{
    private readonly IPlayerCoordinator _playerCoordinator;
    private readonly List<PodcastSeries> _podcasts = new();
    private readonly HttpClient _httpClient = new();

    public PodcastService(IPlayerCoordinator playerCoordinator)
    {
        _playerCoordinator = playerCoordinator;
        SeedDefaultPodcasts();
    }

    private void SeedDefaultPodcasts()
    {
        var npr = new PodcastSeries
        {
            Title = "All Songs Considered",
            Author = "NPR Music",
            FeedUrl = "https://feeds.npr.org/510019/podcast.xml",
            Description = "The weekly music podcast from NPR Music where Bob Boilen and Robin Hilton explore new music and interview visionary artists.",
            EpisodeCount = 3,
            UnplayedCount = 2,
            Episodes = new List<PodcastEpisode>
            {
                new()
                {
                    SeriesTitle = "All Songs Considered",
                    Title = "The Best New Music of the Week: Visionary Indie & Electronic",
                    Description = "Featuring premier singles from upcoming albums, discussing sound design and sonic storytelling.",
                    PublishedAtUtc = DateTime.UtcNow.AddDays(-2),
                    Duration = TimeSpan.FromMinutes(42) + TimeSpan.FromSeconds(15),
                    AudioUrl = "https://play.podtrac.com/npr-510019/npr.org/music/episode1.mp3",
                    IsPlayed = false
                },
                new()
                {
                    SeriesTitle = "All Songs Considered",
                    Title = "Guest DJ: Reminiscing the Golden Era of Portable Media",
                    Description = "A special retrospective on early 2000s music discovery, personal hardware players, and tactile listening.",
                    PublishedAtUtc = DateTime.UtcNow.AddDays(-6),
                    Duration = TimeSpan.FromMinutes(35) + TimeSpan.FromSeconds(50),
                    AudioUrl = "https://play.podtrac.com/npr-510019/npr.org/music/episode2.mp3",
                    IsPlayed = true
                },
                new()
                {
                    SeriesTitle = "All Songs Considered",
                    Title = "Deep Dive: Progressive Rock and Synth Mastery",
                    Description = "Exploring complex polyrhythms, analog synthesizers, and concept albums that reshaped music history.",
                    PublishedAtUtc = DateTime.UtcNow.AddDays(-12),
                    Duration = TimeSpan.FromMinutes(51) + TimeSpan.FromSeconds(20),
                    AudioUrl = "https://play.podtrac.com/npr-510019/npr.org/music/episode3.mp3",
                    IsPlayed = false
                }
            }
        };

        var kexp = new PodcastSeries
        {
            Title = "Music That Matters",
            Author = "KEXP 90.3 FM",
            FeedUrl = "https://kexp.org/podcasts/music-that-matters.xml",
            Description = "Handcrafted mixes curated by passionate KEXP DJs highlighting the best independent artists from Seattle and across the globe.",
            EpisodeCount = 2,
            UnplayedCount = 2,
            Episodes = new List<PodcastEpisode>
            {
                new()
                {
                    SeriesTitle = "Music That Matters",
                    Title = "Midnight Echoes: Post-Punk, Darkwave & Synth-Pop Gems",
                    Description = "Moody basslines, driving drum machines, and atmospheric melodies to carry you into the evening.",
                    PublishedAtUtc = DateTime.UtcNow.AddDays(-1),
                    Duration = TimeSpan.FromMinutes(64) + TimeSpan.FromSeconds(10),
                    AudioUrl = "https://kexp.org/podcasts/mtm_ep1.mp3",
                    IsPlayed = false
                },
                new()
                {
                    SeriesTitle = "Music That Matters",
                    Title = "Harmonic Horizons: Ambient, Modern Classical & Chillout",
                    Description = "Immersive soundscapes and reflective instrumentation for focused listening and quiet reflection.",
                    PublishedAtUtc = DateTime.UtcNow.AddDays(-8),
                    Duration = TimeSpan.FromMinutes(58) + TimeSpan.FromSeconds(40),
                    AudioUrl = "https://kexp.org/podcasts/mtm_ep2.mp3",
                    IsPlayed = false
                }
            }
        };

        _podcasts.Add(npr);
        _podcasts.Add(kexp);
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
        try
        {
            var xmlString = await _httpClient.GetStringAsync(feedUrl);
            var doc = XDocument.Parse(xmlString);
            var channel = doc.Descendants("channel").FirstOrDefault();

            var series = new PodcastSeries
            {
                Title = channel?.Element("title")?.Value ?? "Untitled Podcast",
                Author = channel?.Element("author")?.Value ?? channel?.Element(XName.Get("author", "http://www.itunes.com/dtds/podcast-1.0.dtd"))?.Value ?? "Unknown Author",
                FeedUrl = feedUrl,
                Description = channel?.Element("description")?.Value ?? string.Empty,
                ArtworkUri = channel?.Element("image")?.Element("url")?.Value
            };

            var items = doc.Descendants("item").Take(20);
            foreach (var item in items)
            {
                var enc = item.Element("enclosure");
                series.Episodes.Add(new PodcastEpisode
                {
                    SeriesId = series.Id,
                    SeriesTitle = series.Title,
                    Title = item.Element("title")?.Value ?? "Untitled Episode",
                    Description = item.Element("description")?.Value ?? string.Empty,
                    PublishedAtUtc = DateTime.TryParse(item.Element("pubDate")?.Value, out var dt) ? dt.ToUniversalTime() : DateTime.UtcNow,
                    Duration = TimeSpan.FromMinutes(30),
                    AudioUrl = enc?.Attribute("url")?.Value ?? string.Empty,
                    IsPlayed = false
                });
            }

            series.EpisodeCount = series.Episodes.Count;
            series.UnplayedCount = series.Episodes.Count;
            _podcasts.Add(series);
            return series;
        }
        catch
        {
            // Fallback mock subscription if feed cannot be reached
            var fallback = new PodcastSeries
            {
                Title = "Subscribed Podcast",
                Author = "Web Feed",
                FeedUrl = feedUrl,
                Description = $"Subscribed podcast feed from {feedUrl}",
                EpisodeCount = 1,
                UnplayedCount = 1,
                Episodes = new List<PodcastEpisode>
                {
                    new()
                    {
                        SeriesTitle = "Subscribed Podcast",
                        Title = "Latest Episode",
                        Description = "Podcast episode audio stream.",
                        PublishedAtUtc = DateTime.UtcNow,
                        Duration = TimeSpan.FromMinutes(25),
                        AudioUrl = feedUrl
                    }
                }
            };
            _podcasts.Add(fallback);
            return fallback;
        }
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
