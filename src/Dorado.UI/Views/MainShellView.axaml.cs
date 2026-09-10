using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Dorado.UI.Views;

public partial class MainShellView : UserControl
{
    public MainShellView()
    {
        InitializeComponent();
    }

    public void FocusHeaderSearch()
    {
        if (HeaderSearchBox.IsVisible)
        {
            HeaderSearchBox.Focus();
            HeaderSearchBox.SelectAll();
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var window = TopLevel.GetTopLevel(this) as Window;
        if (window == null)
            return;

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            if (e.ClickCount == 2 && window.CanResize)
            {
                window.WindowState = window.WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
            else
            {
                window.BeginMoveDrag(e);
            }
        }
    }

    private void OnPivotStripPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (sender is not ScrollViewer strip || e.Delta.Y == 0)
            return;

        var nextX = Math.Max(0, strip.Offset.X - e.Delta.Y * 48);
        strip.Offset = new Avalonia.Vector(nextX, 0);
        e.Handled = true;
    }

    /// <summary>
    /// Tier A1: the Zune 4.8 cropped-header back affordance. Clicking the cropped title
    /// on a detail page (Now Playing / Mixview) pops back to the parent pivot; on a wizard
    /// overlay it dismisses the wizard.
    /// </summary>
    private void OnCroppedHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not ViewModels.MainShellViewModel vm)
            return;

        e.Handled = true; // Suppress the title-bar drag handler so the click doesn't move the window.

        if (vm.IsFirstLaunchWizardOpen)
        {
            vm.FirstLaunchWizardVM = null;
            return;
        }
        if (vm.IsFirstConnectWizardOpen)
        {
            vm.FirstConnectWizardVM = null;
            return;
        }
        if (vm.IsWhatsNewOpen)
        {
            vm.WhatsNewVM = null;
            return;
        }

        if (vm.IsNowPlayingActive || vm.IsMixviewActive)
        {
            vm.GoBack();
            return;
        }

        if (vm.CanGoBack)
        {
            vm.GoBack();
        }
    }

    private void OnMinimizeClicked(object? sender, RoutedEventArgs e)
    {
        var window = TopLevel.GetTopLevel(this) as Window;
        if (window != null)
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    private void OnMaximizeClicked(object? sender, RoutedEventArgs e)
    {
        var window = TopLevel.GetTopLevel(this) as Window;
        if (window != null)
        {
            window.WindowState = window.WindowState == WindowState.Maximized 
                ? WindowState.Normal 
                : WindowState.Maximized;
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        var window = TopLevel.GetTopLevel(this) as Window;
        window?.Close();
    }

    private void OnNowPlayingButtonPointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (DataContext is ViewModels.MainShellViewModel vm)
        {
            vm.NotifyNowPlayingButtonHover(true);
        }
    }

    private void OnNowPlayingButtonPointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (DataContext is ViewModels.MainShellViewModel vm)
        {
            vm.NotifyNowPlayingButtonHover(false);
            vm.NotifyNowPlayingButtonPressed(false);
        }
    }

    private void OnNowPlayingButtonPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (DataContext is ViewModels.MainShellViewModel vm)
        {
            vm.NotifyNowPlayingButtonPressed(true);
        }
    }
}
