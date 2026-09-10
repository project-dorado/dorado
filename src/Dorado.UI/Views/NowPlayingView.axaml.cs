using Avalonia.Controls;
using Avalonia.Input;
using Dorado.UI.ViewModels;

namespace Dorado.UI.Views;

public partial class NowPlayingView : UserControl
{
    public NowPlayingView()
    {
        InitializeComponent();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is NowPlayingViewModel vm)
        {
            vm.TriggerHudActivity();
        }

        // Tier A3: any pointer movement resets the idle screensaver clock.
        if (this.VisualRoot is Views.MainShellView shell && shell.DataContext is ViewModels.MainShellViewModel shellVm)
        {
            shellVm.ResetNowPlayingIdle();
        }
    }
}
