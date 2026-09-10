using Avalonia.Headless.XUnit;
using Dorado.Application.Services;
using Dorado.Tests.Application.TestFakes;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// The Now Playing mark is drawn procedurally (Vector 4) from the ViewModel's
/// frame/playing state; these tests assert that state and the equalizer
/// geometry generator, not any Microsoft PNG asset.
/// </summary>
public class NowPlayingIconTests
{
    [Fact]
    public void Default_IsIdleEqualizerState()
    {
        var vm = NewShell();

        Assert.False(vm.NowPlayingIconPlaying);
        Assert.InRange(vm.NowPlayingIconFrame, 1, 10);
    }

    [Fact]
    public void Hover_DoesNotChangeEqualizerState()
    {
        var vm = NewShell();
        var before = (vm.NowPlayingIconFrame, vm.NowPlayingIconPlaying);

        vm.NotifyNowPlayingButtonHover(true);
        vm.NotifyNowPlayingButtonHover(false);

        Assert.Equal(before, (vm.NowPlayingIconFrame, vm.NowPlayingIconPlaying));
    }

    [AvaloniaFact]
    public void EqualizerGeometry_BuildsForIdleAndPlaying()
    {
        Assert.NotNull(Dorado.UI.Design.ZuneGlyphs.Equalizer(1, playing: false));
        Assert.NotNull(Dorado.UI.Design.ZuneGlyphs.Equalizer(5, playing: true));
    }

    private static MainShellViewModel NewShell() => new(
        new PlaybackQueueCoordinator(),
        new FakeMediaLibraryService(),
        new FakeDeviceSyncService(),
        new SmartDJEngine());
}
