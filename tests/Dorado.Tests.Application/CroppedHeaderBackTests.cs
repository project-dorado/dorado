using Avalonia.Headless.XUnit;
using Dorado.Application.Services;
using Dorado.Tests.Application.TestFakes;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Tier A1 — tap-the-cut-off-header-to-go-back. The cropped title binding
/// reflects the active pivot / wizard, and the back affordance is gated by
/// navigation history or wizard-open state.
/// </summary>
public class CroppedHeaderBackTests
{
    private static MainShellViewModel NewShell()
    {
        return new MainShellViewModel(
            new PlaybackQueueCoordinator(),
            new FakeMediaLibraryService(),
            new FakeDeviceSyncService(),
            new SmartDJEngine());
    }

    [AvaloniaFact]
    public void CroppedHeaderTitle_ReflectsLandingPivot()
    {
        var vm = NewShell();

        vm.ActivePivot = NavigationPivot.Quickplay;
        Assert.Equal("QUICKPLAY", vm.CroppedHeaderTitle);

        vm.ActivePivot = NavigationPivot.Collection;
        Assert.Equal("COLLECTION", vm.CroppedHeaderTitle);

        vm.ActivePivot = NavigationPivot.Device;
        Assert.Equal("DEVICE", vm.CroppedHeaderTitle);

        vm.ActivePivot = NavigationPivot.Settings;
        Assert.Equal("SETTINGS", vm.CroppedHeaderTitle);

        vm.ActivePivot = NavigationPivot.Disc;
        Assert.Equal("DISC", vm.CroppedHeaderTitle);

        vm.ActivePivot = NavigationPivot.Social;
        Assert.Equal("SOCIAL", vm.CroppedHeaderTitle);
    }

    [AvaloniaFact]
    public void CroppedHeaderTitle_ReflectsDetailPivot()
    {
        var vm = NewShell();

        vm.ActivePivot = NavigationPivot.NowPlaying;
        Assert.Equal("NOW PLAYING", vm.CroppedHeaderTitle);

        vm.ActivePivot = NavigationPivot.Mixview;
        Assert.Equal("MIXVIEW", vm.CroppedHeaderTitle);
    }

    [AvaloniaFact]
    public void IsCroppedHeaderDetail_TrueForDetailPages()
    {
        var vm = NewShell();
        vm.ActivePivot = NavigationPivot.Quickplay;
        Assert.False(vm.IsCroppedHeaderDetail);

        vm.ActivePivot = NavigationPivot.NowPlaying;
        Assert.True(vm.IsCroppedHeaderDetail);

        vm.ActivePivot = NavigationPivot.Mixview;
        Assert.True(vm.IsCroppedHeaderDetail);
    }

    [AvaloniaFact]
    public void IsCroppedHeaderBack_TrueWithNavigationHistory()
    {
        var vm = NewShell();

        // Fresh: no history.
        Assert.False(vm.IsCroppedHeaderBack);

        // Simulate Quickplay → Now Playing transition (pushes Quickplay onto history).
        vm.ActivePivot = NavigationPivot.Quickplay;
        vm.ActivePivot = NavigationPivot.NowPlaying;
        Assert.True(vm.IsCroppedHeaderBack);

        // Navigate back to Quickplay: history popped → no more back affordance.
        vm.GoBack();
        Assert.False(vm.IsCroppedHeaderBack);
    }

    [AvaloniaFact]
    public void GoBack_PopsNowPlayingToPreviousPivot()
    {
        var vm = NewShell();

        vm.ActivePivot = NavigationPivot.Quickplay;
        vm.ActivePivot = NavigationPivot.NowPlaying;
        Assert.Equal(NavigationPivot.NowPlaying, vm.ActivePivot);

        vm.GoBack();
        Assert.Equal(NavigationPivot.Quickplay, vm.ActivePivot);
    }
}
