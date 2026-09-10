using Avalonia.Headless.XUnit;
using Dorado.Application.Services;
using Dorado.Tests.Application.TestFakes;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Tier A4 — drawer slide-in / fade animations. The Bio and Showlist drawers expose
/// offset + opacity values that the animation timer drives toward their targets;
/// the toggle commands set those targets.
/// </summary>
public class DrawerAnimTests
{
    private static NowPlayingViewModel NewNpvm() => new NowPlayingViewModel(
        new PlaybackQueueCoordinator(),
        new FakeMediaLibraryService());

    [AvaloniaFact]
    public void BioDrawer_Defaults_ToClosedState()
    {
        var vm = NewNpvm();
        Assert.False(vm.IsBioDrawerOpen);
        Assert.Equal(-400, vm.BioDrawerOffsetX);
        Assert.Equal(0.0, vm.BioDrawerOpacity);
    }

    [AvaloniaFact]
    public void ShowlistDrawer_Defaults_ToClosedState()
    {
        var vm = NewNpvm();
        Assert.False(vm.IsShowlistOpen);
        Assert.Equal(400, vm.ShowlistDrawerOffsetX);
        Assert.Equal(0.0, vm.ShowlistDrawerOpacity);
    }

    [AvaloniaFact]
    public void ToggleBioDrawer_Opens_Drawer()
    {
        var vm = NewNpvm();
        vm.ToggleBioDrawerCommand.Execute(null);
        Assert.True(vm.IsBioDrawerOpen);
    }

    [AvaloniaFact]
    public void ToggleBioDrawer_Then_ToggleShowlist_ClosesBio()
    {
        var vm = NewNpvm();
        vm.ToggleBioDrawerCommand.Execute(null);
        Assert.True(vm.IsBioDrawerOpen);
        vm.ToggleShowlistCommand.Execute(null);
        Assert.False(vm.IsBioDrawerOpen);
        Assert.True(vm.IsShowlistOpen);
    }

    [AvaloniaFact]
    public void ToggleShowlist_Opens_Drawer()
    {
        var vm = NewNpvm();
        vm.ToggleShowlistCommand.Execute(null);
        Assert.True(vm.IsShowlistOpen);
    }
}
