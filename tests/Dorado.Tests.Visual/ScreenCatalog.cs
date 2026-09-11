using Avalonia.Controls;
using Dorado.Application.Interfaces;
using Dorado.Application.Services;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;
using Dorado.UI.ViewModels;
using Dorado.UI.Views;

namespace Dorado.Tests.Visual;

public sealed record ScreenSpec(string Name, int Width, int Height, Func<MainShellViewModel, Control> Build);

/// <summary>
/// The canonical set of screens exercised by the visual-regression gate. Each spec
/// builds a fresh control tree from a freshly-seeded shell so tests are independent.
/// </summary>
public static class ScreenCatalog
{
    public const int W = 1280;
    public const int H = 800;

    public static MainShellViewModel NewShell() =>
        new(new PlaybackQueueCoordinator(), SeedLibrary.Build(), new SeedDeviceSync(), new SmartDJEngine());

    public static IReadOnlyList<ScreenSpec> All { get; } = BuildSpecs();

    private static List<ScreenSpec> BuildSpecs()
    {
        var specs = new List<ScreenSpec>();

        void Pivot(string name, NavigationPivot pivot, int w = W, int h = H) =>
            specs.Add(new(name, w, h, vm => { vm.ActivePivot = pivot; return new MainShellView { DataContext = vm }; }));

        Pivot("quickplay", NavigationPivot.Quickplay);
        Pivot("collection-artists", NavigationPivot.Collection, W, H);
        Pivot("device", NavigationPivot.Device);
        Pivot("disc-no-disc", NavigationPivot.Disc);
        Pivot("social-zunecard", NavigationPivot.Social);
        Pivot("mixview", NavigationPivot.Mixview);
        Pivot("settings-about", NavigationPivot.Settings);
        Pivot("min-quickplay", NavigationPivot.Quickplay, 734, 500);
        Pivot("min-nowplaying", NavigationPivot.NowPlaying, 734, 500);

        // Collection sub-pivots
        AddCollection(specs, "collection-albums", CollectionSubPivot.Albums);
        AddCollection(specs, "collection-songs", CollectionSubPivot.Songs);
        AddCollection(specs, "collection-playlists", CollectionSubPivot.Playlists);

        // Now Playing mode variants
        void NowPlaying(string name, NowPlayingMode mode, int w = W, int h = H) =>
            specs.Add(new(name, w, h, vm =>
            {
                vm.ActivePivot = NavigationPivot.NowPlaying;
                vm.NowPlayingVM.Mode = mode;
                return new MainShellView { DataContext = vm };
            }));

        NowPlaying("nowplaying-artistcanvas", NowPlayingMode.ArtistCanvas);
        NowPlaying("nowplaying-mosaicwall", NowPlayingMode.MosaicWall);
        NowPlaying("nowplaying-video", NowPlayingMode.Video);

        // Settings device info (honest connected state)
        specs.Add(new("settings-device-info", W, H, vm =>
        {
            vm.ActivePivot = NavigationPivot.Settings;
            vm.SettingsVM.TopLevelPivot = SettingsTopLevelPivot.Device;
            vm.SettingsVM.DevicePivot = DeviceSubPivot.DeviceInfo;
            return new MainShellView { DataContext = vm };
        }));

        specs.Add(new("compact-miniplayer", 340, 96, vm =>
        {
            vm.IsCompactMode = true;
            return new CompactMiniPlayerView { DataContext = vm };
        }));

        specs.Add(new("firstlaunch-wizard", W, H, vm =>
            new FirstLaunchWizardView { DataContext = new FirstLaunchWizardViewModel(SeedLibrary.Build(), null, null) }));

        specs.Add(new("firstconnect-wizard", W, H, vm =>
            new FirstConnectWizardView { DataContext = new FirstConnectWizardViewModel(new ZuneDevice { ModelName = "Zune HD", SerialNumber = "0001020304050607", FirmwareVersion = "4.8" }) }));

        specs.Add(new("whatsnew", W, H, vm => new WhatsNewView { DataContext = new WhatsNewViewModel(null) }));

        return specs;
    }

    private static void AddCollection(List<ScreenSpec> specs, string name, CollectionSubPivot sub) =>
        specs.Add(new(name, W, H, vm =>
        {
            vm.ActivePivot = NavigationPivot.Collection;
            vm.CollectionVM.ActiveMediaGroup = CollectionMediaGroup.Music;
            vm.CollectionVM.ActiveSubPivot = sub;
            return new MainShellView { DataContext = vm };
        }));
}

/// <summary>Deterministic seeded library for stable renders.</summary>
public class SeedLibrary : IMediaLibraryService
{
    public List<Track> Tracks { get; } = new();
    public List<Album> Albums { get; } = new();
    public List<Artist> Artists { get; } = new();
    public List<Playlist> Playlists { get; } = new();

    public static SeedLibrary Build()
    {
        var lib = new SeedLibrary();
        string[] names = { "Radiohead", "Rush", "Sigur Ros", "The Strokes", "Spoon", "Portishead" };
        string[] albums = { "OK Computer", "Moving Pictures", "Agaetis Byrjun", "Is This It", "Ga Ga Ga Ga Ga", "Dummy" };
        for (int i = 0; i < names.Length; i++)
        {
            var artist = new Artist { Name = names[i], SortName = names[i] };
            var album = new Album { Title = albums[i], ArtistName = names[i], ArtistId = artist.Id, Year = 1990 + i, Genre = "Alternative" };
            for (int t = 1; t <= 6; t++)
            {
                album.Tracks.Add(new Track
                {
                    Title = $"{albums[i]} Track {t}",
                    ArtistName = names[i],
                    ArtistId = artist.Id,
                    AlbumTitle = albums[i],
                    AlbumId = album.Id,
                    TrackNumber = t,
                    Duration = TimeSpan.FromSeconds(180 + t * 5),
                    Genre = "Alternative",
                    FilePath = $"/nonexistent/{names[i]}/{t:00}.mp3"
                });
                lib.Tracks.Add(album.Tracks[^1]);
            }
            album.TrackCount = album.Tracks.Count;
            artist.Albums.Add(album);
            lib.Albums.Add(album);
            lib.Artists.Add(artist);
        }
        lib.Playlists.Add(new Playlist { Name = "Road Trip", Description = "Loud and long" });
        return lib;
    }

    public Task<IReadOnlyList<Track>> GetAllTracksAsync() => Task.FromResult<IReadOnlyList<Track>>(Tracks);
    public Task<IReadOnlyList<Album>> GetAllAlbumsAsync() => Task.FromResult<IReadOnlyList<Album>>(Albums);
    public Task<IReadOnlyList<Artist>> GetAllArtistsAsync() => Task.FromResult<IReadOnlyList<Artist>>(Artists);
    public Task<IReadOnlyList<Playlist>> GetAllPlaylistsAsync() => Task.FromResult<IReadOnlyList<Playlist>>(Playlists);
    public Task<IReadOnlyList<PlayHistoryEntry>> GetRecentHistoryAsync(int count = 20) => Task.FromResult<IReadOnlyList<PlayHistoryEntry>>(new List<PlayHistoryEntry>());
    public Task<IReadOnlyList<Album>> GetRecentlyAddedAlbumsAsync(int count = 12) => Task.FromResult<IReadOnlyList<Album>>(Albums.Take(count).ToList());
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

public class SeedDeviceSync : IDeviceSyncService
{
    private readonly List<ZuneDevice> _devices = new()
    {
        new ZuneDevice
        {
            SerialNumber = "0001020304050607",
            ModelName = "Zune HD",
            FirmwareVersion = "4.8",
            CapacityBytes = 32L * 1024 * 1024 * 1024,
            FreeSpaceBytes = 9L * 1024 * 1024 * 1024,
            MusicBytes = 14L * 1024 * 1024 * 1024,
            VideoBytes = 6L * 1024 * 1024 * 1024,
            PhotoBytes = 2L * 1024 * 1024 * 1024,
            PodcastBytes = 1L * 1024 * 1024 * 1024,
            IsConnected = true,
            IsPaired = true,
            SyncState = DeviceSyncState.Connected
        }
    };

    public IReadOnlyList<ZuneDevice> ConnectedDevices => _devices;
    public Task StartMonitoringAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task SyncDeviceAsync(string serialNumber, IProgress<double>? progress = null) => Task.CompletedTask;
#pragma warning disable CS0067
    public event EventHandler<ZuneDevice>? DeviceConnected;
    public event EventHandler<string>? DeviceDisconnected;
#pragma warning restore CS0067
}
