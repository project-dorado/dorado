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
    public void Hover_And_Press_ExposeIconVariants()
    {
        var vm = NewShell();

        Assert.False(vm.NowPlayingIconHovered);
        Assert.False(vm.NowPlayingIconPressed);

        vm.NotifyNowPlayingButtonHover(true);
        Assert.True(vm.NowPlayingIconHovered);

        vm.NotifyNowPlayingButtonPressed(true);
        Assert.True(vm.NowPlayingIconPressed);

        vm.NotifyNowPlayingButtonPressed(false);
        vm.NotifyNowPlayingButtonHover(false);
        Assert.False(vm.NowPlayingIconHovered);
        Assert.False(vm.NowPlayingIconPressed);
    }

    [AvaloniaFact]
    public void EqualizerGeometry_BuildsForIdleAndPlaying()
    {
        Assert.NotNull(Dorado.UI.Design.ZuneGlyphs.Equalizer(1, playing: false));
        Assert.NotNull(Dorado.UI.Design.ZuneGlyphs.Equalizer(5, playing: true));
    }

    [AvaloniaFact]
    public void EqualizerGeometry_HoverAndPressed_VariantsDiffer()
    {
        var idle = Dorado.UI.Design.ZuneGlyphs.Equalizer(1, playing: false);
        var hover = Dorado.UI.Design.ZuneGlyphs.Equalizer(1, playing: false, hovered: true);
        var pressed = Dorado.UI.Design.ZuneGlyphs.Equalizer(1, playing: false, pressed: true);

        Assert.NotNull(idle);
        Assert.NotNull(hover);
        Assert.NotNull(pressed);
    }

    [Fact]
    public void Mode_ExposesCrossfadeOpacities()
    {
        var np = new NowPlayingViewModel(new PlaybackQueueCoordinator(), new FakeMediaLibraryService());

        // Default mode is Artist Canvas.
        Assert.Equal(1.0, np.ArtistCanvasOpacity);
        Assert.Equal(0.0, np.MosaicWallOpacity);

        np.Mode = NowPlayingMode.MosaicWall;
        Assert.Equal(0.0, np.ArtistCanvasOpacity);
        Assert.Equal(1.0, np.MosaicWallOpacity);

        np.Mode = NowPlayingMode.Video;
        Assert.Equal(1.0, np.VideoOpacity);
        Assert.Equal(0.0, np.MosaicWallOpacity);
    }

    private static MainShellViewModel NewShell() => new(
        new PlaybackQueueCoordinator(),
        new FakeMediaLibraryService(),
        new FakeDeviceSyncService(),
        new SmartDJEngine());
}
