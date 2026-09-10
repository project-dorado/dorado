using Dorado.Domain.Models;

namespace Dorado.Application.Services;

/// <summary>
/// Derives a deterministic audio-feature prior from track metadata (genre, id).
/// This is a replaceable metadata prior, not a DSP extractor: a future implementation
/// can compute the same <see cref="AudioFeatures"/> shape from decoded PCM without any
/// downstream changes. Values are normalized to 0..1 except BPM.
/// </summary>
public static class AudioFeatureExtractor
{
    public static AudioFeatures Extract(Track track)
    {
        var genre = (track.Genre ?? string.Empty).ToLowerInvariant();

        var energy = 0.5;
        var valence = 0.5;
        var acousticness = 0.3;
        var danceability = 0.5;
        var bpm = 120.0;

        if (Has(genre, "metal", "punk", "hardcore")) { energy += 0.3; valence -= 0.2; bpm = 140; }
        if (Has(genre, "rock")) { energy += 0.15; valence += 0.05; bpm = 125; }
        if (Has(genre, "electronic", "edm", "house", "techno", "trance")) { energy += 0.2; danceability += 0.3; valence += 0.1; bpm = 128; }
        if (Has(genre, "dance", "disco", "funk")) { danceability += 0.3; valence += 0.2; bpm = 118; }
        if (Has(genre, "hip", "rap")) { danceability += 0.2; energy += 0.1; bpm = 92; }
        if (Has(genre, "pop")) { valence += 0.2; danceability += 0.15; bpm = 116; }
        if (Has(genre, "jazz", "blues")) { acousticness += 0.3; valence += 0.05; bpm = 110; }
        if (Has(genre, "classical", "orchestra", "opera")) { acousticness += 0.5; energy -= 0.2; bpm = 90; }
        if (Has(genre, "folk", "acoustic", "country", "singer-songwriter")) { acousticness += 0.4; energy -= 0.1; bpm = 100; }
        if (Has(genre, "ambient", "drone", "new age")) { energy -= 0.25; acousticness += 0.2; bpm = 80; }
        if (Has(genre, "soul", "r&b", "rnb")) { valence += 0.15; danceability += 0.1; bpm = 102; }

        // Deterministic per-track jitter so identical-genre tracks are not identical vectors.
        var jitter = (track.Id.GetHashCode() & 0xFF) / 255.0;
        energy = Clamp01(energy + (jitter - 0.5) * 0.12);
        valence = Clamp01(valence + (jitter - 0.5) * 0.10);
        bpm = Math.Clamp(bpm + (jitter - 0.5) * 12.0, 60, 200);
        var spectralCentroid = Clamp01(0.25 + (energy * 0.5) + (jitter - 0.5) * 0.1);

        return new AudioFeatures
        {
            TrackId = track.Id,
            Bpm = bpm,
            Energy = energy,
            Valence = valence,
            Acousticness = Clamp01(acousticness),
            Danceability = Clamp01(danceability),
            SpectralCentroid = spectralCentroid
        };
    }

    private static bool Has(string genre, params string[] tokens)
        => tokens.Any(token => genre.Contains(token, StringComparison.Ordinal));

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}
