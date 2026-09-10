using Dorado.Application.Interfaces;
using Dorado.Application.Services;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;

namespace Dorado.Tests.Application;

public class AudioFeatureExtractorTests
{
    [Fact]
    public void Extraction_is_deterministic()
    {
        var track = new Track { Title = "X", Genre = "Electronic" };
        Assert.Equal(AudioFeatureExtractor.Extract(track).ToVector(), AudioFeatureExtractor.Extract(track).ToVector());
    }

    [Fact]
    public void Genre_lexicon_shapes_features()
    {
        var electronic = AudioFeatureExtractor.Extract(new Track { Genre = "House / EDM" });
        var classical = AudioFeatureExtractor.Extract(new Track { Genre = "Classical Orchestral" });

        Assert.True(electronic.Danceability > classical.Danceability);
        Assert.True(electronic.Energy > classical.Energy);
        Assert.True(classical.Acousticness > electronic.Acousticness);
    }
}

public class AudioAnalysisServiceTests
{
    private static Track Track(string title, string genre, int plays = 0, HeartRating rating = HeartRating.None)
        => new() { Title = title, Genre = genre, PlayCount = plays, Rating = rating };

    [Fact]
    public async Task Similar_tracks_rank_above_dissimilar()
    {
        var service = new AudioAnalysisService();
        var seed = Track("seed", "Electronic Dance");
        var similar = Track("similar", "House EDM");
        var dissimilar = Track("dissimilar", "Classical Orchestral");
        var library = new[] { seed, similar, dissimilar };

        var result = await service.FindSimilarAsync(seed, library, count: 2);

        Assert.DoesNotContain(seed, result);
        Assert.Equal("similar", result[0].Title);
    }

    [Fact]
    public async Task Favorites_centroid_steers_recommendations()
    {
        var service = new AudioAnalysisService();
        var library = new[]
        {
            Track("fav", "Metal", rating: HeartRating.Favorite),
            Track("loud", "Punk Hardcore"),
            Track("calm", "Ambient Drone")
        };

        var result = await service.FindSimilarToFavoritesAsync(
            library.Where(t => t.Rating == HeartRating.Favorite).ToList(), library, count: 1);

        Assert.Equal("loud", result[0].Title);
    }

    [Fact]
    public async Task Features_persist_through_the_store()
    {
        var store = new InMemoryFeatureStore();
        var track = Track("persist", "Jazz Blues");

        await new AudioAnalysisService(store).AnalyzeAsync(track);
        var reloaded = await new AudioAnalysisService(store).GetFeaturesAsync();

        Assert.True(reloaded.ContainsKey(track.Id));
    }
}

public class DynamicMixServiceTests
{
    private static Track Track(string title, string artist, string genre, int plays = 0, HeartRating rating = HeartRating.None)
        => new() { Title = title, ArtistName = artist, Genre = genre, PlayCount = plays, Rating = rating };

    [Fact]
    public async Task Top_played_orders_by_play_count()
    {
        var service = new DynamicMixService(new AudioAnalysisService());
        var library = new[] { Track("a", "A", "Rock", plays: 2), Track("b", "B", "Rock", plays: 9), Track("c", "C", "Rock", plays: 5) };

        var result = await service.MaterializeAsync(new DynamicMix { Kind = DynamicMixKind.TopPlayed, TrackLimit = 2 }, library);

        Assert.Equal(new[] { "b", "c" }, result.Select(t => t.Title));
    }

    [Fact]
    public async Task Playlists_including_artist_filters_by_artist()
    {
        var service = new DynamicMixService(new AudioAnalysisService());
        var library = new[] { Track("a", "Rush", "Rock"), Track("b", "Daft Punk", "Electronic") };

        var result = await service.MaterializeAsync(
            new DynamicMix { Kind = DynamicMixKind.PlaylistsIncludingArtist, SeedText = "rush", TrackLimit = 10 }, library);

        Assert.Single(result);
        Assert.Equal("a", result[0].Title);
    }

    [Fact]
    public async Task Similar_to_track_excludes_seed()
    {
        var service = new DynamicMixService(new AudioAnalysisService());
        var seed = Track("seed", "Rush", "Progressive Rock");
        var library = new[] { seed, Track("x", "Yes", "Progressive Rock"), Track("y", "Miles", "Jazz") };

        var result = await service.MaterializeAsync(
            new DynamicMix { Kind = DynamicMixKind.SimilarToTrack, SeedId = seed.Id, TrackLimit = 5 }, library);

        Assert.DoesNotContain(seed, result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void Default_mixes_are_built()
    {
        var mixes = new DynamicMixService(new AudioAnalysisService()).BuildDefaultMixes();
        Assert.Contains(mixes, m => m.Kind == DynamicMixKind.TopPlayed);
        Assert.Contains(mixes, m => m.Kind == DynamicMixKind.SimilarToFavorites);
    }
}

internal sealed class InMemoryFeatureStore : IAudioFeatureStore
{
    private readonly Dictionary<Guid, AudioFeatures> _data = new();

    public Task<IReadOnlyDictionary<Guid, AudioFeatures>> LoadAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, AudioFeatures>>(new Dictionary<Guid, AudioFeatures>(_data));

    public Task SaveAsync(AudioFeatures features, CancellationToken cancellationToken = default)
    {
        _data[features.TrackId] = features;
        return Task.CompletedTask;
    }
}
