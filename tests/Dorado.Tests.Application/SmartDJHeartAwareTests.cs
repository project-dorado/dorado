using Dorado.Application.Services;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Tier B1 — heart/broken-heart-aware Smart DJ shuffle. Hearts (favorites) are surfaced
/// first regardless of similarity; broken hearts are ALWAYS excluded from any mix
/// even when the seed context (album/artist/genre) would otherwise pull them in.
/// </summary>
public class SmartDJHeartAwareTests
{
    private static Track Track(
        string title,
        Guid? albumId = null,
        Guid? artistId = null,
        string? genre = null,
        HeartRating rating = HeartRating.None)
    {
        return new Track
        {
            Title = title,
            ArtistName = "Artist",
            AlbumTitle = "Album",
            AlbumId = albumId ?? Guid.NewGuid(),
            ArtistId = artistId ?? Guid.NewGuid(),
            Genre = genre ?? "Rock",
            Rating = rating,
            FilePath = $"/music/{title}.mp3"
        };
    }

    [Fact]
    public async Task BrokenHeartTracks_AreAlwaysExcluded_EvenIfTheyMatchTheSeedArtist()
    {
        var artist = Guid.NewGuid();
        var library = new[]
        {
            Track("Loved Hit",    artistId: artist, rating: HeartRating.Favorite),
            Track("Disliked Hit", artistId: artist, rating: HeartRating.Dislike),
            Track("Random Hit",   artistId: artist, rating: HeartRating.None),
        };

        var engine = new SmartDJEngine();
        var seed = new SmartDJSeed
        {
            SeedArtistId = artist,
            TargetTrackCount = 10,
            ExcludeDisliked = false, // Even with the flag off, broken hearts must still be excluded.
        };

        var mix = await engine.GenerateMixAsync(seed, library);

        Assert.DoesNotContain(mix, t => t.Title == "Disliked Hit");
        Assert.Contains(mix, t => t.Title == "Loved Hit");
    }

    [Fact]
    public async Task FavoriteTracks_SurfaceFirst_AcrossDifferentArtists()
    {
        var library = new[]
        {
            Track("Random From Artist A", rating: HeartRating.None),
            Track("Loved From Artist B",   rating: HeartRating.Favorite),
            Track("Random From Artist C", rating: HeartRating.None),
            Track("Loved From Artist D",   rating: HeartRating.Favorite),
        };

        var engine = new SmartDJEngine();
        var seed = new SmartDJSeed { TargetTrackCount = 10, ExcludeDisliked = true };

        var mix = await engine.GenerateMixAsync(seed, library);

        Assert.Equal(4, mix.Count);
        // The first two entries must both be hearts, regardless of which artist they came from.
        Assert.Equal(HeartRating.Favorite, mix[0].Rating);
        Assert.Equal(HeartRating.Favorite, mix[1].Rating);
        Assert.Equal(HeartRating.None, mix[2].Rating);
        Assert.Equal(HeartRating.None, mix[3].Rating);
    }

    [Fact]
    public async Task FavoriteAlbumMatch_Outranks_NonFavoriteAlbumMatch()
    {
        var artist = Guid.NewGuid();
        var favAlbum = Guid.NewGuid();
        var boringAlbum = Guid.NewGuid();

        var library = new[]
        {
            Track("Boring Album Track", artistId: artist, albumId: boringAlbum, rating: HeartRating.None),
            Track("Favourite Album Track", artistId: artist, albumId: favAlbum, rating: HeartRating.Favorite),
        };

        var engine = new SmartDJEngine();
        var seed = new SmartDJSeed { SeedArtistId = artist, TargetTrackCount = 5, ExcludeDisliked = true };

        var mix = await engine.GenerateMixAsync(seed, library);

        Assert.Equal(2, mix.Count);
        Assert.Equal("Favourite Album Track", mix[0].Title);
        Assert.Equal("Boring Album Track", mix[1].Title);
    }

    [Fact]
    public async Task MixedLibrary_OrdersByHeartFirst_ThenBySimilarity()
    {
        var artist = Guid.NewGuid();
        var favAlbum = Guid.NewGuid();
        var boringAlbum = Guid.NewGuid();

        var library = new[]
        {
            Track("Boring Album A", artistId: artist, albumId: boringAlbum),
            Track("Loved Album F",  artistId: artist, albumId: favAlbum, rating: HeartRating.Favorite),
            Track("Boring Album B", artistId: artist, albumId: boringAlbum),
            Track("Loved Single",    artistId: artist, rating: HeartRating.Favorite),
            Track("Broken Heart",    artistId: artist, rating: HeartRating.Dislike),
        };

        var engine = new SmartDJEngine();
        var seed = new SmartDJSeed { SeedArtistId = artist, TargetTrackCount = 10 };

        var mix = await engine.GenerateMixAsync(seed, library);

        Assert.Equal(4, mix.Count);
        Assert.DoesNotContain(mix, t => t.Title == "Broken Heart");
        Assert.Equal(HeartRating.Favorite, mix[0].Rating);
        Assert.Equal(HeartRating.Favorite, mix[1].Rating);
        Assert.Contains(mix, t => t.Title == "Loved Album F");
        Assert.Contains(mix, t => t.Title == "Loved Single");
    }

    [Fact]
    public async Task ResultRespectsTargetTrackCount_AndDislikesAreFiltered()
    {
        var library = Enumerable.Range(0, 50)
            .Select(i => Track($"Track {i:D3}", rating: i % 5 == 0 ? HeartRating.Dislike : HeartRating.None))
            .ToArray();

        var engine = new SmartDJEngine();
        var seed = new SmartDJSeed { TargetTrackCount = 10 };

        var mix = await engine.GenerateMixAsync(seed, library);

        Assert.Equal(10, mix.Count);
        Assert.DoesNotContain(mix, t => t.Rating == HeartRating.Dislike);
    }

    [Fact]
    public async Task EmptyLibrary_ReturnsEmptyMix()
    {
        var engine = new SmartDJEngine();
        var seed = new SmartDJSeed { TargetTrackCount = 25 };

        var mix = await engine.GenerateMixAsync(seed, Array.Empty<Track>());

        Assert.Empty(mix);
    }

    [Fact]
    public async Task AllDislikedLibrary_ReturnsEmptyMix_NotFallbackToDisliked()
    {
        var library = Enumerable.Range(0, 10)
            .Select(i => Track($"Track {i}", rating: HeartRating.Dislike))
            .ToArray();

        var engine = new SmartDJEngine();
        var seed = new SmartDJSeed { TargetTrackCount = 10 };

        var mix = await engine.GenerateMixAsync(seed, library);

        Assert.Empty(mix);
    }
}
