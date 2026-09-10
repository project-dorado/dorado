using Dorado.Application.Services;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;
using Dorado.UI;
using Dorado.UI.ViewModels;
using Dorado.UI.Views;
using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using Xunit;

namespace Dorado.Tests.Application;

public class NavigationAndPlaybackUiTests
{
    [AvaloniaFact]
    public void ViewLocator_BuildsExpectedViewsForViewModels()
    {
        var locator = new ViewLocator();

        // Create mock/dummy services
        var coordinator = new PlaybackQueueCoordinator();
        var smartDj = new SmartDJEngine();
        var dummyLibrary = new DummyMediaLibraryService();
        var dummyDevice = new DummyDeviceSyncService();

        var qpVm = new QuickplayViewModel(coordinator, dummyLibrary, smartDj);
        var collVm = new CollectionViewModel(coordinator, dummyLibrary);
        var npVm = new NowPlayingViewModel(coordinator, dummyLibrary);
        var devVm = new DeviceViewModel(dummyDevice);
        var setVm = new SettingsViewModel();

        Assert.True(locator.Match(qpVm));
        Assert.IsType<QuickplayView>(locator.Build(qpVm));
        Assert.IsType<CollectionView>(locator.Build(collVm));
        Assert.IsType<NowPlayingView>(locator.Build(npVm));
        Assert.IsType<DeviceView>(locator.Build(devVm));
        Assert.IsType<SettingsView>(locator.Build(setVm));
    }

    [Fact]
    public async Task MainShellViewModel_PlayPauseIcon_ReflectsPlaybackState()
    {
        var coordinator = new PlaybackQueueCoordinator();
        var smartDj = new SmartDJEngine();
        var dummyLibrary = new DummyMediaLibraryService();
        var dummyDevice = new DummyDeviceSyncService();

        var shellVm = new MainShellViewModel(coordinator, dummyLibrary, dummyDevice, smartDj);

        // Initially paused/stopped
        Assert.Equal("▶", shellVm.PlayPauseIcon);

        // Play track
        var track = new Track { Title = "Subdivisions", ArtistName = "Rush" };
        await coordinator.PlayTrackAsync(track);

        Assert.Equal("⏸", shellVm.PlayPauseIcon);

        // Pause
        await coordinator.PlayPauseAsync();
        Assert.Equal("▶", shellVm.PlayPauseIcon);
    }

    [AvaloniaFact]
    public async Task MainShellViewModel_TransportShortcuts_StopSeekRewindForward()
    {
        var coordinator = new PlaybackQueueCoordinator();
        var shellVm = new MainShellViewModel(coordinator, new DummyMediaLibraryService(), new DummyDeviceSyncService(), new SmartDJEngine());

        var track = new Track { Title = "Tom Sawyer", ArtistName = "Rush", Duration = TimeSpan.FromMinutes(3) };
        await coordinator.PlayTrackAsync(track);
        Assert.True(shellVm.IsPlaying);

        // Ctrl+S → StopCommand halts playback
        shellVm.StopCommand.Execute(null);
        Assert.False(shellVm.IsPlaying);

        // Ctrl+Right → FastForwardCommand advances ~5s (clamped to duration)
        await coordinator.SeekAsync(TimeSpan.FromSeconds(3));
        shellVm.FastForwardCommand.Execute(null);
        Assert.InRange(shellVm.CurrentPosition.TotalSeconds, 7.5, 8.5);

        // Ctrl+Left → RewindCommand retreats ~5s (clamped to zero)
        shellVm.RewindCommand.Execute(null);
        Assert.InRange(shellVm.CurrentPosition.TotalSeconds, 1.5, 3.5);
        shellVm.RewindCommand.Execute(null);
        shellVm.RewindCommand.Execute(null);
        shellVm.RewindCommand.Execute(null);
        Assert.Equal(TimeSpan.Zero, shellVm.CurrentPosition);
    }

    [AvaloniaFact]
    public void SettingsViewModel_SelectAccentCommand_UpdatesAccentColor()
    {
        var vm = new SettingsViewModel();
        Assert.Equal("#F10DA2", vm.SelectedAccent.HexCode);

        var cyan = vm.AccentColors.First(a => a.HexCode == "#1BA1E2");
        vm.SelectAccentCommand.Execute(cyan);

        Assert.Equal(cyan, vm.SelectedAccent);
        Assert.Equal("#1BA1E2", vm.SelectedAccent.HexCode);
    }

    [Fact]
    public void MainShellViewModel_PivotSwitching_UpdatesCurrentView()
    {
        var coordinator = new PlaybackQueueCoordinator();
        var smartDj = new SmartDJEngine();
        var dummyLibrary = new DummyMediaLibraryService();
        var dummyDevice = new DummyDeviceSyncService();

        var shellVm = new MainShellViewModel(coordinator, dummyLibrary, dummyDevice, smartDj);

        Assert.IsType<QuickplayViewModel>(shellVm.CurrentView);

        shellVm.SelectPivotCommand.Execute(NavigationPivot.Collection);
        Assert.IsType<CollectionViewModel>(shellVm.CurrentView);

        shellVm.SelectPivotCommand.Execute(NavigationPivot.Device);
        Assert.IsType<DeviceViewModel>(shellVm.CurrentView);

        shellVm.SelectPivotCommand.Execute(NavigationPivot.Settings);
        Assert.IsType<SettingsViewModel>(shellVm.CurrentView);

        shellVm.ToggleNowPlayingCommand.Execute(null);
        Assert.IsType<NowPlayingViewModel>(shellVm.CurrentView);
    }

    [AvaloniaFact]
    public async Task Quickplay_LoadsDynamicMixes_AndInvokesPlay()
    {
        var coordinator = new PlaybackQueueCoordinator();
        var library = new DummyMediaLibraryService();
        var dynamicMixes = new DynamicMixService(new AudioAnalysisService());

        var qpVm = new QuickplayViewModel(coordinator, library, new SmartDJEngine(), dynamicMixes);
        await qpVm.LoadInitialDataAsync();

        Assert.True(qpVm.HasDynamicMixes);
        Assert.Contains(qpVm.DynamicMixes, m => m.Kind == DynamicMixKind.TopPlayed);

        // Executing against an empty library must complete without throwing.
        await ((AsyncRelayCommand<DynamicMix>)qpVm.PlayDynamicMixCommand).ExecuteAsync(qpVm.DynamicMixes.First());
    }
}

// Dummy test stubs
internal class DummyMediaLibraryService : Dorado.Application.Interfaces.IMediaLibraryService
{
    public Task<IReadOnlyList<Track>> GetAllTracksAsync() => Task.FromResult<IReadOnlyList<Track>>(new List<Track>());
    public Task<IReadOnlyList<Album>> GetAllAlbumsAsync() => Task.FromResult<IReadOnlyList<Album>>(new List<Album>());
    public Task<IReadOnlyList<Artist>> GetAllArtistsAsync() => Task.FromResult<IReadOnlyList<Artist>>(new List<Artist>());
    public Task<IReadOnlyList<Playlist>> GetAllPlaylistsAsync() => Task.FromResult<IReadOnlyList<Playlist>>(new List<Playlist>());
    public Task<IReadOnlyList<PlayHistoryEntry>> GetRecentHistoryAsync(int count = 20) => Task.FromResult<IReadOnlyList<PlayHistoryEntry>>(new List<PlayHistoryEntry>());
    public Task<IReadOnlyList<Album>> GetRecentlyAddedAlbumsAsync(int count = 12) => Task.FromResult<IReadOnlyList<Album>>(new List<Album>());
    public Task<IReadOnlyList<Track>> SearchAsync(string query) => Task.FromResult<IReadOnlyList<Track>>(new List<Track>());
    public Task SetTrackRatingAsync(Guid trackId, HeartRating rating) => Task.CompletedTask;
    public Task ScanDirectoryAsync(string directoryPath, IProgress<double>? progress = null) => Task.CompletedTask;
    public Task ClearDemoDataAsync() => Task.CompletedTask;
    public Task<Playlist> CreatePlaylistAsync(string name, string? description = null) => Task.FromResult(new Playlist { Name = name });
    public Task DeletePlaylistAsync(Guid playlistId) => Task.CompletedTask;
    public Task AddTrackToPlaylistAsync(Guid playlistId, Guid trackId) => Task.CompletedTask;
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
#pragma warning disable CS0067
    public event EventHandler? LibraryUpdated;
#pragma warning restore CS0067
}

internal class DummyDeviceSyncService : Dorado.Application.Interfaces.IDeviceSyncService
{
    public IReadOnlyList<ZuneDevice> ConnectedDevices => new List<ZuneDevice>();
    public Task StartMonitoringAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task SyncDeviceAsync(string serialNumber, IProgress<double>? progress = null) => Task.CompletedTask;
#pragma warning disable CS0067
    public event EventHandler<ZuneDevice>? DeviceConnected;
    public event EventHandler<string>? DeviceDisconnected;
#pragma warning restore CS0067
}

