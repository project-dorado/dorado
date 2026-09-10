using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Interfaces;
using Dorado.Application.Services;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Phase 6 parity: smart playlists, global back-stack navigation, and Mixview action tiles.
/// </summary>
public class CollectionParityTests
{
    // ==========================================
    // SMART PLAYLIST RULES
    // ==========================================
    private static List<Track> Library() => new()
    {
        new Track { Title = "Subdivisions", ArtistName = "Rush", AlbumTitle = "Signals", Genre = "Progressive Rock", Year = 1982, Rating = HeartRating.Favorite, PlayCount = 9, LastPlayedAtUtc = DateTime.UtcNow.AddDays(-1), Duration = TimeSpan.FromSeconds(334) },
        new Track { Title = "Time", ArtistName = "Pink Floyd", AlbumTitle = "The Dark Side of the Moon", Genre = "Progressive Rock", Year = 1973, Rating = HeartRating.None, PlayCount = 3, LastPlayedAtUtc = DateTime.UtcNow.AddDays(-40), Duration = TimeSpan.FromSeconds(413) },
        new Track { Title = "One More Time", ArtistName = "Daft Punk", AlbumTitle = "Discovery", Genre = "Electronic", Year = 2001, Rating = HeartRating.Favorite, PlayCount = 12, LastPlayedAtUtc = DateTime.UtcNow.AddHours(-2), Duration = TimeSpan.FromSeconds(320) },
        new Track { Title = "Dreams", ArtistName = "Fleetwood Mac", AlbumTitle = "Rumours", Genre = "Classic Rock", Year = 1977, Rating = HeartRating.Dislike, PlayCount = 1, LastPlayedAtUtc = null, Duration = TimeSpan.FromSeconds(257) }
    };

    private static SmartPlaylist MakePlaylist(Action<SmartPlaylist>? configure = null)
    {
        var playlist = new SmartPlaylist { Name = "Test", Match = SmartPlaylistMatch.All, TrackLimit = 0, SortField = "Title" };
        configure?.Invoke(playlist);
        return playlist;
    }

    [Fact]
    public void SmartRules_GenreContains_MatchesAll()
    {
        var playlist = MakePlaylist(p => p.Rules.Add(new SmartPlaylistRule { Field = SmartRuleField.Genre, Operator = SmartRuleOperator.Contains, Value = "rock" }));

        var result = SmartPlaylistRules.Evaluate(playlist, Library());

        Assert.Equal(3, result.Count); // 2x Progressive Rock + Classic Rock all contain "rock"
    }

    [Fact]
    public void SmartRules_GenreIs_MatchesExactly()
    {
        var playlist = MakePlaylist(p => p.Rules.Add(new SmartPlaylistRule { Field = SmartRuleField.Genre, Operator = SmartRuleOperator.Is, Value = "Electronic" }));

        var result = SmartPlaylistRules.Evaluate(playlist, Library());

        Assert.Single(result);
        Assert.Equal("One More Time", result[0].Title);
    }

    [Fact]
    public void SmartRules_MatchAny_UnionsRuleResults()
    {
        var playlist = MakePlaylist(p =>
        {
            p.Match = SmartPlaylistMatch.Any;
            p.Rules.Add(new SmartPlaylistRule { Field = SmartRuleField.Artist, Operator = SmartRuleOperator.Is, Value = "Rush" });
            p.Rules.Add(new SmartPlaylistRule { Field = SmartRuleField.Artist, Operator = SmartRuleOperator.Is, Value = "Daft Punk" });
        });

        var result = SmartPlaylistRules.Evaluate(playlist, Library());

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void SmartRules_FavoriteRating_Filters()
    {
        var playlist = MakePlaylist(p => p.Rules.Add(new SmartPlaylistRule { Field = SmartRuleField.Rating, Operator = SmartRuleOperator.Is, Value = "1" }));

        var result = SmartPlaylistRules.Evaluate(playlist, Library());

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, t => t.Rating == HeartRating.Dislike);
    }

    [Fact]
    public void SmartRules_PlayCountGreaterThan_Filters()
    {
        var playlist = MakePlaylist(p => p.Rules.Add(new SmartPlaylistRule { Field = SmartRuleField.PlayCount, Operator = SmartRuleOperator.GreaterThan, Value = "5" }));

        var result = SmartPlaylistRules.Evaluate(playlist, Library());

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void SmartRules_LastPlayedWithinDays_Filters()
    {
        var playlist = MakePlaylist(p => p.Rules.Add(new SmartPlaylistRule { Field = SmartRuleField.LastPlayed, Operator = SmartRuleOperator.WithinLastDays, Value = "7" }));

        var result = SmartPlaylistRules.Evaluate(playlist, Library());

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void SmartRules_SortAndLimit()
    {
        var playlist = MakePlaylist(p =>
        {
            p.SortField = "PlayCount";
            p.SortDescending = true;
            p.TrackLimit = 2;
        });

        var result = SmartPlaylistRules.Evaluate(playlist, Library());

        Assert.Equal(2, result.Count);
        Assert.Equal("One More Time", result[0].Title);
        Assert.Equal("Subdivisions", result[1].Title);
    }

    [Fact]
    public void SmartRules_NoRules_MatchesEverything()
    {
        var result = SmartPlaylistRules.Evaluate(MakePlaylist(), Library());

        Assert.Equal(4, result.Count);
    }

    // ==========================================
    // GLOBAL BACK-STACK NAVIGATION (PAGESTACK parity)
    // ==========================================
    private sealed class EmptyLibrary : IMediaLibraryService
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
        public event EventHandler? LibraryUpdated { add { } remove { } }
    }

    private sealed class EmptyDeviceService : IDeviceSyncService
    {
        public IReadOnlyList<ZuneDevice> ConnectedDevices => new List<ZuneDevice>();
        public Task StartMonitoringAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SyncDeviceAsync(string serialNumber, IProgress<double>? progress = null) => Task.CompletedTask;
        public event EventHandler<ZuneDevice>? DeviceConnected { add { } remove { } }
        public event EventHandler<string>? DeviceDisconnected { add { } remove { } }
    }

    [Fact]
    public void BackStack_NavigatesPivots_CanGoBackAndReturn()
    {
        var shell = new MainShellViewModel(
            new PlaybackQueueCoordinator(),
            new EmptyLibrary(),
            new EmptyDeviceService(),
            new SmartDJEngine());

        Assert.False(shell.CanGoBack);

        shell.ActivePivot = NavigationPivot.Collection;
        Assert.True(shell.CanGoBack);

        shell.ActivePivot = NavigationPivot.Device;
        shell.ActivePivot = NavigationPivot.Settings;

        shell.GoBack();
        Assert.Equal(NavigationPivot.Device, shell.ActivePivot);

        shell.GoBack();
        Assert.Equal(NavigationPivot.Collection, shell.ActivePivot);

        shell.GoBack();
        Assert.Equal(NavigationPivot.Quickplay, shell.ActivePivot);
        Assert.False(shell.CanGoBack);
    }

    [Fact]
    public void BackStack_GoBackViaCommand_Works()
    {
        var shell = new MainShellViewModel(
            new PlaybackQueueCoordinator(),
            new EmptyLibrary(),
            new EmptyDeviceService(),
            new SmartDJEngine());

        shell.ActivePivot = NavigationPivot.Social;
        Assert.True(shell.CanGoBack);

        shell.GoBackCommand.Execute(null);
        Assert.Equal(NavigationPivot.Quickplay, shell.ActivePivot);
    }

    // ==========================================
    // MIXVIEW ACTION TILES
    // ==========================================
    private sealed class RecordingLibrary : IMediaLibraryService
    {
        public List<Track> Tracks { get; } = Library();
        public List<(Guid TrackId, HeartRating Rating)> RatedTracks { get; } = new();
        public List<Guid> TrackUpdates { get; } = new();

        public Task<IReadOnlyList<Track>> GetAllTracksAsync() => Task.FromResult<IReadOnlyList<Track>>(Tracks);
        public Task<IReadOnlyList<Album>> GetAllAlbumsAsync() => Task.FromResult<IReadOnlyList<Album>>(new List<Album>());
        public Task<IReadOnlyList<Artist>> GetAllArtistsAsync() => Task.FromResult<IReadOnlyList<Artist>>(
            Tracks.Select(t => new Artist { Name = t.ArtistName }).DistinctBy(a => a.Name).ToList());
        public Task<IReadOnlyList<Playlist>> GetAllPlaylistsAsync() => Task.FromResult<IReadOnlyList<Playlist>>(new List<Playlist>());
        public Task<IReadOnlyList<PlayHistoryEntry>> GetRecentHistoryAsync(int count = 20) => Task.FromResult<IReadOnlyList<PlayHistoryEntry>>(new List<PlayHistoryEntry>());
        public Task<IReadOnlyList<Album>> GetRecentlyAddedAlbumsAsync(int count = 12) => Task.FromResult<IReadOnlyList<Album>>(new List<Album>());
        public Task<IReadOnlyList<Track>> SearchAsync(string query) => Task.FromResult<IReadOnlyList<Track>>(new List<Track>());
        public Task SetTrackRatingAsync(Guid trackId, HeartRating rating)
        {
            RatedTracks.Add((trackId, rating));
            return Task.CompletedTask;
        }

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
    public async Task MixviewTiles_LikeNode_RatesRepresentativeTrack()
    {
        var library = new RecordingLibrary();
        var coordinator = new PlaybackQueueCoordinator();
        var mixService = new MixviewCoordinator(library);
        var vm = new MixviewViewModel(mixService, coordinator, new SmartDJEngine(), library);

        var rushTrack = library.Tracks.First(t => t.ArtistName == "Rush");
        var node = new MixNode { Title = "Subdivisions", Subtitle = "TRACK", NodeType = MixNodeType.Track, EntityId = rushTrack.Id };

        await ((AsyncRelayCommand<MixNode>)vm.LikeNodeCommand).ExecuteAsync(node);

        var rated = Assert.Single(library.RatedTracks);
        Assert.Equal(rushTrack.Id, rated.TrackId);
        Assert.Equal(HeartRating.Favorite, rated.Rating);
    }

    [Fact]
    public async Task MixviewTiles_InfoNode_ShowsTrackCard()
    {
        var library = new RecordingLibrary();
        var vm = new MixviewViewModel(new MixviewCoordinator(library), new PlaybackQueueCoordinator(), new SmartDJEngine(), library);

        var rushTrack = library.Tracks.First(t => t.ArtistName == "Rush");
        await ((AsyncRelayCommand<MixNode>)vm.InfoNodeCommand).ExecuteAsync(new MixNode { Title = "Subdivisions", Subtitle = "TRACK", NodeType = MixNodeType.Track, EntityId = rushTrack.Id });

        Assert.True(vm.HasInfoCard);
        Assert.Contains("Signals", vm.InfoCardText);
    }

    [Fact]
    public async Task MixviewTiles_AddTrackNode_EnqueuesTrack()
    {
        var library = new RecordingLibrary();
        var coordinator = new PlaybackQueueCoordinator();
        var vm = new MixviewViewModel(new MixviewCoordinator(library), coordinator, new SmartDJEngine(), library);

        var rushTrack = library.Tracks.First(t => t.ArtistName == "Rush");
        await ((AsyncRelayCommand<MixNode>)vm.AddNodeCommand).ExecuteAsync(new MixNode { Title = "Subdivisions", Subtitle = "TRACK", NodeType = MixNodeType.Track, EntityId = rushTrack.Id });

        Assert.Single(coordinator.Queue);
        Assert.Equal(rushTrack.Id, coordinator.Queue[0].Id);
    }

    // ==========================================
    // MIXVIEW GENRE-AFFINITY SATELLITES
    // ==========================================
    [Fact]
    public async Task MixviewCoordinator_GenreAffinity_RanksSharedGenresFirst()
    {
        var library = new RecordingLibrary();
        var coordinator = new MixviewCoordinator(library);

        var constellation = await coordinator.GenerateConstellationAsync("Rush", MixNodeType.Artist);

        var artistSatellites = constellation.Satellites.Where(s => s.NodeType == MixNodeType.Artist).ToList();
        // Pink Floyd (progressive rock) should outrank Daft Punk (electronic) via genre affinity.
        var pinkFloydIndex = artistSatellites.FindIndex(s => s.Title == "Pink Floyd");
        var daftPunkIndex = artistSatellites.FindIndex(s => s.Title == "Daft Punk");
        Assert.True(pinkFloydIndex >= 0, "Pink Floyd satellite missing");
        if (daftPunkIndex >= 0)
        {
            Assert.True(pinkFloydIndex < daftPunkIndex, "Genre affinity should rank Pink Floyd before Daft Punk");
        }
    }
}
