using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dorado.Application.Events;
using Dorado.Application.Interfaces;
using Dorado.Application.Services;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

public class ZunePhase3FidelityTests
{
    private class FakeSoundEffectService : ISoundEffectService
    {
        public bool SoundEffectsEnabled { get; set; } = true;
        public int RipCount { get; private set; }
        public int BurnCount { get; private set; }
        public int SyncCount { get; private set; }
        public int NotificationCount { get; private set; }

        public void PlaySyncComplete() => SyncCount++;
        public void PlayDownloadComplete() { }
        public void PlayRipComplete() => RipCount++;
        public void PlayBurnComplete() => BurnCount++;
        public void PlayNotification() => NotificationCount++;
    }

    private class FakeLibraryService : IMediaLibraryService
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
        public event EventHandler? LibraryUpdated { add { } remove { } }
    }

    [Fact]
    public async Task MixviewCoordinator_GeneratesValidConstellation_WithCenterAndSatellites()
    {
        var lib = new FakeLibraryService();
        var artistId = Guid.NewGuid();
        lib.Artists.Add(new Artist { Id = artistId, Name = "Rush" });
        lib.Artists.Add(new Artist { Id = Guid.NewGuid(), Name = "Yes" });
        lib.Artists.Add(new Artist { Id = Guid.NewGuid(), Name = "Genesis" });

        var albumId = Guid.NewGuid();
        lib.Albums.Add(new Album { Id = albumId, Title = "Moving Pictures", ArtistId = artistId, ArtistName = "Rush", Year = 1981 });
        lib.Tracks.Add(new Track { Id = Guid.NewGuid(), Title = "Tom Sawyer", ArtistName = "Rush", AlbumTitle = "Moving Pictures", AlbumId = albumId, Genre = "Progressive Rock" });

        var coordinator = new MixviewCoordinator(lib);
        var constellation = await coordinator.GenerateConstellationAsync("Rush", MixNodeType.Artist, artistId);

        Assert.NotNull(constellation);
        Assert.NotNull(constellation.CenterSeed);
        Assert.Equal("Rush", constellation.CenterSeed.Title);
        Assert.True(constellation.CenterSeed.IsCenterSeed);
        Assert.True(constellation.Satellites.Count >= 2, "Mixview constellation should include the seed's albums and tracks");

        // Verify coordinates are calculated
        foreach (var sat in constellation.Satellites)
        {
            Assert.True(sat.OrbitRadius > 0);
            Assert.False(string.IsNullOrEmpty(sat.Title));
        }
    }

    [Fact]
    public async Task MixviewViewModel_SelectingSatellite_PushesToStackAndNavigates()
    {
        var lib = new FakeLibraryService();
        lib.Artists.Add(new Artist { Name = "Pink Floyd" });
        lib.Artists.Add(new Artist { Name = "King Crimson" });
        lib.Albums.Add(new Album { Title = "The Wall", ArtistName = "Pink Floyd", Year = 1979 });
        lib.Tracks.Add(new Track { Title = "Comfortably Numb", ArtistName = "Pink Floyd", AlbumTitle = "The Wall", Genre = "Progressive Rock" });

        var player = new PlaybackQueueCoordinator();
        var smartDJ = new SmartDJEngine();
        var coordinator = new MixviewCoordinator(lib);

        var vm = new MixviewViewModel(coordinator, player, smartDJ, lib);
        await vm.InitializeSeedAsync("Pink Floyd", MixNodeType.Artist);

        Assert.False(vm.CanGoBack);
        Assert.Equal("Pink Floyd", vm.CenterSeed.Title);
        Assert.NotEmpty(vm.Satellites);

        var firstSatellite = vm.Satellites[0];
        var originalSeedTitle = vm.CenterSeed.Title;

        // Select node
        vm.SelectNodeCommand.Execute(firstSatellite);

        // Allow async load to complete
        await Task.Delay(100);

        Assert.True(vm.CanGoBack);
        Assert.Contains(originalSeedTitle.ToUpperInvariant(), vm.HistoryBreadcrumbText);

        // Go Back
        vm.GoBackCommand.Execute(null);
        Assert.False(vm.CanGoBack);
        Assert.Equal(originalSeedTitle, vm.CenterSeed.Title);
    }

    [Fact]
    public async Task PlaybackQueueCoordinator_CrossfadeProperties_FunctionCorrectly()
    {
        var player = new PlaybackQueueCoordinator();
        player.CrossfadeDurationSeconds = 3.0;
        Assert.Equal(3.0, player.CrossfadeDurationSeconds);

        // Clamping test
        player.CrossfadeDurationSeconds = 15.0;
        Assert.Equal(10.0, player.CrossfadeDurationSeconds);
        player.CrossfadeDurationSeconds = -5.0;
        Assert.Equal(0.0, player.CrossfadeDurationSeconds);

        player.CrossfadeDurationSeconds = 2.0;

        var track = new Track
        {
            Id = Guid.NewGuid(),
            Title = "Echoes",
            Duration = TimeSpan.FromSeconds(100)
        };

        await player.PlayTrackAsync(track);
        Assert.False(player.IsCrossfading);

        // Seek to 95 seconds (remaining 5s > crossfade 2s)
        await player.SeekAsync(TimeSpan.FromSeconds(95));
        Assert.False(player.IsCrossfading);

        // Seek to 99 seconds (remaining 1s <= crossfade 2s)
        await player.SeekAsync(TimeSpan.FromSeconds(99));
        Assert.True(player.IsCrossfading);

        // Next track resets crossfading
        await player.NextAsync();
        Assert.False(player.IsCrossfading);
    }

    [Fact]
    public void SettingsViewModel_TwoTierPivots_SwitchCorrectly()
    {
        var vm = new SettingsViewModel();

        // Top level pivot switching
        Assert.True(vm.IsSoftwarePivotActive);
        Assert.False(vm.IsDevicePivotActive);

        vm.SelectTopLevelPivotCommand.Execute("Device");
        Assert.True(vm.IsDevicePivotActive);
        Assert.False(vm.IsSoftwarePivotActive);

        // Device sub-pivots
        vm.SelectDevicePivotCommand.Execute("SpaceReservation");
        Assert.True(vm.IsSpaceReservationSubPivotActive);

        vm.SpaceReservationPercent = 20;
        Assert.Equal(20, vm.SpaceReservationPercent);
        Assert.Contains("6.4 GB", vm.ReservedGbText);
        Assert.Contains("25.6 GB", vm.SyncSpaceGbText);

        // Switch back to Software
        vm.SelectTopLevelPivotCommand.Execute("Software");
        Assert.True(vm.IsSoftwarePivotActive);

        // Software sub-pivots
        vm.SelectSoftwarePivotCommand.Execute("Playback");
        Assert.True(vm.IsPlaybackSubPivotActive);

        vm.CrossfadeDurationSeconds = 4.5;
        Assert.Equal(4.5, vm.CrossfadeDurationSeconds);
        Assert.Equal("4.5 seconds", vm.CrossfadeDurationText);

        vm.SelectSoftwarePivotCommand.Execute("Rip");
        Assert.True(vm.IsRipSubPivotActive);
        Assert.NotEmpty(vm.RipAudioFormats);

        vm.SelectSoftwarePivotCommand.Execute("Burn");
        Assert.True(vm.IsBurnSubPivotActive);
        Assert.NotEmpty(vm.DiscTypes);
    }

    [Fact]
    public async Task CDViewModel_RipAndBurnFlows_ExecuteSuccessfully()
    {
        var lib = new FakeLibraryService();
        var player = new PlaybackQueueCoordinator();
        var sound = new FakeSoundEffectService();

        var vm = new CDViewModel(lib, player, sound);

        Assert.True(vm.IsRipMode);
        Assert.False(vm.IsBurnMode);
        Assert.True(vm.DiscTracks.Count > 0);

        // Execute Rip
        vm.RipCdCommand.Execute(null);
        await WaitForCompletionAsync(() => vm.RipProgress);

        Assert.Equal(1.0, vm.RipProgress);
        Assert.Equal(1, sound.RipCount);

        // Switch to Burn mode
        vm.SwitchModeCommand.Execute("Burn");
        Assert.True(vm.IsBurnMode);

        // Execute Burn
        vm.BurnCdCommand.Execute(null);
        await WaitForCompletionAsync(() => vm.BurnProgress);

        Assert.Equal(1.0, vm.BurnProgress);
        Assert.Equal(1, sound.BurnCount);
    }

    /// <summary>
    /// Polls until the simulated rip/burn progress completes (or fails the deadline).
    /// A fixed Task.Delay is flaky on loaded CI runners where scheduler jitter stacks up.
    /// </summary>
    private static async Task WaitForCompletionAsync(Func<double> progress)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (progress() < 1.0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }
    }

    [Fact]
    public void NowPlayingViewModel_KenBurnsAndMixviewAction_FunctionCorrectly()
    {
        var lib = new FakeLibraryService();
        var player = new PlaybackQueueCoordinator();

        var vm = new NowPlayingViewModel(player, lib);

        Assert.NotNull(vm.CurrentBackdropImage);
        Assert.Equal(24, vm.VisualizerBars.Count);

        string? requestedArtist = null;
        vm.LaunchMixviewRequested += (_, artist) => requestedArtist = artist;

        vm.LaunchMixviewCommand.Execute(null);
        Assert.NotNull(requestedArtist);
        Assert.Equal(vm.ArtistName, requestedArtist);
    }
}
