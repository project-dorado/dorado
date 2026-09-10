using Dorado.Application.Events;
using Dorado.Application.Interfaces;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Tier B2 — drag-and-drop to playlist with hover-swap-icon (Zune 4.8 fan-loved feature).
/// Tests focus on the VM-level state machine: hover targets, hover-swap popup, swap-hover
/// command, and the add-tracks side effect (via a stub IMediaLibraryService).
/// </summary>
public class PlaylistDragDropTests
{
    private static Playlist Playlist(string name)
        => new() { Id = Guid.NewGuid(), Name = name, UpdatedAtUtc = DateTime.UtcNow };

    private static Track Track(string title)
        => new() { Title = title, AlbumTitle = "Album", FilePath = $"/m/{Guid.NewGuid()}.mp3" };

    private static async Task<PlaylistsViewModel> NewViewModelLoadedAsync(FakeLibraryService lib)
    {
        var vm = new PlaylistsViewModel(lib, new PlaybackCoordinatorStub());
        await vm.LoadPlaylistsAsync();
        return vm;
    }

    [Fact]
    public async Task EnterPlaylistDropZone_SetsHoveredPlaylist()
    {
        var lib = new FakeLibraryService();
        var fav = Playlist("Favorites");
        lib.AddPlaylist(fav);

        var vm = await NewViewModelLoadedAsync(lib);
        vm.EnterPlaylistDropZone(fav);

        Assert.Equal(fav, vm.HoveredPlaylist);
        Assert.True(vm.IsPlaylistHovered);
    }

    [Fact]
    public async Task LeavePlaylistDropZone_ClearsHoveredPlaylist()
    {
        var lib = new FakeLibraryService();
        var fav = Playlist("Favorites");
        lib.AddPlaylist(fav);

        var vm = await NewViewModelLoadedAsync(lib);
        vm.EnterPlaylistDropZone(fav);
        vm.LeavePlaylistDropZone(fav);

        Assert.Null(vm.HoveredPlaylist);
        Assert.False(vm.IsPlaylistHovered);
    }

    [Fact]
    public async Task RefreshPlaylistDropZone_SwitchesHoveredPlaylist()
    {
        var lib = new FakeLibraryService();
        var a = Playlist("A");
        var b = Playlist("B");
        lib.AddPlaylist(a);
        lib.AddPlaylist(b);

        var vm = await NewViewModelLoadedAsync(lib);
        vm.EnterPlaylistDropZone(a);
        vm.RefreshPlaylistDropZone(b);

        Assert.Equal(b, vm.HoveredPlaylist);
    }

    [Fact]
    public async Task ClearPlaylistHover_AfterDrop()
    {
        var lib = new FakeLibraryService();
        var fav = Playlist("Favorites");
        lib.AddPlaylist(fav);

        var vm = await NewViewModelLoadedAsync(lib);
        vm.EnterPlaylistDropZone(fav);
        vm.ClearPlaylistHover();

        Assert.Null(vm.HoveredPlaylist);
        Assert.False(vm.IsHoverSwapExpanded);
    }

    [Fact]
    public async Task HoverSwapAlternatives_ExcludesHoveredPlaylist()
    {
        var lib = new FakeLibraryService();
        var a = Playlist("A");
        var b = Playlist("B");
        var c = Playlist("C");
        lib.AddPlaylist(a);
        lib.AddPlaylist(b);
        lib.AddPlaylist(c);

        var vm = await NewViewModelLoadedAsync(lib);
        vm.EnterPlaylistDropZone(b);

        Assert.Equal(2, vm.HoverSwapAlternatives.Count);
        Assert.Contains(a, vm.HoverSwapAlternatives);
        Assert.Contains(c, vm.HoverSwapAlternatives);
        Assert.DoesNotContain(b, vm.HoverSwapAlternatives);
    }

    [Fact]
    public async Task SwapHoverTargetCommand_SwitchesHover()
    {
        var lib = new FakeLibraryService();
        var a = Playlist("A");
        var b = Playlist("B");
        lib.AddPlaylist(a);
        lib.AddPlaylist(b);

        var vm = await NewViewModelLoadedAsync(lib);
        vm.EnterPlaylistDropZone(a);
        vm.SwapHoverTargetCommand.Execute(b);

        Assert.Equal(b, vm.HoveredPlaylist);
    }

    [Fact]
    public async Task AddTracksToPlaylistCommand_AddsAndReloadsTracklist()
    {
        var lib = new FakeLibraryService();
        var target = Playlist("Mix");
        lib.AddPlaylist(target);

        var vm = await NewViewModelLoadedAsync(lib);
        vm.SelectedPlaylist = target;

        var tracks = new[] { Track("A"), Track("B"), Track("C") };
        var cmd = (CommunityToolkit.Mvvm.Input.IAsyncRelayCommand<(Playlist, System.Collections.Generic.IReadOnlyList<Track>)>)
            vm.AddTracksToPlaylistCommand;
        await cmd.ExecuteAsync((target, tracks));

        Assert.Equal(3, lib.AddedCount);
    }

    [Fact]
    public async Task AddTracksToPlaylistCommand_StatusMessage_Announces()
    {
        var lib = new FakeLibraryService();
        var target = Playlist("Mix");
        lib.AddPlaylist(target);

        var vm = await NewViewModelLoadedAsync(lib);

        var cmd = (CommunityToolkit.Mvvm.Input.IAsyncRelayCommand<(Playlist, System.Collections.Generic.IReadOnlyList<Track>)>)
            vm.AddTracksToPlaylistCommand;
        await cmd.ExecuteAsync((target, new[] { Track("X"), Track("Y") }));

        Assert.Equal("Added 2 tracks to Mix.", vm.StatusMessage);
    }

    [Fact]
    public async Task HoveredPlaylistName_MirrorsHovered()
    {
        var lib = new FakeLibraryService();
        var a = Playlist("First");
        var b = Playlist("Second");
        lib.AddPlaylist(a);
        lib.AddPlaylist(b);

        var vm = await NewViewModelLoadedAsync(lib);

        vm.EnterPlaylistDropZone(a);
        Assert.Equal("First", vm.HoverSwapPlaylistName);

        vm.EnterPlaylistDropZone(b);
        Assert.Equal("Second", vm.HoverSwapPlaylistName);

        vm.LeavePlaylistDropZone(b);
        Assert.Equal(string.Empty, vm.HoverSwapPlaylistName);
    }
}

/// <summary>In-memory library service for drag-drop tests — records playlist + track writes.</summary>
internal class FakeLibraryService : IMediaLibraryService
{
    private readonly System.Collections.Generic.List<Playlist> _playlists = new();

    public int AddedCount;

    public void AddPlaylist(Playlist playlist) => _playlists.Add(playlist);

    public Task AddTrackToPlaylistAsync(Guid playlistId, Guid trackId)
    {
        AddedCount++;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Playlist>> GetAllPlaylistsAsync() => Task.FromResult<IReadOnlyList<Playlist>>(_playlists);

#pragma warning disable CS0067
    public event EventHandler? LibraryUpdated;
#pragma warning restore CS0067

    public Task<IReadOnlyList<Track>> GetAllTracksAsync() => Task.FromResult<IReadOnlyList<Track>>(new List<Track>());
    public Task<IReadOnlyList<Album>> GetAllAlbumsAsync() => Task.FromResult<IReadOnlyList<Album>>(new List<Album>());
    public Task<IReadOnlyList<Artist>> GetAllArtistsAsync() => Task.FromResult<IReadOnlyList<Artist>>(new List<Artist>());
    public Task<IReadOnlyList<PlayHistoryEntry>> GetRecentHistoryAsync(int count = 20) => Task.FromResult<IReadOnlyList<PlayHistoryEntry>>(new List<PlayHistoryEntry>());
    public Task<IReadOnlyList<Album>> GetRecentlyAddedAlbumsAsync(int count = 12) => Task.FromResult<IReadOnlyList<Album>>(new List<Album>());
    public Task<IReadOnlyList<Track>> SearchAsync(string query) => Task.FromResult<IReadOnlyList<Track>>(new List<Track>());
    public Task SetTrackRatingAsync(Guid trackId, HeartRating rating) => Task.CompletedTask;
    public Task ScanDirectoryAsync(string directoryPath, IProgress<double>? progress = null) => Task.CompletedTask;
    public Task ClearDemoDataAsync() => Task.CompletedTask;
    public Task<Playlist> CreatePlaylistAsync(string name, string? description = null) => Task.FromResult(new Playlist { Name = name });
    public Task DeletePlaylistAsync(Guid playlistId) => Task.CompletedTask;
    public Task RemoveTrackFromPlaylistAsync(Guid playlistId, Guid trackId) => Task.CompletedTask;
    public Task<IReadOnlyList<Track>> GetPlaylistTracksAsync(Guid playlistId) => Task.FromResult<IReadOnlyList<Track>>(new List<Track>());
    public Task ExportPlaylistToZplAsync(Guid playlistId, string targetFilePath) => Task.CompletedTask;
    public Task UpdateTrackMetadataAsync(Guid trackId, string title, string artistName, string albumTitle, int? year, string genre, int trackNumber, int discNumber) => Task.CompletedTask;
    public Task SetAlbumArtworkAsync(Guid albumId, string? artworkUri) => Task.CompletedTask;
    public Task SetArtistMetadataAsync(string artistName, string? biography, string? thumbnailUri, string? backgroundImageUri, string? musicBrainzId) => Task.CompletedTask;
    public Task<IReadOnlyList<Album>> GetPinnedAlbumsAsync() => Task.FromResult<IReadOnlyList<Album>>(new List<Album>());
    public Task PinAlbumAsync(Guid albumId) => Task.CompletedTask;
    public Task UnpinAlbumAsync(Guid albumId) => Task.CompletedTask;
    public void StartDirectoryWatcher(string directoryPath) { }
    public void StopDirectoryWatcher() { }
}

internal class PlaybackCoordinatorStub : IPlayerCoordinator
{
    public PlaybackState State => PlaybackState.Stopped;
    public Track? CurrentTrack => null;
    public TimeSpan CurrentPosition => TimeSpan.Zero;
    public TimeSpan Duration => TimeSpan.Zero;
    public double Volume { get; set; }
    public bool IsMuted { get; set; }
    public bool Shuffle { get; set; }
    public bool Repeat { get; set; }
    public double CrossfadeDurationSeconds { get; set; }
    public bool IsCrossfading => false;
    public bool GaplessEnabled { get; set; }
    public bool VolumeLevelingEnabled { get; set; }
    public bool IsSimulatedPlayback => true;
    public IReadOnlyList<Track> Queue => Array.Empty<Track>();

    public Task PlayTrackAsync(Track track, IEnumerable<Track>? contextQueue = null) => Task.CompletedTask;
    public Task PlayPauseAsync() => Task.CompletedTask;
    public Task StopAsync() => Task.CompletedTask;
    public Task NextAsync() => Task.CompletedTask;
    public Task PreviousAsync() => Task.CompletedTask;
    public Task SeekAsync(TimeSpan position) => Task.CompletedTask;
    public Task SetRatingAsync(Guid trackId, HeartRating rating) => Task.CompletedTask;
    public void Enqueue(IEnumerable<Track> tracks) { }
    public void PlayNext(IEnumerable<Track> tracks) { }

    public event EventHandler<TrackChangedEventArgs>? TrackChanged { add { } remove { } }
    public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged { add { } remove { } }
    public event EventHandler<HeartRatingChangedEventArgs>? RatingChanged { add { } remove { } }
}
