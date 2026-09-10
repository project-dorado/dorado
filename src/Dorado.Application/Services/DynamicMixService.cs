using Dorado.Application.Interfaces;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;

namespace Dorado.Application.Services;

/// <summary>Materializes rune-style auto-updating Mixes from the current library.</summary>
public sealed class DynamicMixService : IDynamicMixService
{
    private readonly IAudioAnalysisService _analysis;

    public DynamicMixService(IAudioAnalysisService analysis)
    {
        _analysis = analysis;
    }

    public IReadOnlyList<DynamicMix> BuildDefaultMixes() => new[]
    {
        new DynamicMix { Name = "Most Played", Kind = DynamicMixKind.TopPlayed, TrackLimit = 100 },
        new DynamicMix { Name = "Favorites Mix", Kind = DynamicMixKind.SimilarToFavorites, TrackLimit = 50 }
    };

    public async Task<IReadOnlyList<Track>> MaterializeAsync(
        DynamicMix mix,
        IReadOnlyList<Track> library,
        CancellationToken cancellationToken = default)
    {
        switch (mix.Kind)
        {
            case DynamicMixKind.TopPlayed:
                return library
                    .OrderByDescending(t => t.PlayCount)
                    .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
                    .Take(mix.TrackLimit)
                    .ToList();

            case DynamicMixKind.SimilarToFavorites:
            {
                var favorites = library.Where(t => t.Rating == HeartRating.Favorite).ToList();
                return await _analysis
                    .FindSimilarToFavoritesAsync(favorites, library, mix.TrackLimit, cancellationToken)
                    .ConfigureAwait(false);
            }

            case DynamicMixKind.SimilarToTrack:
            {
                var seed = mix.SeedId is { } id ? library.FirstOrDefault(t => t.Id == id) : null;
                return seed is null
                    ? Array.Empty<Track>()
                    : await _analysis.FindSimilarAsync(seed, library, mix.TrackLimit, cancellationToken).ConfigureAwait(false);
            }

            case DynamicMixKind.SimilarToAlbum:
            {
                var seed = mix.SeedId is { } id ? library.FirstOrDefault(t => t.Id == id) : null;
                if (seed is null)
                {
                    return Array.Empty<Track>();
                }

                // Use the album's first track as the representative seed.
                var albumSeed = library.FirstOrDefault(t => string.Equals(t.AlbumTitle, seed.AlbumTitle, StringComparison.OrdinalIgnoreCase) && t.Id != seed.Id) ?? seed;
                return await _analysis.FindSimilarAsync(albumSeed, library, mix.TrackLimit, cancellationToken).ConfigureAwait(false);
            }

            case DynamicMixKind.PlaylistsIncludingArtist:
            {
                if (string.IsNullOrWhiteSpace(mix.SeedText))
                {
                    return Array.Empty<Track>();
                }

                return library
                    .Where(t => t.ArtistName.Contains(mix.SeedText, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(t => t.PlayCount)
                    .Take(mix.TrackLimit)
                    .ToList();
            }

            default:
                return Array.Empty<Track>();
        }
    }
}
