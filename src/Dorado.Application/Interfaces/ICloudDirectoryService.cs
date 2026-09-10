namespace Dorado.Application.Interfaces;

/// <summary>
/// Unified provider-directory search backed by the Dorado Cloud Directory
/// module (Podcast Index + Radio-Browser). Implemented in Infrastructure over
/// the typed cloud client; a no-op/offline default keeps the app functional.
/// </summary>
public interface ICloudDirectoryService
{
    bool IsEnabled { get; }

    Task<IReadOnlyList<PodcastDirectoryEntry>> SearchPodcastsAsync(
        string query, int limit = 20, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RadioDirectoryEntry>> SearchRadioAsync(
        string? query = null, string? country = null, string? tag = null,
        int limit = 30, CancellationToken cancellationToken = default);
}

/// <summary>A podcast-directory result (Podcast Index).</summary>
public sealed class PodcastDirectoryEntry
{
    public string FeedId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string FeedUrl { get; set; } = string.Empty;
    public string Categories { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
}

/// <summary>A radio-directory result (Radio-Browser).</summary>
public sealed class RadioDirectoryEntry
{
    public string StationId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Favicon { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public string Codec { get; set; } = string.Empty;
    public int Bitrate { get; set; }
    public int Votes { get; set; }
}

/// <summary>Offline default: reports disabled and returns no results.</summary>
public sealed class NullCloudDirectoryService : ICloudDirectoryService
{
    public bool IsEnabled => false;

    public Task<IReadOnlyList<PodcastDirectoryEntry>> SearchPodcastsAsync(
        string query, int limit = 20, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PodcastDirectoryEntry>>(Array.Empty<PodcastDirectoryEntry>());

    public Task<IReadOnlyList<RadioDirectoryEntry>> SearchRadioAsync(
        string? query = null, string? country = null, string? tag = null,
        int limit = 30, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<RadioDirectoryEntry>>(Array.Empty<RadioDirectoryEntry>());
}
