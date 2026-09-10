using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using Dorado.Application.Interfaces;

namespace Dorado.Infrastructure.Video;

/// <summary>
/// Real video playback engine built on libVLC (VLC 3.x).
/// Natives: bundled via VideoLAN.LibVLC.Windows on Windows; resolved from the system
/// installation on Linux via <see cref="VideoNativeResolver"/>.
/// Degrades gracefully to <see cref="IsAvailable"/> = false when libVLC is missing.
/// </summary>
public sealed class VideoPlaybackEngine : IVideoPlaybackEngine
{
    private static bool _resolverRegistered;

    private readonly object _gate = new();
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private bool _initialized;
    private bool _disposed;

    public bool IsAvailable { get; private set; }

    /// <summary>The LibVLCSharp MediaPlayer, for Avalonia VideoView binding. Null when unavailable.</summary>
    public object? MediaPlayerHandle => IsAvailable ? _mediaPlayer : null;

    public event EventHandler? PlaybackEnded;

    public VideoPlaybackEngine()
    {
        RegisterResolver();
    }

    private static void RegisterResolver()
    {
        if (_resolverRegistered)
        {
            return;
        }

        _resolverRegistered = true;
        VideoNativeResolver.Register();
    }

    private bool EnsureInitialized()
    {
        if (_initialized)
        {
            return IsAvailable;
        }

        _initialized = true;
        try
        {
            _libVlc = new LibVLC(enableDebugLogs: false, "--no-video-title-show", "--no-snapshot-preview");
            _mediaPlayer = new MediaPlayer(_libVlc);
            _mediaPlayer.EndReached += (_, _) => PlaybackEnded?.Invoke(this, EventArgs.Empty);
            IsAvailable = true;
        }
        catch (Exception)
        {
            // libVLC natives missing (e.g. Linux without VLC installed) — degrade silently.
            IsAvailable = false;
            _libVlc = null;
            _mediaPlayer = null;
        }

        return IsAvailable;
    }

    public void LoadAndPlay(string filePath)
    {
        if (!EnsureInitialized() || _mediaPlayer == null || _libVlc == null || string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        lock (_gate)
        {
            // Windows paths (including rooted-but-not-drive-qualified ones like /x/y.mp4)
            // are not valid absolute URIs; resolve to a full path first (nonexistent
            // files still play-fail gracefully inside libVLC).
            using var media = new Media(_libVlc, new Uri(Path.GetFullPath(filePath)));
            _mediaPlayer.Play(media);
        }
    }

    public void Play()
    {
        if (!IsAvailable || _mediaPlayer == null)
        {
            return;
        }

        lock (_gate)
        {
            _mediaPlayer.Play();
        }
    }

    public void Pause()
    {
        if (!IsAvailable || _mediaPlayer == null)
        {
            return;
        }

        lock (_gate)
        {
            _mediaPlayer.Pause();
        }
    }

    public void Stop()
    {
        if (!IsAvailable || _mediaPlayer == null)
        {
            return;
        }

        lock (_gate)
        {
            _mediaPlayer.Stop();
        }
    }

    public void SetVolume(double volume, bool muted)
    {
        if (!IsAvailable || _mediaPlayer == null)
        {
            return;
        }

        lock (_gate)
        {
            _mediaPlayer.Volume = muted ? 0 : (int)Math.Clamp(volume * 100, 0, 100);
        }
    }

    public void Seek(TimeSpan position)
    {
        if (!IsAvailable || _mediaPlayer == null)
        {
            return;
        }

        lock (_gate)
        {
            if (_mediaPlayer.Time >= 0)
            {
                _mediaPlayer.Time = (long)Math.Max(0, position.TotalMilliseconds);
            }
        }
    }

    public TimeSpan GetPosition()
    {
        if (!IsAvailable || _mediaPlayer == null)
        {
            return TimeSpan.Zero;
        }

        lock (_gate)
        {
            var ms = _mediaPlayer.Time;
            return ms < 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(ms);
        }
    }

    public TimeSpan GetDuration()
    {
        if (!IsAvailable || _mediaPlayer == null)
        {
            return TimeSpan.Zero;
        }

        lock (_gate)
        {
            var ms = _mediaPlayer.Length;
            return ms <= 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(ms);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            lock (_gate)
            {
                _mediaPlayer?.Stop();
                _mediaPlayer?.Dispose();
                _mediaPlayer = null;
                _libVlc?.Dispose();
                _libVlc = null;
            }
        }
        catch
        {
        }

        GC.SuppressFinalize(this);
    }
}
