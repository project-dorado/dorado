using Avalonia.Headless.XUnit;
using Dorado.Application.Services;
using Dorado.Tests.Application.TestFakes;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

public class NowPlayingIconTests
{
    [AvaloniaFact]
    public void Default_IconIsEnterStatic()
    {
        var vm = new MainShellViewModel(
            new PlaybackQueueCoordinator(),
            new FakeMediaLibraryService(),
            new FakeDeviceSyncService(),
            new SmartDJEngine());

        Assert.EndsWith("ICON.NOWPLAYING.ENTER.PNG", vm.NowPlayingIconSource);
    }

    [AvaloniaFact]
    public void Hover_SwitchesToHoverVariant()
    {
        var vm = new MainShellViewModel(
            new PlaybackQueueCoordinator(),
            new FakeMediaLibraryService(),
            new FakeDeviceSyncService(),
            new SmartDJEngine());

        vm.NotifyNowPlayingButtonHover(true);
        Assert.EndsWith("ICON.NOWPLAYING.ENTER.HOVER.PNG", vm.NowPlayingIconSource);

        vm.NotifyNowPlayingButtonHover(false);
        Assert.EndsWith("ICON.NOWPLAYING.ENTER.PNG", vm.NowPlayingIconSource);
    }

    [AvaloniaFact]
    public void Pressed_OverridesHoverVariant()
    {
        var vm = new MainShellViewModel(
            new PlaybackQueueCoordinator(),
            new FakeMediaLibraryService(),
            new FakeDeviceSyncService(),
            new SmartDJEngine());

        vm.NotifyNowPlayingButtonHover(true);
        vm.NotifyNowPlayingButtonPressed(true);
        Assert.EndsWith("ICON.NOWPLAYING.ENTER.PRESSED.PNG", vm.NowPlayingIconSource);

        vm.NotifyNowPlayingButtonPressed(false);
        Assert.EndsWith("ICON.NOWPLAYING.ENTER.HOVER.PNG", vm.NowPlayingIconSource);
    }
}
