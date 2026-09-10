using System;
using System.Timers;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Interfaces;
using Dorado.Domain.Models;

namespace Dorado.UI.ViewModels;

/// <summary>
/// Hosts one playing video (NOWPLAYINGCLIPS / video library playback parity).
/// The Avalonia VideoView binds to <see cref="MediaPlayerHandle"/>.
/// </summary>
public class VideoPlaybackViewModel : ViewModelBase
{
    private readonly IVideoPlaybackEngine _engine;
    private readonly Video? _video;
    private readonly System.Timers.Timer _positionTimer;
    private bool _isPlaying;
    private string? _statusText;

    public event EventHandler? RequestClose;

    public VideoPlaybackViewModel(IVideoPlaybackEngine engine, Video? video)
    {
        _engine = engine;
        _video = video;

        _positionTimer = new System.Timers.Timer(500) { AutoReset = true };
        _positionTimer.Elapsed += (_, _) => NotifyTimeProperties();
        _positionTimer.Start();

        PlayPauseCommand = new RelayCommand(OnPlayPause);
        StopCommand = new RelayCommand(OnStop);
        CloseCommand = new RelayCommand(OnClose);
        SeekCommand = new RelayCommand<double>(OnSeek);
    }

    /// <summary>The live LibVLCSharp.MediaPlayer for the VideoView surface.</summary>
    public object? MediaPlayerHandle => _engine.MediaPlayerHandle;

    public string VideoTitle => _video?.Title ?? "Video";

    public bool IsVideoAvailable => _engine.IsAvailable && _engine.MediaPlayerHandle != null;

    public string UnavailableText => "Video playback is unavailable on this system. Install VLC (libvlc) and restart Dorado to enable video.";

    public string? StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                OnPropertyChanged(nameof(PlayPauseIcon));
            }
        }
    }

    public string PlayPauseIcon => IsPlaying ? "⏸" : "▶";

    public TimeSpan Position => _engine.GetPosition();

    public TimeSpan Duration
    {
        get
        {
            var engineDuration = _engine.GetDuration();
            if (engineDuration > TimeSpan.Zero)
            {
                return engineDuration;
            }

            return _video?.Duration ?? TimeSpan.Zero;
        }
    }

    public double ProgressFraction
    {
        get
        {
            var total = Duration.TotalMilliseconds;
            return total <= 0 ? 0 : Math.Clamp(Position.TotalMilliseconds / total, 0, 1);
        }
    }

    /// <summary>Two-way scrub target for the seek slider.</summary>
    public double SeekFraction
    {
        get => ProgressFraction;
        set => OnSeek(Math.Clamp(value, 0, 1));
    }

    public string TimeText => $"{Position:hh\\:mm\\:ss} / {Duration:hh\\:mm\\:ss}";

    public ICommand PlayPauseCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand SeekCommand { get; }

    public void Start()
    {
        if (_video == null || !IsVideoAvailable)
        {
            StatusText = IsVideoAvailable ? null : UnavailableText;
            return;
        }

        _engine.LoadAndPlay(_video.FilePath);
        _engine.SetVolume(0.8, muted: false);
        IsPlaying = true;
        NotifyTimeProperties();
    }

    private void OnPlayPause()
    {
        if (IsPlaying)
        {
            _engine.Pause();
            IsPlaying = false;
        }
        else
        {
            _engine.Play();
            IsPlaying = true;
        }
    }

    private void OnStop()
    {
        _engine.Stop();
        IsPlaying = false;
        NotifyTimeProperties();
    }

    private void OnClose()
    {
        _engine.Stop();
        _positionTimer.Stop();
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private void OnSeek(double fraction)
    {
        var total = Duration.TotalMilliseconds;
        if (total <= 0)
        {
            return;
        }

        _engine.Seek(TimeSpan.FromMilliseconds(fraction * total));
    }

    private void NotifyTimeProperties()
    {
        OnPropertyChanged(nameof(Position));
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(ProgressFraction));
        OnPropertyChanged(nameof(TimeText));
        if (IsPlaying && _engine.GetDuration() > TimeSpan.Zero && _engine.GetPosition() >= _engine.GetDuration().Subtract(TimeSpan.FromMilliseconds(250)))
        {
            IsPlaying = false;
        }
    }
}
