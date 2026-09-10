using Dorado.Application.Interfaces;
using Dorado.Domain.Models;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Verifies the Zune Card page shows the live cross-device card when the cloud
/// is enabled and a handle is configured, and degrades to the local projection
/// otherwise.
/// </summary>
public sealed class ZuneCardLiveTests
{
    [Fact]
    public async Task LiveCard_PopulatesWhenEnabledAndHandleSet()
    {
        var cloud = new RecordingCloudSocial
        {
            Enabled = true,
            Card = new ZuneCardSnapshot
            {
                Handle = "jane",
                DisplayName = "Jane",
                Bio = "b",
                Followers = 5,
                Following = 2,
                Activities = 9,
                Badges = new[] { new ZuneCardBadge { Code = "connector", Name = "Connector" } },
                Recent = new[] { new ZuneCardActivity { Kind = "listen", PayloadJson = "{}" } },
            },
        };
        var vm = new ZuneCardViewModel(new StubUserStats(), cloud, () => "jane");

        await vm.LoadLiveCardAsync();

        Assert.True(vm.HasLiveCard);
        Assert.Equal("jane", vm.LiveHandle);
        Assert.Equal("5 FOLLOWERS", vm.LiveFollowersText);
        Assert.Equal("2 FOLLOWING", vm.LiveFollowingText);
        Assert.Equal("9 ACTIVITIES", vm.LiveActivitiesText);
        Assert.Single(vm.LiveBadges);
        Assert.Single(vm.LiveRecent);
    }

    [Fact]
    public async Task LiveCard_HiddenWhenCloudDisabled()
    {
        var cloud = new RecordingCloudSocial { Enabled = false };
        var vm = new ZuneCardViewModel(new StubUserStats(), cloud, () => "jane");

        await vm.LoadLiveCardAsync();

        Assert.False(vm.HasLiveCard);
        Assert.Empty(vm.LiveBadges);
    }

    [Fact]
    public async Task LiveCard_HiddenWhenNoHandle()
    {
        var cloud = new RecordingCloudSocial { Enabled = true, Card = new ZuneCardSnapshot { Handle = "jane" } };
        var vm = new ZuneCardViewModel(new StubUserStats(), cloud, () => "");

        await vm.LoadLiveCardAsync();

        Assert.False(vm.HasLiveCard);
    }

    [Fact]
    public async Task LiveCard_HiddenWhenNoCloudService()
    {
        var vm = new ZuneCardViewModel(new StubUserStats());

        await vm.LoadLiveCardAsync();

        Assert.False(vm.HasLiveCard);
    }

    [Fact]
    public async Task LiveCard_HiddenWhenCloudReturnsNull()
    {
        var cloud = new RecordingCloudSocial { Enabled = true, Card = null };
        var vm = new ZuneCardViewModel(new StubUserStats(), cloud, () => "jane");

        await vm.LoadLiveCardAsync();

        Assert.False(vm.HasLiveCard);
    }
}

internal sealed class StubUserStats : IUserStatsService
{
    public Task<ZuneProfile> GetProfileAsync() => Task.FromResult(new ZuneProfile());

    public Task<IReadOnlyList<TopArtistStat>> GetTopArtistsAsync(int count = 5)
        => Task.FromResult<IReadOnlyList<TopArtistStat>>(Array.Empty<TopArtistStat>());

    public Task<IReadOnlyList<ZuneBadge>> GetBadgesAsync()
        => Task.FromResult<IReadOnlyList<ZuneBadge>>(Array.Empty<ZuneBadge>());

    public Task RecordTrackPlayedAsync(Track track) => Task.CompletedTask;
}
