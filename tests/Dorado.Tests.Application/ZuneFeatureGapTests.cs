using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Dorado.Application.Interfaces;
using Dorado.Application.Services;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;
using Dorado.Infrastructure.Audio;
using Dorado.UI;
using Dorado.UI.ViewModels;
using Dorado.UI.Views;
using Xunit;

namespace Dorado.Tests.Application;

public class ZuneFeatureGapTests
{
    private class TestMediaLibraryService : IMediaLibraryService
    {
        public List<Track> Tracks { get; set; } = new();
        public List<PlayHistoryEntry> History { get; set; } = new();
        public List<Playlist> Playlists { get; set; } = new();
        public (Guid trackId, string title, string artist, string album, int? year, string genre, int trackNum, int discNum)? LastUpdatedMetadata { get; set; }

        public Task<IReadOnlyList<Track>> GetAllTracksAsync() => Task.FromResult<IReadOnlyList<Track>>(Tracks);
        public Task<IReadOnlyList<Album>> GetAllAlbumsAsync() => Task.FromResult<IReadOnlyList<Album>>(new List<Album>());
        public Task<IReadOnlyList<Artist>> GetAllArtistsAsync() => Task.FromResult<IReadOnlyList<Artist>>(new List<Artist>());
        public Task<IReadOnlyList<Playlist>> GetAllPlaylistsAsync() => Task.FromResult<IReadOnlyList<Playlist>>(Playlists);
        public Task<IReadOnlyList<PlayHistoryEntry>> GetRecentHistoryAsync(int count = 20) => Task.FromResult<IReadOnlyList<PlayHistoryEntry>>(History);
        public Task<IReadOnlyList<Album>> GetRecentlyAddedAlbumsAsync(int count = 12) => Task.FromResult<IReadOnlyList<Album>>(new List<Album>());
        public Task<IReadOnlyList<Track>> SearchAsync(string query) => Task.FromResult<IReadOnlyList<Track>>(new List<Track>());
        public Task SetTrackRatingAsync(Guid trackId, HeartRating rating) => Task.CompletedTask;
        public Task ScanDirectoryAsync(string directoryPath, IProgress<double>? progress = null) => Task.CompletedTask;
        public Task ClearDemoDataAsync() => Task.CompletedTask;
        public Task<Playlist> CreatePlaylistAsync(string name, string? description = null)
        {
            var p = new Playlist { Id = Guid.NewGuid(), Name = name, Description = description };
            Playlists.Add(p);
            return Task.FromResult(p);
        }
        public Task DeletePlaylistAsync(Guid playlistId)
        {
            Playlists.RemoveAll(p => p.Id == playlistId);
            return Task.CompletedTask;
        }
        public Task AddTrackToPlaylistAsync(Guid playlistId, Guid trackId) => Task.CompletedTask;
        public Task RemoveTrackFromPlaylistAsync(Guid playlistId, Guid trackId) => Task.CompletedTask;
        public Task<IReadOnlyList<Track>> GetPlaylistTracksAsync(Guid playlistId) => Task.FromResult<IReadOnlyList<Track>>(Tracks);
        public Task ExportPlaylistToZplAsync(Guid playlistId, string targetFilePath) => Task.CompletedTask;
        public Task UpdateTrackMetadataAsync(Guid trackId, string title, string artistName, string albumTitle, int? year, string genre, int trackNumber, int discNumber)
        {
            LastUpdatedMetadata = (trackId, title, artistName, albumTitle, year, genre, trackNumber, discNumber);
            return Task.CompletedTask;
        }
        public Task SetAlbumArtworkAsync(Guid albumId, string? artworkUri) => Task.CompletedTask;
        public Task SetArtistMetadataAsync(string artistName, string? biography, string? thumbnailUri, string? backgroundImageUri, string? musicBrainzId) => Task.CompletedTask;
        public List<Album> PinnedAlbums { get; set; } = new();
        public Task<IReadOnlyList<Album>> GetPinnedAlbumsAsync() => Task.FromResult<IReadOnlyList<Album>>(PinnedAlbums);
        public Task PinAlbumAsync(Guid albumId)
        {
            var album = PinnedAlbums.FirstOrDefault(a => a.Id == albumId);
            if (album != null) album.IsPinned = true;
            return Task.CompletedTask;
        }
        public Task UnpinAlbumAsync(Guid albumId)
        {
            var album = PinnedAlbums.FirstOrDefault(a => a.Id == albumId);
            if (album != null) album.IsPinned = false;
            return Task.CompletedTask;
        }
        public void StartDirectoryWatcher(string directoryPath) { }
        public void StopDirectoryWatcher() { }
#pragma warning disable CS0067
        public event EventHandler? LibraryUpdated;
#pragma warning restore CS0067
    }

    private class TestDeviceSyncService : IDeviceSyncService
    {
        public List<ZuneDevice> Devices { get; set; } = new();
        public IReadOnlyList<ZuneDevice> ConnectedDevices => Devices;
        public Task StartMonitoringAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SyncDeviceAsync(string serialNumber, IProgress<double>? progress = null)
        {
            progress?.Report(1.0);
            return Task.CompletedTask;
        }
#pragma warning disable CS0067
        public event EventHandler<ZuneDevice>? DeviceConnected;
        public event EventHandler<string>? DeviceDisconnected;
#pragma warning restore CS0067
    }

    [Fact]
    public async Task UserStatsService_CalculatesProfile_AndTopArtistsCorrectly()
    {
        var lib = new TestMediaLibraryService();
        lib.History.Add(new PlayHistoryEntry
        {
            TrackTitle = "Subdivisions",
            ArtistName = "Rush",
            DurationPlayed = TimeSpan.FromMinutes(5)
        });
        lib.History.Add(new PlayHistoryEntry
        {
            TrackTitle = "Tom Sawyer",
            ArtistName = "Rush",
            DurationPlayed = TimeSpan.FromMinutes(4)
        });
        lib.History.Add(new PlayHistoryEntry
        {
            TrackTitle = "One More Time",
            ArtistName = "Daft Punk",
            DurationPlayed = TimeSpan.FromMinutes(5)
        });

        var statsService = new UserStatsService(lib);
        var profile = await statsService.GetProfileAsync();

        Assert.Equal("ZuneUser", profile.ZuneTag);
        Assert.Equal(3, profile.TotalTracksPlayed);
        Assert.Equal(TimeSpan.FromMinutes(14), profile.TotalListeningTime);

        var topArtists = await statsService.GetTopArtistsAsync(5);
        Assert.NotEmpty(topArtists);
        Assert.Equal("Rush", topArtists[0].ArtistName);
        Assert.Equal(2, topArtists[0].PlayCount);
        Assert.Equal(66.7, topArtists[0].Percentage);
    }

    [Fact]
    public async Task UserStatsService_Badges_EvaluatesMilestones()
    {
        var lib = new TestMediaLibraryService();
        for (int i = 0; i < 50; i++)
        {
            lib.History.Add(new PlayHistoryEntry
            {
                TrackTitle = $"Track {i}",
                ArtistName = "Rush",
                DurationPlayed = TimeSpan.FromMinutes(3)
            });
        }

        var statsService = new UserStatsService(lib);
        var badges = await statsService.GetBadgesAsync();

        // 50 plays by one artist → Artist Power Listener reaches Bronze (>= 25).
        var artistBadge = badges.First(b => b.Id == "badge_artist_power");
        Assert.True(artistBadge.IsUnlocked);
        Assert.Equal(BadgeTier.Bronze, artistBadge.Tier);

        // One album ("") accumulated 50 plays → Album Power Listener reaches Silver (>= 30).
        var albumBadge = badges.First(b => b.Id == "badge_album_power");
        Assert.Equal(BadgeTier.Silver, albumBadge.Tier);

        // 50 total plays is below the Milestone Bronze threshold (100).
        var milestoneBadge = badges.First(b => b.Id == "badge_milestone");
        Assert.False(milestoneBadge.IsUnlocked);
        Assert.Equal(BadgeTier.None, milestoneBadge.Tier);
    }

    [Fact]
    public async Task PodcastService_StartsEmptyAndPlaysEpisodes()
    {
        var coordinator = new PlaybackQueueCoordinator();
        var podcastService = new PodcastService(coordinator);

        // No demo podcasts are seeded; the list starts empty until the user subscribes.
        var podcasts = await podcastService.GetAllPodcastsAsync();
        Assert.Empty(podcasts);

        var firstEp = new PodcastEpisode
        {
            SeriesTitle = "Test Series",
            Title = "Episode 1",
            Duration = TimeSpan.FromMinutes(10),
            AudioUrl = "https://example.com/ep1.mp3"
        };

        await podcastService.PlayEpisodeAsync(firstEp);

        Assert.NotNull(coordinator.CurrentTrack);
        Assert.Equal(firstEp.Title, coordinator.CurrentTrack.Title);
        Assert.Equal(firstEp.SeriesTitle, coordinator.CurrentTrack.ArtistName);
    }

    [Fact]
    public void SoundEffectService_CanToggleAndPlay()
    {
        var soundService = new SoundEffectService();
        Assert.True(soundService.SoundEffectsEnabled);

        soundService.SoundEffectsEnabled = false;
        Assert.False(soundService.SoundEffectsEnabled);

        // Verify calls do not throw even if audio hardware is absent
        soundService.PlaySyncComplete();
        soundService.PlayRipComplete();
        soundService.PlayDownloadComplete();
        soundService.PlayNotification();
    }

    [Fact]
    public void MainShellViewModel_CompactMode_TogglesAndFiresEvent()
    {
        var coordinator = new PlaybackQueueCoordinator();
        var smartDj = new SmartDJEngine();
        var lib = new TestMediaLibraryService();
        var dev = new TestDeviceSyncService();

        var shellVm = new MainShellViewModel(coordinator, lib, dev, smartDj);

        bool eventFired = false;
        bool compactState = false;
        shellVm.CompactModeChanged += (s, isCompact) =>
        {
            eventFired = true;
            compactState = isCompact;
        };

        Assert.False(shellVm.IsCompactMode);
        shellVm.ToggleCompactModeCommand.Execute(null);

        Assert.True(shellVm.IsCompactMode);
        Assert.True(eventFired);
        Assert.True(compactState);

        shellVm.ToggleCompactModeCommand.Execute(null);
        Assert.False(shellVm.IsCompactMode);
        Assert.False(compactState);
    }

    [Fact]
    public void MainShellViewModel_SocialPivot_SwitchesToZuneCard()
    {
        var coordinator = new PlaybackQueueCoordinator();
        var smartDj = new SmartDJEngine();
        var lib = new TestMediaLibraryService();
        var dev = new TestDeviceSyncService();

        var shellVm = new MainShellViewModel(coordinator, lib, dev, smartDj);

        shellVm.SelectPivotCommand.Execute(NavigationPivot.Social);
        Assert.IsType<ZuneCardViewModel>(shellVm.CurrentView);
        Assert.True(shellVm.IsSocialActive);

        // Toggle user card button
        shellVm.ToggleZuneCardCommand.Execute(null);
        Assert.Equal(NavigationPivot.Collection, shellVm.ActivePivot);

        shellVm.ToggleZuneCardCommand.Execute(null);
        Assert.Equal(NavigationPivot.Social, shellVm.ActivePivot);
    }

    [Fact]
    public void SettingsViewModel_BackgroundThemes_CanBeSelected()
    {
        var settingsVm = new SettingsViewModel();
        Assert.NotEmpty(settingsVm.BackgroundThemes);

        string? selectedUri = null;
        settingsVm.BackgroundArtChanged += (s, uri) => selectedUri = uri;

        var aurora = settingsVm.BackgroundThemes.First(t => t.Name.Contains("Aurora"));
        settingsVm.SelectBackgroundCommand.Execute(aurora);

        Assert.Equal(aurora, settingsVm.SelectedBackground);
        Assert.Equal(aurora.AssetUri, selectedUri);
    }

    [AvaloniaFact]
    public void ViewLocator_ResolvesZuneCardAndPodcasts()
    {
        var locator = new ViewLocator();
        var coordinator = new PlaybackQueueCoordinator();
        var lib = new TestMediaLibraryService();
        var stats = new UserStatsService(lib);
        var podService = new PodcastService(coordinator);

        var zuneCardVm = new ZuneCardViewModel(stats);
        var podcastsVm = new PodcastsViewModel(podService);

        Assert.True(locator.Match(zuneCardVm));
        Assert.IsType<ZuneCardView>(locator.Build(zuneCardVm));

        Assert.True(locator.Match(podcastsVm));
        Assert.IsType<PodcastsView>(locator.Build(podcastsVm));
    }

    [Fact]
    public async Task DeviceViewModel_GasGauge_ComputesProportionsCorrectly()
    {
        var dev = new TestDeviceSyncService();
        var zune = new ZuneDevice
        {
            ModelName = "Zune HD 32GB",
            SerialNumber = "ZUNE-HD-TEST-1234",
            FirmwareVersion = "4.8",
            CapacityBytes = 32L * 1024 * 1024 * 1024,
            FreeSpaceBytes = 12L * 1024 * 1024 * 1024,
            MusicBytes = 10L * 1024 * 1024 * 1024,
            VideoBytes = 4L * 1024 * 1024 * 1024,
            PhotoBytes = 2L * 1024 * 1024 * 1024,
            PodcastBytes = 2L * 1024 * 1024 * 1024,
            SystemBytes = 2L * 1024 * 1024 * 1024
        };
        dev.Devices.Add(zune);

        var vm = new DeviceViewModel(dev);

        Assert.True(vm.HasDevice);
        Assert.Equal("Zune HD 32GB", vm.DeviceName);
        Assert.Equal("ZUNE-HD-TEST-1234", vm.SerialNumber);
        Assert.Equal(32.0, vm.TotalGb);
        Assert.Equal(12.0, vm.FreeGb);
        Assert.Equal(10.0, vm.MusicGb);
        Assert.Equal(4.0, vm.VideoGb);
        Assert.Equal(2.0, vm.PhotoGb);
        Assert.Equal(2.0, vm.PodcastGb);
        Assert.Equal(2.0, vm.SystemGb);
        Assert.Contains("12.0 GB free of 32.0 GB", vm.StorageText);
        Assert.NotNull(vm.GasGaugeColumns);
        var columnParts = vm.GasGaugeColumns.Split(',');
        Assert.Equal(6, columnParts.Length);
        Assert.All(columnParts, p => Assert.EndsWith("*", p));

        // Test sync command
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.SyncCommand).ExecuteAsync(null);
        Assert.Equal(1.0, vm.SyncProgress);
        Assert.False(vm.IsSyncing);
    }

    [Fact]
    public void DeviceViewModel_StorageSegments_UseAuthenticZuneMediaColors()
    {
        var dev = new TestDeviceSyncService();
        dev.Devices.Add(new ZuneDevice
        {
            ModelName = "Zune HD 32GB",
            SerialNumber = "ZUNE-HD-TEST-1234",
            FirmwareVersion = "4.8",
            CapacityBytes = 32L * 1024 * 1024 * 1024,
            FreeSpaceBytes = 12L * 1024 * 1024 * 1024,
            MusicBytes = 10L * 1024 * 1024 * 1024,
            VideoBytes = 4L * 1024 * 1024 * 1024,
            PhotoBytes = 2L * 1024 * 1024 * 1024,
            PodcastBytes = 2L * 1024 * 1024 * 1024,
            SystemBytes = 2L * 1024 * 1024 * 1024
        });

        var vm = new DeviceViewModel(dev);
        var segments = vm.StorageSegments;

        Assert.Equal(6, segments.Count);
        Assert.Equal(new[] { "MUSIC", "VIDEO", "PICTURES", "PODCASTS", "SYSTEM", "FREE" },
            segments.Select(s => s.Label).ToArray());
        Assert.Equal(10.0 * 1024 * 1024 * 1024, segments[0].Bytes);
        Assert.All(segments, s => Assert.False(string.IsNullOrWhiteSpace(s.Tooltip)));
    }

    [Fact]
    public void MainShellViewModel_HeaderSearch_SwitchesToCollectionAndFilters()
    {
        var coordinator = new PlaybackQueueCoordinator();
        var smartDj = new SmartDJEngine();
        var lib = new TestMediaLibraryService();
        var dev = new TestDeviceSyncService();

        var shellVm = new MainShellViewModel(coordinator, lib, dev, smartDj);
        Assert.Equal(NavigationPivot.Quickplay, shellVm.ActivePivot);

        // Act: Type in search
        shellVm.HeaderSearchQuery = "Rush";

        // Assert: Auto-switched to collection and synced search query
        Assert.Equal(NavigationPivot.Collection, shellVm.ActivePivot);
        Assert.Equal("Rush", shellVm.CollectionVM.SearchQuery);
        Assert.True(shellVm.HasHeaderSearchQuery);

        // Act: Clear search
        shellVm.ClearSearchCommand.Execute(null);
        Assert.Equal(string.Empty, shellVm.HeaderSearchQuery);
        Assert.False(shellVm.HasHeaderSearchQuery);
    }

    [Fact]
    public void MainShellViewModel_QuickDock_NavigatesToDeviceAndPlaylists()
    {
        var coordinator = new PlaybackQueueCoordinator();
        var smartDj = new SmartDJEngine();
        var lib = new TestMediaLibraryService();
        var dev = new TestDeviceSyncService();

        var shellVm = new MainShellViewModel(coordinator, lib, dev, smartDj);

        // Test navigate to Device
        shellVm.OpenDeviceCommand.Execute(null);
        Assert.Equal(NavigationPivot.Device, shellVm.ActivePivot);
        Assert.True(shellVm.IsDeviceActive);

        // Test navigate to Playlists
        shellVm.OpenPlaylistsCommand.Execute(null);
        Assert.Equal(NavigationPivot.Collection, shellVm.ActivePivot);
        Assert.Equal(CollectionSubPivot.Playlists, shellVm.CollectionVM.ActiveSubPivot);
        Assert.True(shellVm.CollectionVM.IsPlaylistsActive);
    }

    [Fact]
    public async Task PlaylistsViewModel_CreateSelectAndDelete()
    {
        var coordinator = new PlaybackQueueCoordinator();
        var lib = new TestMediaLibraryService();
        var vm = new PlaylistsViewModel(lib, coordinator);

        await vm.LoadPlaylistsAsync();
        Assert.Empty(vm.Playlists);

        // Create playlist
        vm.NewPlaylistName = "Chill Lounge";
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.CreatePlaylistCommand).ExecuteAsync(null);

        Assert.Single(vm.Playlists);
        Assert.Equal("Chill Lounge", vm.Playlists[0].Name);
        Assert.Equal(vm.Playlists[0], vm.SelectedPlaylist);

        // Delete playlist
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand<Playlist>)vm.DeletePlaylistCommand).ExecuteAsync(vm.SelectedPlaylist);
        Assert.Empty(vm.Playlists);
    }

    [Fact]
    public async Task MetadataEditViewModel_EditsAndSavesTrack()
    {
        var lib = new TestMediaLibraryService();
        var track = new Track
        {
            Id = Guid.NewGuid(),
            Title = "Original Song",
            ArtistName = "Original Band",
            AlbumTitle = "Original Record",
            Year = 2010,
            Genre = "Rock",
            TrackNumber = 1,
            DiscNumber = 1
        };

        bool closed = false;
        var vm = new MetadataEditViewModel(track, lib);
        vm.RequestClose += (s, e) => closed = true;

        // Check pre-populated fields
        Assert.Equal("Original Song", vm.Title);
        Assert.Equal("Original Band", vm.ArtistName);
        Assert.Equal("Original Record", vm.AlbumTitle);
        Assert.Equal(2010, vm.Year);
        Assert.Equal("Rock", vm.Genre);

        // Edit fields
        vm.Title = "Edited Song";
        vm.ArtistName = "Edited Band";
        vm.AlbumTitle = "Edited Record";
        vm.Year = 2023;
        vm.Genre = "Electronic";
        vm.TrackNumber = 5;
        vm.DiscNumber = 2;

        // Save
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.SaveCommand).ExecuteAsync(null);

        Assert.True(closed);
        Assert.NotNull(lib.LastUpdatedMetadata);
        Assert.Equal(track.Id, lib.LastUpdatedMetadata.Value.trackId);
        Assert.Equal("Edited Song", lib.LastUpdatedMetadata.Value.title);
        Assert.Equal("Edited Band", lib.LastUpdatedMetadata.Value.artist);
        Assert.Equal("Edited Record", lib.LastUpdatedMetadata.Value.album);
        Assert.Equal(2023, lib.LastUpdatedMetadata.Value.year);
        Assert.Equal("Electronic", lib.LastUpdatedMetadata.Value.genre);
        Assert.Equal(5, lib.LastUpdatedMetadata.Value.trackNum);
        Assert.Equal(2, lib.LastUpdatedMetadata.Value.discNum);
    }

    [Fact]
    public async Task QuickplayPinning_PinsAndUnpinsAlbum()
    {
        var lib = new TestMediaLibraryService();
        var album = new Album
        {
            Id = Guid.NewGuid(),
            Title = "Subdivisions",
            ArtistName = "Rush",
            IsPinned = false
        };
        lib.PinnedAlbums.Add(album);

        // Pin album
        await lib.PinAlbumAsync(album.Id);
        Assert.True(album.IsPinned);

        // Unpin album
        await lib.UnpinAlbumAsync(album.Id);
        Assert.False(album.IsPinned);
    }

    [Fact]
    public async Task SmartDJ_GeneratesMixFromAlbumSeed()
    {
        var engine = new Dorado.Application.Services.SmartDJEngine();
        var albumId = Guid.NewGuid();
        var artistId = Guid.NewGuid();

        var libraryTracks = new List<Track>
        {
            new Track { Id = Guid.NewGuid(), Title = "Track 1", AlbumId = albumId, ArtistId = artistId, Genre = "Prog Rock" },
            new Track { Id = Guid.NewGuid(), Title = "Track 2", AlbumId = albumId, ArtistId = artistId, Genre = "Prog Rock" },
            new Track { Id = Guid.NewGuid(), Title = "Track 3", AlbumId = Guid.NewGuid(), ArtistId = artistId, Genre = "Prog Rock" },
            new Track { Id = Guid.NewGuid(), Title = "Track 4", AlbumId = Guid.NewGuid(), ArtistId = Guid.NewGuid(), Genre = "Pop" }
        };

        var seed = new SmartDJSeed
        {
            SeedAlbumId = albumId,
            TargetTrackCount = 3,
            ExcludeDisliked = true
        };

        var mix = await engine.GenerateMixAsync(seed, libraryTracks);

        Assert.NotEmpty(mix);
        // The top candidate should match the album seed
        Assert.Equal(albumId, mix[0].AlbumId);
    }

    [Fact]
    public void NowPlaying_Showlist_TogglesAndUpdatesQueue()
    {
        var player = new PlaybackQueueCoordinator();
        var lib = new TestMediaLibraryService();
        var vm = new NowPlayingViewModel(player, lib);

        Assert.False(vm.IsShowlistOpen);
        Assert.Equal(0, vm.UpcomingQueueCount);

        // Enqueue tracks
        var t1 = new Track { Id = Guid.NewGuid(), Title = "Song A", ArtistName = "Artist A" };
        var t2 = new Track { Id = Guid.NewGuid(), Title = "Song B", ArtistName = "Artist B" };
        player.Enqueue(new[] { t1, t2 });

        // Toggle showlist
        vm.ToggleShowlistCommand.Execute(null);
        Assert.True(vm.IsShowlistOpen);
        Assert.Equal(2, vm.UpcomingQueueCount);
        Assert.Equal("Song A", vm.UpcomingQueue[0].Title);

        // Toggle closed
        vm.ToggleShowlistCommand.Execute(null);
        Assert.False(vm.IsShowlistOpen);
    }

    [Fact]
    public void Settings_ExpandedPreferences_UpdateState()
    {
        var vm = new SettingsViewModel();

        Assert.True(vm.VolumeLevelingEnabled);
        Assert.True(vm.CompactModeAlwaysOnTop);
        Assert.True(vm.AutoWatchFolder);

        vm.VolumeLevelingEnabled = false;
        Assert.False(vm.VolumeLevelingEnabled);

        vm.CompactModeAlwaysOnTop = false;
        Assert.False(vm.CompactModeAlwaysOnTop);

        vm.AutoWatchFolder = false;
        Assert.False(vm.AutoWatchFolder);
    }
}

