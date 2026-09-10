namespace Dorado.Domain.Models;

/// <summary>
/// Per-track audio feature vector used for similarity and recommendations. Values are
/// normalized 0..1 except <see cref="Bpm"/>. Populated by the analysis service; the
/// current extractor derives a metadata prior and is designed to be replaced by a
/// DSP-based extractor behind the same shape.
/// </summary>
public class AudioFeatures
{
    public Guid TrackId { get; set; }
    public double Bpm { get; set; }
    public double Energy { get; set; }
    public double Valence { get; set; }
    public double Acousticness { get; set; }
    public double Danceability { get; set; }
    public double SpectralCentroid { get; set; }
    public DateTime AnalyzedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Six-dimensional normalized vector for cosine similarity.</summary>
    public double[] ToVector() => new[]
    {
        Math.Clamp(Bpm / 200.0, 0.0, 1.0),
        Energy,
        Valence,
        Acousticness,
        Danceability,
        SpectralCentroid
    };
}

public enum DynamicMixKind
{
    SimilarToAlbum,
    SimilarToTrack,
    TopPlayed,
    SimilarToFavorites,
    PlaylistsIncludingArtist
}

/// <summary>
/// An auto-updating "Mix": a rule that materializes a track list from the library on
/// demand (rune-style dynamic playlist).
/// </summary>
public class DynamicMix
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DynamicMixKind Kind { get; set; } = DynamicMixKind.TopPlayed;
    public Guid? SeedId { get; set; }
    public string? SeedText { get; set; }
    public int TrackLimit { get; set; } = 50;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
