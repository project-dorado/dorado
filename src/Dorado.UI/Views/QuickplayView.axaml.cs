using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Dorado.UI.ViewModels;

namespace Dorado.UI.Views;

public partial class QuickplayView : UserControl
{
    private ScrollViewer? _ribbon;
    private DispatcherTimer? _inertiaTimer;
    private DispatcherTimer? _animTimer;
    private bool _dragging;
    private double _lastX;
    private double _velocity;
    private long _lastTicks;
    private bool _suppressDeckAnimation;
    private QuickplayViewModel? _currentVm;

    public QuickplayView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_currentVm != null)
        {
            _currentVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _currentVm = DataContext as QuickplayViewModel;
        if (_currentVm != null)
        {
            _currentVm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QuickplayViewModel.ActiveDeck) && !_suppressDeckAnimation && _currentVm != null)
        {
            AnimateToDeck(_currentVm.ActiveDeck);
        }
    }

    private void AnimateToDeck(QuickplayDeck deck)
    {
        _ribbon ??= this.FindControl<ScrollViewer>("QuickplayRibbon");
        if (_ribbon is null) return;

        StopInertia();
        StopAnimation();

        double targetX = 0;
        if (deck == QuickplayDeck.History)
        {
            var history = this.FindControl<StackPanel>("HistorySection");
            targetX = history?.Bounds.Left > 0 ? history.Bounds.Left : 600;
        }
        else if (deck == QuickplayDeck.New)
        {
            var newSec = this.FindControl<StackPanel>("NewSection");
            targetX = newSec?.Bounds.Left > 0 ? newSec.Bounds.Left : 1200;
        }

        var maxScroll = Math.Max(0, _ribbon.Extent.Width - _ribbon.Viewport.Width);
        targetX = Math.Clamp(targetX, 0, maxScroll);

        var startX = _ribbon.Offset.X;
        var distance = targetX - startX;
        if (Math.Abs(distance) < 1.0)
        {
            return;
        }

        var startTime = Environment.TickCount64;
        const double durationMs = 350.0;

        _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animTimer.Tick += (s, e) =>
        {
            var elapsed = Environment.TickCount64 - startTime;
            var t = Math.Clamp(elapsed / durationMs, 0.0, 1.0);
            // Cubic Ease-Out: f(t) = 1 - (1 - t)^3
            var ease = 1.0 - Math.Pow(1.0 - t, 3);
            var curX = startX + distance * ease;
            _ribbon.Offset = new Vector(curX, 0);

            if (t >= 1.0)
            {
                StopAnimation();
                _ribbon.Offset = new Vector(targetX, 0);
            }
        };
        _animTimer.Start();
    }

    private void OnRibbonPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        _ribbon ??= sender as ScrollViewer;
        if (_ribbon is null || e.Delta.Y == 0) return;

        StopAnimation();
        StopInertia();

        var nextX = PivotPanMath.ClampOffset(_ribbon.Offset.X - e.Delta.Y * 48, _ribbon.Extent.Width, _ribbon.Viewport.Width);
        _ribbon.Offset = new Vector(nextX, 0);
        UpdateActiveDeckFromOffset();
        e.Handled = true;
    }

    private void OnRibbonPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _ribbon ??= sender as ScrollViewer;
        if (_ribbon is null || !e.GetCurrentPoint(_ribbon).Properties.IsLeftButtonPressed) return;

        _dragging = true;
        _lastX = e.GetPosition(_ribbon).X;
        _velocity = 0;
        _lastTicks = Environment.TickCount64;

        StopAnimation();
        StopInertia();
        e.Pointer.Capture(_ribbon);
    }

    private void OnRibbonPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging || _ribbon is null) return;

        var x = e.GetPosition(_ribbon).X;
        var delta = x - _lastX;
        _lastX = x;

        var now = Environment.TickCount64;
        var elapsed = Math.Max(1, now - _lastTicks);
        _lastTicks = now;
        _velocity = -delta / elapsed;

        var nextX = PivotPanMath.ClampOffset(_ribbon.Offset.X - delta, _ribbon.Extent.Width, _ribbon.Viewport.Width);
        _ribbon.Offset = new Vector(nextX, 0);
        UpdateActiveDeckFromOffset();
        e.Handled = true;
    }

    private void OnRibbonPointerReleased(object? sender, PointerReleasedEventArgs e) => EndDrag();

    private void OnRibbonPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => EndDrag();

    private void EndDrag()
    {
        if (!_dragging) return;
        _dragging = false;

        if (_ribbon is null || !PivotPanMath.ShouldContinue(_velocity)) return;

        _inertiaTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _inertiaTimer.Tick -= OnInertiaTick;
        _inertiaTimer.Tick += OnInertiaTick;
        _inertiaTimer.Start();
    }

    private void OnInertiaTick(object? sender, EventArgs e)
    {
        if (_ribbon is null)
        {
            StopInertia();
            return;
        }

        var target = _ribbon.Offset.X + _velocity * 16;
        var clamped = PivotPanMath.ClampOffset(target, _ribbon.Extent.Width, _ribbon.Viewport.Width);
        _ribbon.Offset = new Vector(clamped, 0);
        _velocity = PivotPanMath.NextVelocity(_velocity);
        UpdateActiveDeckFromOffset();

        var maxScroll = Math.Max(0, _ribbon.Extent.Width - _ribbon.Viewport.Width);
        if (!PivotPanMath.ShouldContinue(_velocity) || clamped <= 0 || clamped >= maxScroll)
        {
            StopInertia();
        }
    }

    private void UpdateActiveDeckFromOffset()
    {
        if (DataContext is not QuickplayViewModel vm || _ribbon is null) return;

        var history = this.FindControl<StackPanel>("HistorySection");
        var newSec = this.FindControl<StackPanel>("NewSection");

        var historyLeft = history?.Bounds.Left > 0 ? history.Bounds.Left : 600;
        var newLeft = newSec?.Bounds.Left > 0 ? newSec.Bounds.Left : 1200;
        var offset = _ribbon.Offset.X;

        QuickplayDeck detected;
        if (offset < historyLeft * 0.6)
        {
            detected = QuickplayDeck.Pins;
        }
        else if (offset < (historyLeft + newLeft) * 0.5)
        {
            detected = QuickplayDeck.History;
        }
        else
        {
            detected = QuickplayDeck.New;
        }

        if (vm.ActiveDeck != detected)
        {
            _suppressDeckAnimation = true;
            vm.ActiveDeck = detected;
            _suppressDeckAnimation = false;
        }
    }

    private void StopAnimation()
    {
        _animTimer?.Stop();
        _animTimer = null;
    }

    private void StopInertia()
    {
        _inertiaTimer?.Stop();
        _velocity = 0;
    }
}
