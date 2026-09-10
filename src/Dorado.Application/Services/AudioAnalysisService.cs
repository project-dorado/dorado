using Dorado.Application.Interfaces;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;

namespace Dorado.Application.Services;

/// <summary>
/// In-memory feature cache backed by an optional store. Similarity is cosine distance
/// over the normalized feature vector.
/// </summary>
public sealed class AudioAnalysisService : IAudioAnalysisService
{
    private readonly IAudioFeatureStore? _store;
    private readonly Dictionary<Guid, AudioFeatures> _features = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _loaded;

    public AudioAnalysisService(IAudioFeatureStore? store = null)
    {
        _store = store;
    }

    public async Task<AudioFeatures> AnalyzeAsync(Track track, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        var features = AudioFeatureExtractor.Extract(track);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _features[track.Id] = features;
        }
        finally
        {
            _gate.Release();
        }

        if (_store is not null)
        {
            await _store.SaveAsync(features, cancellationToken).ConfigureAwait(false);
        }

        return features;
    }

    public async Task AnalyzeAllAsync(IEnumerable<Track> tracks, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        foreach (var track in tracks)
        {
            if (!_features.ContainsKey(track.Id))
            {
                await AnalyzeAsync(track, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task<IReadOnlyDictionary<Guid, AudioFeatures>> GetFeaturesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        return new Dictionary<Guid, AudioFeatures>(_features);
    }

    public async Task<IReadOnlyList<Track>> FindSimilarAsync(
        Track seed,
        IReadOnlyList<Track> library,
        int count = 25,
        CancellationToken cancellationToken = default)
    {
        await AnalyzeAllAsync(library, cancellationToken).ConfigureAwait(false);
        var seedVector = VectorFor(seed);
        return Rank(library.Where(t => t.Id != seed.Id), seedVector, count);
    }

    public async Task<IReadOnlyList<Track>> FindSimilarToFavoritesAsync(
        IReadOnlyList<Track> favorites,
        IReadOnlyList<Track> library,
        int count = 50,
        CancellationToken cancellationToken = default)
    {
        if (favorites.Count == 0)
        {
            return library
                .OrderByDescending(t => t.Rating == HeartRating.Favorite)
                .ThenByDescending(t => t.PlayCount)
                .Take(count)
                .ToList();
        }

        await AnalyzeAllAsync(library, cancellationToken).ConfigureAwait(false);

        var dimensions = 6;
        var centroid = new double[dimensions];
        foreach (var favorite in favorites)
        {
            var vector = VectorFor(favorite);
            for (var i = 0; i < dimensions; i++)
            {
                centroid[i] += vector[i];
            }
        }

        for (var i = 0; i < dimensions; i++)
        {
            centroid[i] /= favorites.Count;
        }

        var favoriteIds = favorites.Select(f => f.Id).ToHashSet();
        return Rank(library.Where(t => !favoriteIds.Contains(t.Id)), centroid, count);
    }

    private IReadOnlyList<Track> Rank(IEnumerable<Track> library, double[] target, int count)
        => library
            .Select(track => (Track: track, Score: Cosine(target, VectorFor(track))))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Track.Title, StringComparer.OrdinalIgnoreCase)
            .Take(count)
            .Select(x => x.Track)
            .ToList();

    private double[] VectorFor(Track track)
        => _features.TryGetValue(track.Id, out var features)
            ? features.ToVector()
            : AudioFeatureExtractor.Extract(track).ToVector();

    private static double Cosine(double[] a, double[] b)
    {
        double dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        if (magA == 0 || magB == 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_loaded)
            {
                return;
            }

            if (_store is not null)
            {
                var stored = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
                foreach (var pair in stored)
                {
                    _features[pair.Key] = pair.Value;
                }
            }

            _loaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }
}
