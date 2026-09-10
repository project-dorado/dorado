using Dorado.Domain.Enums;
using Dorado.Domain.Models;
using Xunit;

namespace Dorado.Tests.Domain;

public class DomainModelTests
{
    [Fact]
    public void HeartRating_HasExpectedValues()
    {
        Assert.Equal(0, (int)HeartRating.None);
        Assert.Equal(1, (int)HeartRating.Favorite);
        Assert.Equal(2, (int)HeartRating.Dislike);
    }

    [Fact]
    public void Track_InitializesWithDefaultValues()
    {
        var track = new Track
        {
            Title = "Subdivisions",
            ArtistName = "Rush",
            AlbumTitle = "Signals",
            Duration = TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(34)
        };

        Assert.NotEqual(Guid.Empty, track.Id);
        Assert.Equal(HeartRating.None, track.Rating);
        Assert.Equal(0, track.PlayCount);
        Assert.Equal(1, track.DiscNumber);
        Assert.Equal("Subdivisions", track.Title);
    }

    [Fact]
    public void SmartDJSeed_RespectsDefaults()
    {
        var seed = new SmartDJSeed();
        Assert.Equal(25, seed.TargetTrackCount);
        Assert.True(seed.ExcludeDisliked);
    }

    [Fact]
    public void ZuneDevice_DefaultsToDisconnected()
    {
        var device = new ZuneDevice
        {
            SerialNumber = "1234567890",
            ModelName = "Zune HD 64"
        };

        Assert.False(device.IsConnected);
        Assert.Equal(DeviceSyncState.Disconnected, device.SyncState);
    }
}
