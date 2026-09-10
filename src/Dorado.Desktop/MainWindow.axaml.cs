using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Dorado.UI.ViewModels;
using Dorado.UI.Views;

namespace Dorado.Desktop;

public partial class MainWindow : Window
{
    private double _preCompactWidth = 1240;
    private double _preCompactHeight = 780;

    public MainWindow()
    {
        InitializeComponent();

        // Toggle playback on Space from anywhere, even when a song/album Button still has
        // focus after being clicked. Tunnelling intercepts the key before the focused
        // control, and swallowing the matching KeyUp stops the Button from activating.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnPreviewKeyUp, RoutingStrategies.Tunnel);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || DataContext is not MainShellViewModel vm)
        {
            return;
        }

        if (FocusManager?.GetFocusedElement() is TextBox)
        {
            return;
        }

        vm.PlayPauseCommand.Execute(null);
        e.Handled = true;
    }

    private void OnPreviewKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || DataContext is not MainShellViewModel)
        {
            return;
        }

        if (FocusManager?.GetFocusedElement() is TextBox)
        {
            return;
        }

        e.Handled = true;
    }

    private void OnResizeZonePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control zone || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (!CanResize || WindowState != WindowState.Normal)
            return;

        WindowEdge? edge = zone.Name switch
        {
            "ResizeNorthWest" => WindowEdge.NorthWest,
            "ResizeNorth" => WindowEdge.North,
            "ResizeNorthEast" => WindowEdge.NorthEast,
            "ResizeWest" => WindowEdge.West,
            "ResizeEast" => WindowEdge.East,
            "ResizeSouthWest" => WindowEdge.SouthWest,
            "ResizeSouth" => WindowEdge.South,
            "ResizeSouthEast" => WindowEdge.SouthEast,
            _ => null
        };

        if (edge is { } resolved)
        {
            BeginResizeDrag(resolved, e);
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainShellViewModel vm)
        {
            vm.CompactModeChanged += OnCompactModeChanged;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (DataContext is not MainShellViewModel vm)
            return;

        var focused = FocusManager?.GetFocusedElement();
        bool isTextBoxFocused = focused is TextBox;

        // Ctrl shortcuts (work even if text box is focused)
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Key.P:
                    vm.PlayPauseCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.F:
                    vm.NextCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.B:
                    vm.PreviousCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.H:
                    vm.ToggleShuffleCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.T:
                    vm.ToggleRepeatCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.M:
                    vm.ToggleCompactModeCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.E:
                    vm.ActivePivot = NavigationPivot.Collection;
                    e.Handled = true;
                    return;
                case Key.S:
                    vm.StopCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.Left:
                    vm.RewindCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.Right:
                    vm.FastForwardCommand.Execute(null);
                    e.Handled = true;
                    return;
            }
        }

        // Non-Ctrl shortcuts (only if NOT typing in a TextBox)
        if (!isTextBoxFocused)
        {
            switch (e.Key)
            {
                case Key.Space:
                    vm.PlayPauseCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.Oem2:
                    // "/" — search-focus shortcut: put the caret in the header search box
                    if (Content is MainShellView shell)
                    {
                        shell.FocusHeaderSearch();
                        e.Handled = true;
                    }
                    return;
                case Key.F1:
                    vm.ActivePivot = NavigationPivot.Settings;
                    vm.SettingsVM.SoftwarePivot = SoftwareSubPivot.About;
                    e.Handled = true;
                    return;
                case Key.F7:
                    // Mute / Unmute
                    vm.Volume = vm.Volume > 0 ? 0 : 75;
                    e.Handled = true;
                    return;
                case Key.F8:
                    // Volume down
                    vm.Volume = Math.Max(0, vm.Volume - 5);
                    e.Handled = true;
                    return;
                case Key.F9:
                    // Volume up
                    vm.Volume = Math.Min(100, vm.Volume + 5);
                    e.Handled = true;
                    return;
                case Key.Escape:
                    if (vm.IsCompactMode)
                    {
                        vm.ToggleCompactModeCommand.Execute(null);
                        e.Handled = true;
                    }
                    else if (vm.IsNowPlayingActive)
                    {
                        vm.ActivePivot = NavigationPivot.Collection;
                        e.Handled = true;
                    }
                    else if (vm.CanGoBack)
                    {
                        vm.GoBack();
                        e.Handled = true;
                    }
                    else if (vm.HasHeaderSearchQuery)
                    {
                        vm.HeaderSearchQuery = string.Empty;
                        e.Handled = true;
                    }
                    return;
            }
        }
    }

    private void OnCompactModeChanged(object? sender, bool isCompact)
    {
        if (isCompact)
        {
            _preCompactWidth = Width;
            _preCompactHeight = Height;
            MinWidth = 480;
            MinHeight = 110;
            Width = 480;
            Height = 130;
            Topmost = (DataContext is MainShellViewModel vm) ? vm.SettingsVM.CompactModeAlwaysOnTop : true;
            CanResize = false;
        }
        else
        {
            MinWidth = 734;
            MinHeight = 500;
            Width = _preCompactWidth >= 734 ? _preCompactWidth : 1012;
            Height = _preCompactHeight >= 500 ? _preCompactHeight : 693;
            Topmost = false;
            CanResize = true;
        }
    }
}
