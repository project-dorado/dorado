using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;
using Dorado.Infrastructure.Persistence;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Phase 7 parity: first-launch wizard, What's New gating, and the sync instruction toast.
/// </summary>
public class OnboardingShellParityTests : IDisposable
{
    private readonly string _tempDir;
    private readonly JsonSettingsStore _settingsStore;

    public OnboardingShellParityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dorado-onboard-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _settingsStore = new JsonSettingsStore(Path.Combine(_tempDir, "settings.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private sealed class SeededLibrary : IMediaLibraryService
    {
        public List<Track> Tracks { get; } = new()
        {
            new Track { Title = "Subdivisions", ArtistName = "Rush", Genre = "Progressive Rock" },
            new Track { Title = "One More Time", ArtistName = "Daft Punk", Genre = "Electronic" }
        };

        public Task<IReadOnlyList<Track>> GetAllTracksAsync() => Task.FromResult<IReadOnlyList<Track>>(Tracks);
        public Task<IReadOnlyList<Album>> GetAllAlbumsAsync() => Task.FromResult<IReadOnlyList<Album>>(new List<Album>());
        public Task<IReadOnlyList<Artist>> GetAllArtistsAsync() => Task.FromResult<IReadOnlyList<Artist>>(new List<Artist>());
        public Task<IReadOnlyList<Playlist>> GetAllPlaylistsAsync() => Task.FromResult<IReadOnlyList<Playlist>>(new List<Playlist>());
        public Task<IReadOnlyList<PlayHistoryEntry>> GetRecentHistoryAsync(int count = 20) => Task.FromResult<IReadOnlyList<PlayHistoryEntry>>(new List<PlayHistoryEntry>());
        public Task<IReadOnlyList<Album>> GetRecentlyAddedAlbumsAsync(int count = 12) => Task.FromResult<IReadOnlyList<Album>>(new List<Album>());
        public Task<IReadOnlyList<Track>> SearchAsync(string query) => Task.FromResult<IReadOnlyList<Track>>(new List<Track>());
        public Task SetTrackRatingAsync(Guid trackId, HeartRating rating) => Task.CompletedTask;
        public Task ScanDirectoryAsync(string directoryPath, IProgress<double>? progress = null)
        {
            progress?.Report(1.0);
            return Task.CompletedTask;
        }

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

    private sealed class TempFolderPicker : IFolderPickerService
    {
        private readonly string _directory;
        public TempFolderPicker(string directory) => _directory = directory;
        public Task<string?> PickFolderAsync(string title = "Select Music Collection Folder") => Task.FromResult<string?>(_directory);
        public Task<string?> PickFileAsync(string title = "Select File", string extension = "*.*") => Task.FromResult<string?>(null);
    }

    [Fact]
    public void Wizard_StartsAtWelcome_AdvancesToChooseFolder()
    {
        var wizard = new FirstLaunchWizardViewModel(new SeededLibrary(), new TempFolderPicker(_tempDir), _settingsStore);

        Assert.True(wizard.IsWelcomeStep);

        wizard.NextCommand.Execute(null);

        Assert.True(wizard.IsChooseFolderStep);
    }

    [Fact]
    public async Task Wizard_InvalidFolder_IsRejected()
    {
        var wizard = new FirstLaunchWizardViewModel(new SeededLibrary(), new TempFolderPicker(_tempDir), _settingsStore);
        wizard.NextCommand.Execute(null);
        wizard.FolderPath = "/nonexistent/folder/path";

        await ((AsyncRelayCommand)wizard.NextCommand).ExecuteAsync(null);

        Assert.True(wizard.IsChooseFolderStep);
        Assert.Contains("valid music folder", wizard.ScanStatusText);
    }

    [Fact]
    public async Task Wizard_ValidFolder_ScansCountsAndCompletes()
    {
        var library = new SeededLibrary();
        var wizard = new FirstLaunchWizardViewModel(library, new TempFolderPicker(_tempDir), _settingsStore);
        wizard.NextCommand.Execute(null);
        wizard.FolderPath = _tempDir;

        await ((AsyncRelayCommand)wizard.NextCommand).ExecuteAsync(null);

        Assert.True(wizard.IsDoneStep);
        Assert.Equal(2, wizard.DiscoveredCount);
        Assert.Equal(1.0, wizard.ScanProgress);
    }

    [Fact]
    public async Task Wizard_Completion_PersistsFirstLaunchFlagAndFolder()
    {
        var wizard = new FirstLaunchWizardViewModel(new SeededLibrary(), new TempFolderPicker(_tempDir), _settingsStore);
        var closed = false;
        wizard.RequestClose += (_, _) => closed = true;

        wizard.NextCommand.Execute(null);
        wizard.FolderPath = _tempDir;
        await ((AsyncRelayCommand)wizard.NextCommand).ExecuteAsync(null);

        wizard.FinishCommand.Execute(null);

        Assert.True(closed, "Finish should raise RequestClose");
        var settings = _settingsStore.Load();
        Assert.True(settings.FirstLaunchCompleted);
        Assert.Equal(_tempDir, settings.MusicFolderPath);
    }

    [Fact]
    public void Wizard_Skip_MarksFirstLaunchCompleted()
    {
        var wizard = new FirstLaunchWizardViewModel(new SeededLibrary(), new TempFolderPicker(_tempDir), _settingsStore);

        wizard.SkipCommand.Execute(null);

        var settings = _settingsStore.Load();
        Assert.True(settings.FirstLaunchCompleted);
    }

    [Fact]
    public void WhatsNew_Continue_PersistsSeenVersion()
    {
        var vm = new WhatsNewViewModel(_settingsStore);

        Assert.Equal(Dorado.Application.AppInfo.VersionDisplay, vm.VersionDisplay);
        Assert.NotEmpty(vm.Highlights);

        vm.ContinueCommand.Execute(null);

        Assert.Equal(Dorado.Application.AppInfo.Version, _settingsStore.Load().WhatsNewSeenVersion);
    }

    [Fact]
    public void Settings_FirstLaunchFlag_RoundTripsThroughStore()
    {
        var settingsVm = new SettingsViewModel(settingsStore: _settingsStore);

        settingsVm.FirstLaunchCompleted = true;

        Assert.True(_settingsStore.Load().FirstLaunchCompleted);
        Assert.True(settingsVm.FirstLaunchCompleted);
    }
}
