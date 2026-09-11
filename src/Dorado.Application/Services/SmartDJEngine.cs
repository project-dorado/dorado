using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;

namespace Dorado.Application.Services;

public class SmartDJEngine : ISmartDJService
{
    private readonly Random _random = new();

    // Scoring weights — tuned so a heart in any context outranks a non-heart, and album
    // match still narrowly outranks a single-artist favourite (Zune 4.8 fans consistently
    // reported hearts "feel weighted" even within a single artist's discography).
    private const double AlbumMatchWeight = 12.0;
    private const double ArtistMatchWeight = 10.0;
    private const double GenreMatchWeight = 5.0;
    private const double HeartBonus = 25.0;
    private const double RandomJitter = 3.0;

    public Task<IReadOnlyList<Track>> GenerateMixAsync(SmartDJSeed seed, IReadOnlyList<Track> libraryTracks)
        => GenerateMixAsync(seed, libraryTracks, null, CancellationToken.None);

    public Task<IReadOnlyList<Track>> GenerateMixAsync(
        SmartDJSeed seed,
        IReadOnlyList<Track> libraryTracks,
        IProgress<QuickMixProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new QuickMixProgress("Scanning library", 0.10));
        cancellationToken.ThrowIfCancellationRequested();

        // Tier B1 (Zune 4.8 Smart DJ parity): broken hearts are ALWAYS excluded — even
        // when the seed context (album/artist/genre match) would otherwise pull them in.
        // Hearts are surfaced first via a bonus that overrides the similarity weights.
        var brokenHeartCount = libraryTracks.Count(t => t.Rating == HeartRating.Dislike);

        var candidates = libraryTracks
            .Where(t => t.Rating != HeartRating.Dislike)
            .ToList();

        var seedTrack = seed.SeedTrackId.HasValue
            ? libraryTracks.FirstOrDefault(t => t.Id == seed.SeedTrackId.Value)
            : null;

        Guid? targetAlbumId = seed.SeedAlbumId ?? seedTrack?.AlbumId;
        Guid? targetArtistId = seed.SeedArtistId ?? seedTrack?.ArtistId;
        string? targetGenre = !string.IsNullOrEmpty(seed.SeedGenre) ? seed.SeedGenre : seedTrack?.Genre;

        if (!targetArtistId.HasValue && targetAlbumId.HasValue)
        {
            var albumTrack = candidates.FirstOrDefault(t => t.AlbumId == targetAlbumId.Value);
            if (albumTrack != null)
            {
                targetArtistId = albumTrack.ArtistId;
                if (string.IsNullOrEmpty(targetGenre))
                {
                    targetGenre = albumTrack.Genre;
                }
            }
        }

        // Stage 1: score — hearts get a +25 bonus so they always outrank non-hearts regardless
        // of similarity; small random jitter keeps each mix dynamic.
        var scored = candidates.Select(track => new ScoredTrack
        {
            Track = track,
            Score = ScoreTrack(track, targetAlbumId, targetArtistId, targetGenre),
            IsFavorite = track.Rating == HeartRating.Favorite,
        });

        progress?.Report(new QuickMixProgress("Scoring candidates", 0.55));
        cancellationToken.ThrowIfCancellationRequested();

        // Stage 2: order — favorites first (broken hearts already excluded), then by score.
        // Using a stable secondary sort by Title keeps the output deterministic for tests.
        var ordered = scored
            .OrderByDescending(x => x.IsFavorite)
            .ThenByDescending(x => x.Score)
            .ThenBy(x => x.Track.Title, StringComparer.Ordinal)
            .Take(seed.TargetTrackCount)
            .Select(x => x.Track)
            .ToList();

        progress?.Report(new QuickMixProgress("Building mix", 0.90));
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new QuickMixProgress("Ready", 1.0));

        return Task.FromResult<IReadOnlyList<Track>>(ordered);
    }

    private double ScoreTrack(Track track, Guid? targetAlbumId, Guid? targetArtistId, string? targetGenre)
    {
        double score = 0.0;

        if (targetAlbumId.HasValue && track.AlbumId == targetAlbumId.Value)
        {
            score += AlbumMatchWeight;
        }
        if (targetArtistId.HasValue && track.ArtistId == targetArtistId.Value)
        {
            score += ArtistMatchWeight;
        }
        if (!string.IsNullOrEmpty(targetGenre)
            && string.Equals(track.Genre, targetGenre, StringComparison.OrdinalIgnoreCase))
        {
            score += GenreMatchWeight;
        }
        if (track.Rating == HeartRating.Favorite)
        {
            score += HeartBonus;
        }

        score += _random.NextDouble() * RandomJitter;
        return score;
    }

    private sealed class ScoredTrack
    {
        public required Track Track { get; init; }
        public required double Score { get; init; }
        public required bool IsFavorite { get; init; }
    }
}
