using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Dorado.Domain.Models;
using Dorado.UI.ViewModels;

namespace Dorado.UI.Views;

public partial class CollectionView : UserControl
{
    private DispatcherTimer? _longPressTimer;
    private long _pressStartedAt;
    private double _pressStartX;
    private double _pressStartY;
    private Album? _pressedAlbum;
    private bool _suppressNextClick;
    private readonly TypeAheadBuffer _typeAhead = new();

    public CollectionView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// A–Z type-ahead (Phase 19c / `JUMPINLIST` parity): typed letters jump the active list to
    /// the first matching entry. Ignored while a text box (e.g. header search) has focus.
    /// </summary>
    private void OnTypeAheadText(object? sender, TextInputEventArgs e)
    {
        if (e.Source is TextBox || string.IsNullOrEmpty(e.Text) || e.Text.Length != 1)
        {
            return;
        }

        var character = e.Text[0];
        if (!char.IsLetterOrDigit(character) || DataContext is not CollectionViewModel vm)
        {
            return;
        }

        var prefix = _typeAhead.Append(character, DateTime.UtcNow);

        switch (vm.ActiveSubPivot)
        {
            case CollectionSubPivot.Artists:
            {
                var index = TypeAheadSearch.FindIndex(vm.Artists, prefix, a => a.Name);
                if (index >= 0)
                {
                    vm.SelectedArtist = vm.Artists[index];
                    ArtistsList.ContainerFromIndex(index)?.BringIntoView();
                }

                break;
            }

            case CollectionSubPivot.Albums:
                BringIntoView(vm.Albums, AlbumsList, prefix, a => a.Title);
                break;

            case CollectionSubPivot.Songs:
                BringIntoView(vm.Songs, SongsList, prefix, s => s.Title);
                break;

            case CollectionSubPivot.Genres:
            {
                var index = TypeAheadSearch.FindIndex(vm.Genres, prefix, g => g);
                if (index >= 0)
                {
                    vm.SelectedGenre = vm.Genres[index];
                    GenresList.ContainerFromIndex(index)?.BringIntoView();
                }

                break;
            }
        }

        e.Handled = true;
    }

    private static void BringIntoView<T>(System.Collections.Generic.IEnumerable<T> items, ItemsControl control, string prefix, Func<T, string> key)
    {
        var index = TypeAheadSearch.FindIndex(items, prefix, key);
        if (index >= 0)
        {
            control.ContainerFromIndex(index)?.BringIntoView();
        }
    }

    /// <summary>
    /// B3: press-and-hold an album tile to pin it to Quickplay. The tap is owned here so a
    /// triggered long-press does not also play the album.
    /// </summary>
    private void OnAlbumPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Button button || !e.GetCurrentPoint(button).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pressedAlbum = button.DataContext as Album;
        _pressStartedAt = Environment.TickCount64;
        var position = e.GetPosition(button);
        _pressStartX = position.X;
        _pressStartY = position.Y;

        _longPressTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(LongPressMath.HoldMilliseconds) };
        _longPressTimer.Stop();
        _longPressTimer.Tick -= OnLongPressTick;
        _longPressTimer.Tick += OnLongPressTick;
        _longPressTimer.Start();
    }

    private void OnAlbumPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressedAlbum is null || _longPressTimer is null || sender is not Visual visual)
        {
            return;
        }

        var position = e.GetPosition(visual);
        var moved = Math.Sqrt(Math.Pow(position.X - _pressStartX, 2) + Math.Pow(position.Y - _pressStartY, 2));
        if (moved > LongPressMath.MoveTolerancePixels)
        {
            _longPressTimer.Stop();
        }
    }

    private void OnAlbumPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _longPressTimer?.Stop();
    }

    private void OnLongPressTick(object? sender, EventArgs e)
    {
        _longPressTimer?.Stop();
        if (_pressedAlbum is null)
        {
            return;
        }

        if (DataContext is CollectionViewModel vm)
        {
            vm.PinAlbumCommand.Execute(_pressedAlbum);
        }

        _suppressNextClick = true;
    }

    private void OnAlbumClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_suppressNextClick)
        {
            _suppressNextClick = false;
            return;
        }

        var album = (sender as Button)?.DataContext as Album;
        if (album is not null && DataContext is CollectionViewModel vm)
        {
            vm.PlayAlbumCommand.Execute(album);
        }
    }
}
