using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Dorado.UI.ViewModels;

namespace Dorado.UI.Views;

public partial class MainShellView : UserControl
{
    private ScrollViewer? _pivotStrip;
    private DispatcherTimer? _pivotInertia;
    private bool _pivotDragging;
    private double _pivotLastX;
    private double _pivotVelocity;
    private long _pivotLastTicks;
    private bool _isSeeking;

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

        StopPivotInertia();
        var nextX = PivotPanMath.ClampOffset(strip.Offset.X - e.Delta.Y * 48, strip.Extent.Width, strip.Viewport.Width);
        strip.Offset = new Avalonia.Vector(nextX, 0);
        e.Handled = true;
    }

    private void OnPivotStripPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not ScrollViewer strip || !e.GetCurrentPoint(strip).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pivotStrip = strip;
        _pivotDragging = true;
        _pivotLastX = e.GetPosition(strip).X;
        _pivotVelocity = 0;
        _pivotLastTicks = Environment.TickCount64;
        StopPivotInertia();
        e.Pointer.Capture(strip);
    }

    private void OnPivotStripPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_pivotDragging || _pivotStrip is null)
        {
            return;
        }

        var x = e.GetPosition(_pivotStrip).X;
        var delta = x - _pivotLastX;
        _pivotLastX = x;

        var now = Environment.TickCount64;
        var elapsed = Math.Max(1, now - _pivotLastTicks);
        _pivotLastTicks = now;
        _pivotVelocity = -delta / elapsed; // px per ms

        _pivotStrip.Offset = new Vector(
            PivotPanMath.ClampOffset(_pivotStrip.Offset.X - delta, _pivotStrip.Extent.Width, _pivotStrip.Viewport.Width), 0);
        e.Handled = true;
    }

    /// <summary>Releases the pivot strip: momentum continues under friction until it stops.</summary>
    private void OnPivotStripPointerReleased(object? sender, PointerReleasedEventArgs e) => EndPivotDrag();

    private void OnPivotStripPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => EndPivotDrag();

    private void EndPivotDrag()
    {
        if (!_pivotDragging)
        {
            return;
        }

        _pivotDragging = false;
        if (_pivotStrip is null || !PivotPanMath.ShouldContinue(_pivotVelocity))
        {
            return;
        }

        _pivotInertia ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _pivotInertia.Tick -= OnPivotInertiaTick;
        _pivotInertia.Tick += OnPivotInertiaTick;
        _pivotInertia.Start();
    }

    private void OnPivotInertiaTick(object? sender, EventArgs e)
    {
        if (_pivotStrip is null)
        {
            StopPivotInertia();
            return;
        }

        var target = _pivotStrip.Offset.X + _pivotVelocity * 16;
        var clamped = PivotPanMath.ClampOffset(target, _pivotStrip.Extent.Width, _pivotStrip.Viewport.Width);
        _pivotStrip.Offset = new Vector(clamped, 0);
        _pivotVelocity = PivotPanMath.NextVelocity(_pivotVelocity);

        if (!PivotPanMath.ShouldContinue(_pivotVelocity) || clamped <= 0 || clamped >= Math.Max(0, _pivotStrip.Extent.Width - _pivotStrip.Viewport.Width))
        {
            StopPivotInertia();
        }
    }

    private void StopPivotInertia()
    {
        _pivotInertia?.Stop();
        _pivotVelocity = 0;
    }

    private void OnPivotScrollLeft(object? sender, RoutedEventArgs e) => ScrollPivot(-260);

    private void OnPivotScrollRight(object? sender, RoutedEventArgs e) => ScrollPivot(260);

    private void ScrollPivot(double delta)
    {
        var strip = this.FindControl<ScrollViewer>("PivotStrip");
        if (strip is null)
        {
            return;
        }

        StopPivotInertia();
        strip.Offset = new Vector(
            PivotPanMath.ClampOffset(strip.Offset.X + delta, strip.Extent.Width, strip.Viewport.Width), 0);
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
