namespace Dorado.Application.Models;

public class ArtistMetadataResult
{
    public string Name { get; set; } = string.Empty;
    public string? MusicBrainzId { get; set; }
    public string? Biography { get; set; }
    public string? BiographySource { get; set; }
    public string? ThumbnailUrl { get; set; }
    public List<string> BackgroundImageUrls { get; set; } = new();
}

public class AlbumArtworkResult
{
    public string AlbumTitle { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string? MusicBrainzReleaseGroupId { get; set; }
    public string? ArtworkUrl { get; set; }
}

public class TrackMatchCandidate
{
    public Guid TrackId { get; set; }
    public string OriginalTitle { get; set; } = string.Empty;
    public string MatchedTitle { get; set; } = string.Empty;
    public string MatchedArtist { get; set; } = string.Empty;
    public long? MatchedDurationMs { get; set; }
    public string? MusicBrainzRecordingId { get; set; }
    public int Score { get; set; }
}

public class LyricsResult
{
    public string TrackName { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string? PlainLyrics { get; set; }
    public string? SyncedLyrics { get; set; }

    public bool Found => !string.IsNullOrWhiteSpace(PlainLyrics) || !string.IsNullOrWhiteSpace(SyncedLyrics);
}

public class ArtistEnrichmentSnapshot
{
    public string ArtistName { get; set; } = string.Empty;
    public string? Biography { get; set; }
    public string? BiographySource { get; set; }
    public IReadOnlyList<string> BackdropLocalPaths { get; set; } = Array.Empty<string>();
}
