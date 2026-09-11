using Dorado.Application.Interfaces;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;

namespace Dorado.Tests.Application.TestFakes;

internal class FakeMediaLibraryService : IMediaLibraryService
{
    public List<Track> Tracks { get; } = new();
    public List<Album> Albums { get; } = new();
    public List<Artist> Artists { get; } = new();

    public Task<IReadOnlyList<Track>> GetAllTracksAsync() => Task.FromResult<IReadOnlyList<Track>>(Tracks);
    public Task<IReadOnlyList<Album>> GetAllAlbumsAsync() => Task.FromResult<IReadOnlyList<Album>>(Albums);
    public Task<IReadOnlyList<Artist>> GetAllArtistsAsync() => Task.FromResult<IReadOnlyList<Artist>>(Artists);
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

internal class FakeDeviceSyncService : IDeviceSyncService
{
    public IReadOnlyList<ZuneDevice> ConnectedDevices => new List<ZuneDevice>();
    public Task StartMonitoringAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task SyncDeviceAsync(string serialNumber, IProgress<double>? progress = null) => Task.CompletedTask;
#pragma warning disable CS0067
    public event EventHandler<ZuneDevice>? DeviceConnected;
    public event EventHandler<string>? DeviceDisconnected;
#pragma warning restore CS0067
}
