using System.Threading.Tasks;
using Dorado.Application.Services;
using Dorado.Tests.Application.TestFakes;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Playlist search parity (2026-09-11 audit M-5): the collection filter and the
/// header search must cover playlists, not just songs/albums/artists.
/// </summary>
public class PlaylistSearchTests
{
    [Fact]
    public async Task PlaylistsViewModel_Filter_MatchesNameAndDescription()
    {
        var lib = new FakeMediaLibraryService();
        lib.Playlists.Add(new Dorado.Domain.Models.Playlist { Name = "Road Trip", Description = "Loud and long" });
        lib.Playlists.Add(new Dorado.Domain.Models.Playlist { Name = "Focus", Description = "No vocals" });

        var vm = new PlaylistsViewModel(lib, new PlaybackQueueCoordinator());
        await vm.LoadPlaylistsAsync();
        Assert.Equal(2, vm.Playlists.Count);

        vm.Filter("road");
        Assert.Single(vm.Playlists);
        Assert.Equal("Road Trip", vm.Playlists[0].Name);

        vm.Filter("vocals");
        Assert.Single(vm.Playlists);
        Assert.Equal("Focus", vm.Playlists[0].Name);

        vm.Filter(string.Empty);
        Assert.Equal(2, vm.Playlists.Count);

        vm.Filter("zzz-no-match");
        Assert.Empty(vm.Playlists);
    }

    [Fact]
    public async Task CollectionFilter_AppliesToPlaylists()
    {
        var lib = new FakeMediaLibraryService();
        lib.Playlists.Add(new Dorado.Domain.Models.Playlist { Name = "Road Trip" });
        lib.Playlists.Add(new Dorado.Domain.Models.Playlist { Name = "Focus" });

        var vm = new CollectionViewModel(new PlaybackQueueCoordinator(), lib);
        await vm.RefreshDataAsync();
        Assert.Equal(2, vm.PlaylistsVM.Playlists.Count);

        vm.FilterQuery("road");
        Assert.Single(vm.PlaylistsVM.Playlists);
        Assert.Equal("Road Trip", vm.PlaylistsVM.Playlists[0].Name);

        vm.FilterQuery(string.Empty);
        Assert.Equal(2, vm.PlaylistsVM.Playlists.Count);
    }
}
