using System.Text.Json.Serialization;

namespace Dorado.Plugins.Protocol.Dto;

public class TrackDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("artist")]
    public string Artist { get; set; } = string.Empty;

    [JsonPropertyName("album")]
    public string Album { get; set; } = string.Empty;

    [JsonPropertyName("durationMs")]
    public long DurationMs { get; set; }

    [JsonPropertyName("rating")]
    public string Rating { get; set; } = "None";

    [JsonPropertyName("artworkUri")]
    public string? ArtworkUri { get; set; }

    [JsonPropertyName("musicBrainzTrackId")]
    public string? MusicBrainzTrackId { get; set; }

    [JsonPropertyName("musicBrainzArtistId")]
    public string? MusicBrainzArtistId { get; set; }
}

public class TrackChangedDto
{
    [JsonPropertyName("track")]
    public TrackDto? Track { get; set; }

    [JsonPropertyName("positionMs")]
    public long PositionMs { get; set; }

    [JsonPropertyName("isPlaying")]
    public bool IsPlaying { get; set; }
}

public class PlaybackStateDto
{
    [JsonPropertyName("state")]
    public string State { get; set; } = "Stopped";

    [JsonPropertyName("positionMs")]
    public long PositionMs { get; set; }
}

public class RatingChangedDto
{
    [JsonPropertyName("trackId")]
    public Guid TrackId { get; set; }

    [JsonPropertyName("rating")]
    public string Rating { get; set; } = "None";
}

public class ShowToastDto
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("durationMs")]
    public int DurationMs { get; set; } = 3000;
}
