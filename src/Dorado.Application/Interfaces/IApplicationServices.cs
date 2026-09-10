using Dorado.Application.Events;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;

namespace Dorado.Application.Interfaces;

public interface IPlayerCoordinator
{
    PlaybackState State { get; }
    Track? CurrentTrack { get; }
    TimeSpan CurrentPosition { get; }
    TimeSpan Duration { get; }
    double Volume { get; set; }
    bool IsMuted { get; set; }
    bool Shuffle { get; set; }
    bool Repeat { get; set; }
    double CrossfadeDurationSeconds { get; set; }
    bool IsCrossfading { get; }
    bool GaplessEnabled { get; set; }
    bool VolumeLevelingEnabled { get; set; }
    bool IsSimulatedPlayback { get; }
    IReadOnlyList<Track> Queue { get; }

    Task PlayTrackAsync(Track track, IEnumerable<Track>? contextQueue = null);
    Task PlayPauseAsync();
    Task StopAsync();
    Task NextAsync();
    Task PreviousAsync();
    Task SeekAsync(TimeSpan position);
    Task SetRatingAsync(Guid trackId, HeartRating rating);
    void Enqueue(IEnumerable<Track> tracks);
    void PlayNext(IEnumerable<Track> tracks);

    event EventHandler<TrackChangedEventArgs>? TrackChanged;
    event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;
    event EventHandler<HeartRatingChangedEventArgs>? RatingChanged;
}

public interface IMediaLibraryService
{
    Task<IReadOnlyList<Track>> GetAllTracksAsync();
    Task<IReadOnlyList<Album>> GetAllAlbumsAsync();
    Task<IReadOnlyList<Artist>> GetAllArtistsAsync();
    Task<IReadOnlyList<Playlist>> GetAllPlaylistsAsync();
    Task<IReadOnlyList<PlayHistoryEntry>> GetRecentHistoryAsync(int count = 20);
    Task<IReadOnlyList<Album>> GetRecentlyAddedAlbumsAsync(int count = 12);
    Task<IReadOnlyList<Track>> SearchAsync(string query);
    Task SetTrackRatingAsync(Guid trackId, HeartRating rating);
    Task ScanDirectoryAsync(string directoryPath, IProgress<double>? progress = null);
    Task ClearDemoDataAsync();
    Task<Playlist> CreatePlaylistAsync(string name, string? description = null);
    Task DeletePlaylistAsync(Guid playlistId);
    Task AddTrackToPlaylistAsync(Guid playlistId, Guid trackId);
    Task RemoveTrackFromPlaylistAsync(Guid playlistId, Guid trackId);
    Task<IReadOnlyList<Track>> GetPlaylistTracksAsync(Guid playlistId);
    Task ExportPlaylistToZplAsync(Guid playlistId, string targetFilePath);
    Task UpdateTrackMetadataAsync(Guid trackId, string title, string artistName, string albumTitle, int? year, string genre, int trackNumber, int discNumber);
    Task SetAlbumArtworkAsync(Guid albumId, string? artworkUri);
    Task SetArtistMetadataAsync(string artistName, string? biography, string? thumbnailUri, string? backgroundImageUri, string? musicBrainzId);
    Task<IReadOnlyList<Album>> GetPinnedAlbumsAsync();
    Task PinAlbumAsync(Guid albumId);
    Task UnpinAlbumAsync(Guid albumId);
    void StartDirectoryWatcher(string directoryPath);
    void StopDirectoryWatcher();
    event EventHandler? LibraryUpdated;
}

public interface IDeviceSyncService
{
    IReadOnlyList<ZuneDevice> ConnectedDevices { get; }
    Task StartMonitoringAsync(CancellationToken cancellationToken);
    Task SyncDeviceAsync(string serialNumber, IProgress<double>? progress = null);
    event EventHandler<ZuneDevice>? DeviceConnected;
    event EventHandler<string>? DeviceDisconnected;
}

public interface ISmartDJService
{
    Task<IReadOnlyList<Track>> GenerateMixAsync(SmartDJSeed seed, IReadOnlyList<Track> libraryTracks);
}
