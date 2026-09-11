using System.Threading.Tasks;
using Dorado.Application.Services;
using Dorado.Tests.Application.TestFakes;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Clean-room Iris mosaic (Mixview): the constellation is backed by tiled
/// collection artwork, recreated procedurally from the local library.
/// </summary>
public class MixviewMosaicTests
{
    private static MixviewViewModel NewVm(out FakeMediaLibraryService lib)
    {
        lib = new FakeMediaLibraryService();
        for (int i = 0; i < 30; i++)
        {
            lib.Albums.Add(new Dorado.Domain.Models.Album { Title = $"Album {i}", ArtistName = "A" });
        }
        var coordinator = new MixviewCoordinator(lib, null);
        return new MixviewViewModel(coordinator, new PlaybackQueueCoordinator(), new SmartDJEngine(), lib);
    }

    [Fact]
    public async Task Mosaic_FillsFromLibrary_Capped()
    {
        var vm = NewVm(out _);

        for (int i = 0; i < 100 && vm.MosaicTiles.Count == 0; i++)
        {
            await Task.Delay(5);
        }

        Assert.True(vm.HasMosaicTiles);
        Assert.Equal(24, vm.MosaicTiles.Count);
    }
}
