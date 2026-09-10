namespace Dorado.Application.Interfaces;

/// <summary>
/// Real video playback boundary (libVLC-backed). The Avalonia VideoView binds to
/// <see cref="MediaPlayerHandle"/>; all operations no-op safely when unavailable.
/// </summary>
public interface IVideoPlaybackEngine : IDisposable
{
    bool IsAvailable { get; }

    object? MediaPlayerHandle { get; }

    void LoadAndPlay(string filePath);

    void Play();

    void Pause();

    void Stop();

    void SetVolume(double volume, bool muted);

    void Seek(TimeSpan position);

    TimeSpan GetPosition();

    TimeSpan GetDuration();

    event EventHandler? PlaybackEnded;
}
