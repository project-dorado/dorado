using Dorado.Application.Interfaces;
using Dorado.Application.Services;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;
using Xunit;

namespace Dorado.Tests.Application;

public class PlaybackQueueCoordinatorTests
{
    [Fact]
    public async Task PlayTrackAsync_SetsCurrentTrackAndState()
    {
        var coordinator = new PlaybackQueueCoordinator();
        var track = new Track { Title = "Tom Sawyer", ArtistName = "Rush" };

        await coordinator.PlayTrackAsync(track);

        Assert.Equal(PlaybackState.Playing, coordinator.State);
        Assert.NotNull(coordinator.CurrentTrack);
        Assert.Equal("Tom Sawyer", coordinator.CurrentTrack!.Title);
    }

    [Fact]
    public async Task NextAsync_AdvancesQueue()
    {
        var coordinator = new PlaybackQueueCoordinator();
        var track1 = new Track { Title = "Track 1" };
        var track2 = new Track { Title = "Track 2" };

        await coordinator.PlayTrackAsync(track1, new[] { track1, track2 });
        Assert.Equal("Track 1", coordinator.CurrentTrack!.Title);

        await coordinator.NextAsync();
        Assert.Equal("Track 2", coordinator.CurrentTrack!.Title);
    }

    [Fact]
    public async Task SetRatingAsync_UpdatesTrackRatingInQueue()
    {
        var coordinator = new PlaybackQueueCoordinator();
        var track = new Track { Title = "Favorite Song", Rating = HeartRating.None };

        await coordinator.PlayTrackAsync(track);
        await coordinator.SetRatingAsync(track.Id, HeartRating.Favorite);

        Assert.Equal(HeartRating.Favorite, coordinator.CurrentTrack!.Rating);
    }

    [Fact]
    public async Task SmartDJEngine_ExcludesDislikedTracks()
    {
        var engine = new SmartDJEngine();
        var artistId = Guid.NewGuid();

        var tracks = new List<Track>
        {
            new() { Title = "Good Song", ArtistId = artistId, Genre = "Rock", Rating = HeartRating.Favorite },
            new() { Title = "Disliked Song", ArtistId = artistId, Genre = "Rock", Rating = HeartRating.Dislike },
            new() { Title = "Neutral Song", ArtistId = artistId, Genre = "Rock", Rating = HeartRating.None }
        };

        var seed = new SmartDJSeed
        {
            SeedArtistId = artistId,
            ExcludeDisliked = true,
            TargetTrackCount = 10
        };

        var mix = await engine.GenerateMixAsync(seed, tracks);

        Assert.DoesNotContain(mix, t => t.Rating == HeartRating.Dislike);
        Assert.Contains(mix, t => t.Title == "Good Song");
    }
}
