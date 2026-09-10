using Dorado.Domain.Enums;

namespace Dorado.Domain.Models;

public class Track
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public Guid ArtistId { get; set; }
    public string ArtistName { get; set; } = string.Empty;
    public Guid AlbumId { get; set; }
    public string AlbumTitle { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public int TrackNumber { get; set; }
    public int DiscNumber { get; set; } = 1;
    public int? Year { get; set; }
    public string Genre { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public HeartRating Rating { get; set; } = HeartRating.None;
    public bool IsFavorite => Rating == HeartRating.Favorite;
    public bool IsDisliked => Rating == HeartRating.Dislike;
    public bool IsNeutral => Rating == HeartRating.None;
    public int PlayCount { get; set; }
    public DateTime? LastPlayedAtUtc { get; set; }
    public string? ArtworkUri { get; set; }
    public string? MusicBrainzTrackId { get; set; }
    public string? MusicBrainzArtistId { get; set; }
    public double? ReplayGainTrackGainDb { get; set; }
    public double? ReplayGainTrackPeak { get; set; }
}

public class Album
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public Guid ArtistId { get; set; }
    public string ArtistName { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string Genre { get; set; } = string.Empty;
    public string? ArtworkUri { get; set; }
    public int TrackCount { get; set; }
    public bool IsPinned { get; set; }
    public DateTime? PinnedAtUtc { get; set; }
    public List<Track> Tracks { get; set; } = new();
}

public class Artist
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string SortName { get; set; } = string.Empty;
    public string? Biography { get; set; }
    public string? ThumbnailUri { get; set; }
    public string? BackgroundImageUri { get; set; }
    public string? MusicBrainzId { get; set; }
    public List<Album> Albums { get; set; } = new();
}

public class Playlist
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<Guid> TrackIds { get; set; } = new();
}

public class ZuneDevice
{
    public string SerialNumber { get; set; } = string.Empty;
    public string ModelName { get; set; } = "Zune";
    public string FirmwareVersion { get; set; } = "4.8";
    public long CapacityBytes { get; set; }
    public long FreeSpaceBytes { get; set; }
    public long MusicBytes { get; set; }
    public long VideoBytes { get; set; }
    public long PhotoBytes { get; set; }
    public long PodcastBytes { get; set; }
    public long SystemBytes { get; set; }
    public bool IsPaired { get; set; }
    public bool IsConnected { get; set; }
    public DeviceSyncState SyncState { get; set; } = DeviceSyncState.Disconnected;
}

public class SmartDJSeed
{
    public Guid? SeedTrackId { get; set; }
    public Guid? SeedAlbumId { get; set; }
    public Guid? SeedArtistId { get; set; }
    public string? SeedGenre { get; set; }
    public int TargetTrackCount { get; set; } = 25;
    public bool ExcludeDisliked { get; set; } = true;
}

public class PlayHistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TrackId { get; set; }
    public string TrackTitle { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string AlbumTitle { get; set; } = string.Empty;
    public string? ArtworkUri { get; set; }
    public DateTime PlayedAtUtc { get; set; } = DateTime.UtcNow;
    public TimeSpan DurationPlayed { get; set; }
    public bool Completed { get; set; }
}
