using Dorado.Domain.Models;

namespace Dorado.Application.Interfaces;

/// <summary>Computes and stores per-track audio features and answers similarity queries.</summary>
public interface IAudioAnalysisService
{
    Task<AudioFeatures> AnalyzeAsync(Track track, CancellationToken cancellationToken = default);

    Task AnalyzeAllAsync(IEnumerable<Track> tracks, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, AudioFeatures>> GetFeaturesAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns up to <paramref name="count"/> library tracks most similar to the seed.</summary>
    Task<IReadOnlyList<Track>> FindSimilarAsync(
        Track seed,
        IReadOnlyList<Track> library,
        int count = 25,
        CancellationToken cancellationToken = default);

    /// <summary>Returns tracks most similar to the centroid of the given favorites.</summary>
    Task<IReadOnlyList<Track>> FindSimilarToFavoritesAsync(
        IReadOnlyList<Track> favorites,
        IReadOnlyList<Track> library,
        int count = 50,
        CancellationToken cancellationToken = default);
}
