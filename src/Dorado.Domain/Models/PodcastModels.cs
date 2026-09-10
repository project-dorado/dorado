namespace Dorado.Domain.Models;

public class PodcastSeries
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string FeedUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ArtworkUri { get; set; }
    public int EpisodeCount { get; set; }
    public int UnplayedCount { get; set; }
    public List<PodcastEpisode> Episodes { get; set; } = new();
}

public class PodcastEpisode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SeriesId { get; set; }
    public string SeriesTitle { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime PublishedAtUtc { get; set; } = DateTime.UtcNow;
    public TimeSpan Duration { get; set; }
    public string AudioUrl { get; set; } = string.Empty;
    public bool IsPlayed { get; set; }
    public bool IsDownloaded { get; set; }
}
