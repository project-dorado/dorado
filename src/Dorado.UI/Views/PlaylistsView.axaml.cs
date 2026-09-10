using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Dorado.Domain.Models;

namespace Dorado.UI.Views;

public partial class PlaylistsView : UserControl
{
    private readonly TypeAheadBuffer _typeAhead = new();

    public PlaylistsView()
    {
        InitializeComponent();
    }

    /// <summary>A–Z jump: match playlists (selection + scroll), then the selected playlist's tracks.</summary>
    private void OnTypeAheadText(object? sender, TextInputEventArgs e)
    {
        if (e.Source is TextBox || string.IsNullOrEmpty(e.Text) || e.Text.Length != 1)
        {
            return;
        }

        var character = e.Text[0];
        if (!char.IsLetterOrDigit(character) || DataContext is not ViewModels.PlaylistsViewModel vm)
        {
            return;
        }

        var prefix = _typeAhead.Append(character, DateTime.UtcNow);

        var playlistIndex = TypeAheadSearch.FindIndex(vm.Playlists, prefix, playlist => playlist.Name);
        if (playlistIndex >= 0)
        {
            vm.SelectedPlaylist = vm.Playlists[playlistIndex];
            PlaylistsList.ScrollIntoView(vm.Playlists[playlistIndex]);
            e.Handled = true;
            return;
        }

        var trackIndex = TypeAheadSearch.FindIndex(vm.SelectedPlaylistTracks, prefix, track => track.Title);
        if (trackIndex >= 0)
        {
            PlaylistTracksList.ContainerFromIndex(trackIndex)?.BringIntoView();
        }

        e.Handled = true;
    }

    /// <summary>
    /// Tier B2: enter a playlist's drop zone — highlight it and arm the hover-swap timer.
    /// </summary>
    private void OnPlaylistDragEnter(object? sender, DragEventArgs e)
    {
        if (sender is not Control element || element.Tag is not Playlist playlist || DataContext is not ViewModels.PlaylistsViewModel vm)
            return;

        e.DragEffects = DragDropEffects.Copy;
        vm.EnterPlaylistDropZone(playlist);
    }

    /// <summary>Tier B2: refresh hover on intra-list movement (re-arms the swap timer).</summary>
    private void OnPlaylistDragOver(object? sender, DragEventArgs e)
    {
        if (e.DragEffects == DragDropEffects.None)
        {
            e.DragEffects = DragDropEffects.Copy;
        }
    }

    /// <summary>Tier B2: leave a playlist's drop zone — clear highlight + collapse swap popup.</summary>
    private void OnPlaylistDragLeave(object? sender, DragEventArgs e)
    {
        if (sender is not Control element || element.Tag is not Playlist playlist || DataContext is not ViewModels.PlaylistsViewModel vm)
            return;

        vm.LeavePlaylistDropZone(playlist);
    }

    /// <summary>
    /// Tier B2: drop the dragged payload (a list of Track ids) onto the hovered playlist.
    /// Pulls the track list out of the drag data and pipes it into AddTracksToPlaylistCommand.
    /// </summary>
    private void OnPlaylistDrop(object? sender, DragEventArgs e)
    {
        if (sender is not Control element || element.Tag is not Playlist playlist || DataContext is not ViewModels.PlaylistsViewModel vm)
            return;

        e.DragEffects = DragDropEffects.Copy;

        // The payload is a DataObject with the dragged Track list under the "Tracks" key.
        var tracks = e.Data is DataObject data
            ? data.Get(TrackListPayload.TracksKey) as System.Collections.Generic.IReadOnlyList<Track>
            : null;

        if (tracks == null || tracks.Count == 0)
        {
            tracks = vm.SelectedPlaylistTracks.ToList();
        }

        if (tracks.Count == 0)
        {
            vm.ClearPlaylistHover();
            return;
        }

        var target = vm.HoveredPlaylist ?? playlist;
        vm.ClearPlaylistHover();
        vm.AddTracksToPlaylistCommand.Execute((target, tracks));
    }

    /// <summary>
    /// Tier B2: track-row drag source. PointerPressed initiates a DoDragDrop with a
    /// TrackListPayload containing the dragged tracks.
    /// </summary>
    private void OnTrackPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control element || element.Tag is not Track startTrack || DataContext is not ViewModels.PlaylistsViewModel vm)
            return;

        // Use right-click or modifier-click to ignore (preserve normal selection behaviour).
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        // Build the payload: the row that was clicked plus any selected siblings (multi-select is a future).
        var tracks = new System.Collections.Generic.List<Track> { startTrack };

        var data = new DataObject();
        data.Set("Tracks", tracks);

        // Start the drag — the result tells us what drop effect actually happened.
        _ = DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
    }
}

/// <summary>
/// Tier B2: drag payload contract — a DataObject with the dragged Track list under the "Tracks" key.
/// (Marker type retained for documentation; actual payload is the keyed DataObject.)
/// </summary>
public static class TrackListPayload
{
    public const string TracksKey = "Tracks";
}
