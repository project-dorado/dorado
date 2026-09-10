using Dorado.Application.Interfaces;
using Dorado.Domain.Models;

namespace Dorado.Application.Services;

/// <summary>Default no-op video library used when the persistence layer is not wired (e.g. unit tests).</summary>
public sealed class EmptyVideoLibraryService : IVideoLibraryService
{
    public Task<IReadOnlyList<Video>> GetAllVideosAsync() => Task.FromResult<IReadOnlyList<Video>>(new List<Video>());
    public Task ScanDirectoryAsync(string directoryPath, IProgress<double>? progress = null) => Task.CompletedTask;
    public Task MarkPlayedAsync(Guid videoId) => Task.CompletedTask;
}

/// <summary>Default no-op photo library used when the persistence layer is not wired (e.g. unit tests).</summary>
public sealed class EmptyPhotoLibraryService : IPhotoLibraryService
{
    public Task<IReadOnlyList<Photo>> GetAllPhotosAsync() => Task.FromResult<IReadOnlyList<Photo>>(new List<Photo>());
    public Task ScanDirectoryAsync(string directoryPath, IProgress<double>? progress = null) => Task.CompletedTask;
    public Task<IReadOnlyList<string>> GetFoldersAsync() => Task.FromResult<IReadOnlyList<string>>(new List<string>());
}

/// <summary>Default unavailable video engine (no libVLC) — all operations are safe no-ops.</summary>
public sealed class UnavailableVideoPlaybackEngine : IVideoPlaybackEngine
{
    public bool IsAvailable => false;
    public object? MediaPlayerHandle => null;
    public void LoadAndPlay(string filePath) { }
    public void Play() { }
    public void Pause() { }
    public void Stop() { }
    public void SetVolume(double volume, bool muted) { }
    public void Seek(TimeSpan position) { }
    public TimeSpan GetPosition() => TimeSpan.Zero;
    public TimeSpan GetDuration() => TimeSpan.Zero;
    public event EventHandler? PlaybackEnded { add { } remove { } }
    public void Dispose() { }
}
