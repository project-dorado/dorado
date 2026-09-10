using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Dorado.Application.Interfaces;
using Dorado.Application.Services;
using Dorado.Domain.Models;
using Dorado.Infrastructure.Persistence;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Phase 8 parity: video/photo libraries, playback view models, and the slideshow.
/// </summary>
public class VideoPhotoParityTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dbPath;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public VideoPhotoParityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dorado-video-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "test.db");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_dbPath}").Options;
        _dbFactory = new TestDbContextFactory(options);
        using var ctx = _dbFactory.CreateDbContext();
        ctx.Database.EnsureCreated();
    }

    public void Dispose()
    {
        // Windows keeps pooled SQLite connections open, which would lock test.db.
        SqliteConnection.ClearAllPools();

        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup; a lingering temp dir must not fail the test run.
        }
    }

    private sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;
        public TestDbContextFactory(DbContextOptions<AppDbContext> options) => _options = options;
        public AppDbContext CreateDbContext() => new(_options);
        public async Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => await Task.FromResult(new AppDbContext(_options));
    }

    [Fact]
    public async Task VideoLibrary_Scan_PersistsVideosWithDurationGraceful()
    {
        var fakeVideo = Path.Combine(_tempDir, "Sample Movie.mp4");
        File.WriteAllBytes(fakeVideo, new byte[] { 0x00, 0x00, 0x00, 0x18 }); // container bytes; TagLib will fail gracefully
        var service = new VideoLibraryService(_dbFactory);

        await service.ScanDirectoryAsync(_tempDir);
        var videos = await service.GetAllVideosAsync();

        var video = Assert.Single(videos);
        Assert.Equal("Sample Movie", video.Title);
        Assert.Equal(fakeVideo, video.FilePath);
    }

    [Fact]
    public async Task VideoLibrary_MarkPlayed_IncrementsCount()
    {
        var fakeVideo = Path.Combine(_tempDir, "clip.mp4");
        File.WriteAllBytes(fakeVideo, new byte[] { 0x01 });
        var service = new VideoLibraryService(_dbFactory);
        await service.ScanDirectoryAsync(_tempDir);
        var video = (await service.GetAllVideosAsync()).Single();

        await service.MarkPlayedAsync(video.Id);
        await service.MarkPlayedAsync(video.Id);

        var updated = (await service.GetAllVideosAsync()).Single();
        Assert.Equal(2, updated.PlayCount);
        Assert.NotNull(updated.LastPlayedAtUtc);
    }

    [Fact]
    public async Task PhotoLibrary_Scan_PersistsPhotosAndFolders()
    {
        var subDir = Path.Combine(_tempDir, "Vacation");
        Directory.CreateDirectory(subDir);
        File.WriteAllBytes(Path.Combine(subDir, "beach.jpg"), new byte[] { 0xFF, 0xD8 });
        File.WriteAllBytes(Path.Combine(subDir, "sunset.png"), new byte[] { 0x89, 0x50 });

        var service = new PhotoLibraryService(_dbFactory);
        await service.ScanDirectoryAsync(_tempDir);

        var photos = await service.GetAllPhotosAsync();
        Assert.Equal(2, photos.Count);
        Assert.All(photos, p => Assert.Equal(subDir, p.FolderPath));

        var folders = await service.GetFoldersAsync();
        Assert.Single(folders);
        Assert.Equal(subDir, folders[0]);
    }

    [Fact]
    public async Task PhotoLibrary_RescanSkipsDuplicates()
    {
        File.WriteAllBytes(Path.Combine(_tempDir, "a.jpg"), new byte[] { 0xFF, 0xD8 });
        var service = new PhotoLibraryService(_dbFactory);

        await service.ScanDirectoryAsync(_tempDir);
        await service.ScanDirectoryAsync(_tempDir);

        var photos = await service.GetAllPhotosAsync();
        Assert.Single(photos);
    }

    [Fact]
    public void VideoPlayback_UnavailableEngine_ShowsGuidanceAndSafeOps()
    {
        var vm = new VideoPlaybackViewModel(new UnavailableVideoPlaybackEngine(), new Video { Title = "Test", FilePath = "/x.mp4" });

        vm.Start();

        Assert.False(vm.IsVideoAvailable);
        Assert.Contains("libvlc", vm.UnavailableText);
        Assert.Equal(TimeSpan.Zero, vm.Position);
        Assert.Equal(TimeSpan.Zero, vm.Duration);
        Assert.Equal(0.0, vm.SeekFraction);
    }

    [Fact]
    public async Task PhotoLibraryVM_FolderFilter_AndStats()
    {
        var subDir = Path.Combine(_tempDir, "Trip");
        Directory.CreateDirectory(subDir);
        File.WriteAllBytes(Path.Combine(subDir, "p1.jpg"), new byte[] { 0xFF, 0xD8 });
        File.WriteAllBytes(Path.Combine(_tempDir, "p2.jpg"), new byte[] { 0xFF, 0xD8 });

        var service = new PhotoLibraryService(_dbFactory);
        await service.ScanDirectoryAsync(_tempDir);
        var vm = new PhotoLibraryViewModel(service);
        await vm.LoadPhotosAsync();

        Assert.Equal(2, vm.GalleryPhotos.Count); // All Photos default

        vm.SelectedFolder = subDir;
        Assert.Single(vm.GalleryPhotos);
        Assert.Equal("Trip", vm.SelectedFolderName);
        Assert.False(vm.HasNoPhotos);

        vm.SelectedFolder = PhotoLibraryViewModel.AllFoldersEntry;
        Assert.Equal(2, vm.GalleryPhotos.Count);
    }

    [Fact]
    public void PhotoSlideshow_CyclesAndPauses()
    {
        var photos = new ObservableCollection<Photo>(
            new[] { "a.jpg", "b.jpg", "c.jpg" }.Select(f => new Photo { Title = f, FilePath = f }));
        var vm = new PhotoSlideshowViewModel(photos, 0);

        Assert.Equal("a.jpg", vm.CurrentPhoto!.Title);

        ((RelayCommand)vm.NextCommand).Execute(null);
        Assert.Equal("b.jpg", vm.CurrentPhoto!.Title);

        ((RelayCommand)vm.BackCommand).Execute(null);
        Assert.Equal("a.jpg", vm.CurrentPhoto!.Title);

        Assert.True(vm.IsPlaying);
        ((RelayCommand)vm.PlayPauseCommand).Execute(null);
        Assert.False(vm.IsPlaying);
        Assert.Equal("PLAY", vm.PlayPauseIcon);
    }

    [Fact]
    public void VideoLibraryVM_EmptyLibrary_ShowsEmptyState()
    {
        var vm = new VideoLibraryViewModel(new EmptyVideoLibraryService(), new UnavailableVideoPlaybackEngine());

        Assert.False(vm.HasVideos);
        Assert.False(vm.IsPlayerOpen);
    }

    [Fact]
    public void VideoEngine_InitializesOrDegradesGracefully()
    {
        // Exercises the full libVLC native path: either a real VLC installation initializes
        // (Windows natives from the NuGet package, Linux system libvlc via the resolver),
        // or the engine degrades to unavailable (headless/CI without VLC).
        using var engine = new Dorado.Infrastructure.Video.VideoPlaybackEngine();

        engine.LoadAndPlay("/nonexistent/video.mp4");

        if (engine.IsAvailable)
        {
            Assert.Equal(TimeSpan.Zero, engine.GetPosition());
            Assert.NotNull(engine.MediaPlayerHandle);
        }
        else
        {
            Assert.Null(engine.MediaPlayerHandle);
        }

        engine.SetVolume(0.5, muted: false);
        engine.Seek(TimeSpan.FromSeconds(1));
        engine.Pause();
        engine.Play();
        engine.Stop();
    }
}
