using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Timers;
using System.Windows.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Events;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;

namespace Dorado.UI.ViewModels;

public enum NowPlayingMode
{
    ArtistCanvas,
    MosaicWall,
    Video
}

public class NowPlayingViewModel : ViewModelBase
{
    private readonly IPlayerCoordinator _playerCoordinator;
    private readonly IMediaLibraryService _libraryService;
    private readonly IArtistEnrichmentService? _enrichmentService;
    private readonly IAudioOutputEngine? _audioEngine;
    private readonly System.Timers.Timer _hudIdleTimer;
    private readonly System.Timers.Timer _slideshowTimer;
    private readonly System.Timers.Timer _visualizerTimer;
    private readonly Random _random = new();

    public event EventHandler<string>? LaunchMixviewRequested;

    private NowPlayingMode _mode = NowPlayingMode.ArtistCanvas;
    private bool _isHudVisible = true;
    private bool _isBioDrawerOpen = false;
    private bool _isShowlistOpen = false;

    public ObservableCollection<Album> MosaicWallAlbums { get; } = new();
    public ObservableCollection<Track> UpcomingQueue { get; } = new();
    public ObservableCollection<double> VisualizerBars { get; } = new();

    private readonly string[] _themeBackdrops = new[]
    {
        "avares://Dorado.UI/Assets/Zune/Backgrounds/DORADO-BACKGROUND-01.PNG",
        "avares://Dorado.UI/Assets/Zune/Backgrounds/DORADO-BACKGROUND-02.PNG",
        "avares://Dorado.UI/Assets/Zune/Backgrounds/DORADO-BACKGROUND-03.PNG",
        "avares://Dorado.UI/Assets/Zune/Backgrounds/DORADO-BACKGROUND-04.PNG",
        "avares://Dorado.UI/Assets/Zune/Backgrounds/DORADO-BACKGROUND-05.PNG",
        "avares://Dorado.UI/Assets/Zune/Backgrounds/DORADO-BACKGROUND-06.PNG",
        "avares://Dorado.UI/Assets/Zune/Backgrounds/DORADO-BACKGROUND-07.PNG",
        "avares://Dorado.UI/Assets/Zune/Backgrounds/DORADO-BACKGROUND-08.PNG",
        "avares://Dorado.UI/Assets/Zune/Backgrounds/DORADO-BACKGROUND-09.PNG"
    };

    private List<string> _activeBackdrops;
    private string? _activeBackdropArtist;
    private int _backdropIndex = 0;
    private string _currentBackdropImage;

    private string? _enrichedBiography;
    private string? _biographySource;
    private string? _lyrics;
    public string CurrentBackdropImage
    {
        get => _currentBackdropImage;
        set => SetProperty(ref _currentBackdropImage, value);
    }

    private double _kenBurnsScale = 1.05;
    public double KenBurnsScale
    {
        get => _kenBurnsScale;
        set => SetProperty(ref _kenBurnsScale, value);
    }

    private double _kenBurnsTranslateX = 0;
    public double KenBurnsTranslateX
    {
        get => _kenBurnsTranslateX;
        set => SetProperty(ref _kenBurnsTranslateX, value);
    }

    private double _kenBurnsTranslateY = 0;
    public double KenBurnsTranslateY
    {
        get => _kenBurnsTranslateY;
        set => SetProperty(ref _kenBurnsTranslateY, value);
    }

    public NowPlayingMode Mode
    {
        get => _mode;
        set
        {
            if (SetProperty(ref _mode, value))
            {
                OnPropertyChanged(nameof(IsArtistCanvasMode));
                OnPropertyChanged(nameof(IsMosaicWallMode));
                OnPropertyChanged(nameof(IsVideoMode));

                if (value == NowPlayingMode.Video)
                {
                    _ = LoadLibraryVideosAsync();
                }
            }
        }
    }

    public bool IsArtistCanvasMode => _mode == NowPlayingMode.ArtistCanvas;
    public bool IsMosaicWallMode => _mode == NowPlayingMode.MosaicWall;
    public bool IsVideoMode => _mode == NowPlayingMode.Video;

    private readonly IVideoLibraryService? _videoLibraryService;
    private readonly IVideoPlaybackEngine? _videoEngine;

    public ObservableCollection<Video> LibraryVideos { get; } = new();

    private Video? _selectedVideo;
    public Video? SelectedVideo
    {
        get => _selectedVideo;
        set
        {
            if (SetProperty(ref _selectedVideo, value))
            {
                OpenVideoPlayback();
            }
        }
    }

    private VideoPlaybackViewModel? _videoPlaybackVM;
    public VideoPlaybackViewModel? VideoPlaybackVM
    {
        get => _videoPlaybackVM;
        private set
        {
            if (SetProperty(ref _videoPlaybackVM, value))
            {
                OnPropertyChanged(nameof(HasVideoPlayback));
            }
        }
    }

    public bool HasVideoPlayback => VideoPlaybackVM != null;

    private async System.Threading.Tasks.Task LoadLibraryVideosAsync()
    {
        if (_videoLibraryService == null)
        {
            return;
        }

        try
        {
            var videos = await _videoLibraryService.GetAllVideosAsync();
            LibraryVideos.Clear();
            foreach (var v in videos)
            {
                LibraryVideos.Add(v);
            }

            if (_selectedVideo == null && LibraryVideos.Count > 0)
            {
                SelectedVideo = LibraryVideos[0];
            }
        }
        catch (Exception)
        {
            // Video library is best-effort inside Now Playing.
        }
    }

    private void OpenVideoPlayback()
    {
        if (_videoEngine == null || _selectedVideo == null)
        {
            VideoPlaybackVM = null;
            return;
        }

        var playback = new VideoPlaybackViewModel(_videoEngine, _selectedVideo);
        playback.Start();
        VideoPlaybackVM = playback;
    }

    public bool IsHudVisible
    {
        get => _isHudVisible;
        set => SetProperty(ref _isHudVisible, value);
    }

    public bool IsBioDrawerOpen
    {
        get => _isBioDrawerOpen;
        set => SetProperty(ref _isBioDrawerOpen, value);
    }

    public bool IsShowlistOpen
    {
        get => _isShowlistOpen;
        set => SetProperty(ref _isShowlistOpen, value);
    }

    // Tier A4: drawer slide-in / fade. Targets are set by the toggle commands; the animation
    // dispatcher interpolates the current values toward the targets at ~60fps, then stops.
    private double _bioDrawerOffsetX = -400;
    private double _bioDrawerOpacity;
    private double _showlistDrawerOffsetX = 400;
    private double _showlistDrawerOpacity;

    public double BioDrawerOffsetX
    {
        get => _bioDrawerOffsetX;
        private set => SetProperty(ref _bioDrawerOffsetX, value);
    }

    public double BioDrawerOpacity
    {
        get => _bioDrawerOpacity;
        private set => SetProperty(ref _bioDrawerOpacity, value);
    }

    public double ShowlistDrawerOffsetX
    {
        get => _showlistDrawerOffsetX;
        private set => SetProperty(ref _showlistDrawerOffsetX, value);
    }

    public double ShowlistDrawerOpacity
    {
        get => _showlistDrawerOpacity;
        private set => SetProperty(ref _showlistDrawerOpacity, value);
    }

    private double _bioTargetOffset;
    private double _bioTargetOpacity;
    private double _showlistTargetOffset;
    private double _showlistTargetOpacity;
    private Avalonia.Threading.DispatcherTimer? _drawerAnimTimer;

    public int UpcomingQueueCount => UpcomingQueue.Count;

    public Track? CurrentTrack => _playerCoordinator.CurrentTrack;
    public string TrackTitle => CurrentTrack?.Title ?? "No Track Playing";
    public string ArtistName => CurrentTrack?.ArtistName ?? "Zune Player";
    public string AlbumTitle => CurrentTrack?.AlbumTitle ?? string.Empty;
    public string Genre => CurrentTrack?.Genre ?? "Music";
    public int Year => CurrentTrack?.Year ?? 0;

    public bool IsPlaying => _playerCoordinator.State == PlaybackState.Playing;
    public string PlayPauseIcon => IsPlaying ? "⏸" : "▶";
    public bool IsFavorite => CurrentTrack?.Rating == HeartRating.Favorite;
    public bool IsDisliked => CurrentTrack?.Rating == HeartRating.Dislike;

    public TimeSpan CurrentPosition => _playerCoordinator.CurrentPosition;
    public TimeSpan Duration => _playerCoordinator.Duration;
    public string ElapsedTimeText => CurrentPosition.ToString(@"m\:ss");
    public string RemainingTimeText => (Duration - CurrentPosition).ToString(@"\-m\:ss");

    public double ProgressPercentage
    {
        get
        {
            if (Duration.TotalSeconds <= 0) return 0.0;
            return Math.Clamp(CurrentPosition.TotalSeconds / Duration.TotalSeconds, 0.0, 1.0);
        }
    }

    public string BiographyText => _enrichedBiography
        ?? $"{ArtistName} is cataloged in your Dorado collection. Enable online metadata services in Settings to display the full artist biography here.";

    public string? BiographySource => _biographySource;

    public string LyricsText => _lyrics
        ?? "No lyrics found for this track. Online lyric lookup runs through LRCLIB when enabled in Settings > Software > Metadata.";

    public ICommand ToggleModeCommand { get; }
    public ICommand ToggleBioDrawerCommand { get; }
    public ICommand ToggleShowlistCommand { get; }
    public ICommand PlayQueueTrackCommand { get; }
    public ICommand ResetHudTimerCommand { get; }
    public ICommand PlayPauseCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand PreviousCommand { get; }
    public ICommand ToggleFavoriteCommand { get; }
    public ICommand ToggleDislikeCommand { get; }
    public ICommand LaunchMixviewCommand { get; }

    public NowPlayingViewModel(
        IPlayerCoordinator playerCoordinator,
        IMediaLibraryService libraryService,
        IArtistEnrichmentService? enrichmentService = null,
        IAudioOutputEngine? audioEngine = null,
        IVideoLibraryService? videoLibraryService = null,
        IVideoPlaybackEngine? videoEngine = null)
    {
        _playerCoordinator = playerCoordinator;
        _libraryService = libraryService;
        _enrichmentService = enrichmentService;
        _audioEngine = audioEngine;
        _videoLibraryService = videoLibraryService;
        _videoEngine = videoEngine;

        _activeBackdrops = new List<string>(_themeBackdrops);
        _currentBackdropImage = _activeBackdrops[0];

        if (_enrichmentService != null)
        {
            _enrichmentService.EnrichmentCompleted += OnEnrichmentCompleted;
        }

        // Initialize 24 ambient visualizer bars
        for (int i = 0; i < 24; i++)
        {
            VisualizerBars.Add(4.0);
        }

        // Auto-hiding HUD timer (fades out after 3.5 seconds of idle)
        _hudIdleTimer = new System.Timers.Timer(3500) { AutoReset = false };
        _hudIdleTimer.Elapsed += (s, e) =>
        {
            if (IsPlaying)
            {
                IsHudVisible = false;
            }
        };

        // Ken-Burns slideshow timer (cycles backdrop photo and motion every 8 seconds)
        _slideshowTimer = new System.Timers.Timer(8000) { AutoReset = true };
        _slideshowTimer.Elapsed += (s, e) =>
        {
            if (_activeBackdrops.Count == 0) return;
            _backdropIndex = (_backdropIndex + 1) % _activeBackdrops.Count;
            CurrentBackdropImage = _activeBackdrops[_backdropIndex];
            KenBurnsScale = 1.05 + (_random.NextDouble() * 0.12);
            KenBurnsTranslateX = (_random.NextDouble() * 40) - 20;
            KenBurnsTranslateY = (_random.NextDouble() * 30) - 15;
        };
        _slideshowTimer.Start();

        // Ambient visualizer update loop (every 75ms)
        _visualizerTimer = new System.Timers.Timer(75) { AutoReset = true };
        _visualizerTimer.Elapsed += (s, e) =>
        {
            if (IsPlaying && _audioEngine is { IsAvailable: true })
            {
                // Real spectrum from the live audio output.
                var bands = _audioEngine.GetFftData();
                if (bands.Length == VisualizerBars.Count)
                {
                    for (int i = 0; i < VisualizerBars.Count; i++)
                    {
                        // Peak-weighted so bass bands read taller, matching Zune's energy distribution.
                        double weight = 1.0 + (1.0 - (i / (double)VisualizerBars.Count)) * 0.9;
                        VisualizerBars[i] = Math.Clamp(bands[i] * weight * 45.0 + 4.0, 4.0, 49.0);
                    }

                    return;
                }
            }

            if (IsPlaying)
            {
                for (int i = 0; i < VisualizerBars.Count; i++)
                {
                    // Simulated natural spectrum distribution (higher energy in bass, taper in highs)
                    double factor = 1.0 - (i * 0.03);
                    double height = 4.0 + (_random.NextDouble() * 45.0 * factor);
                    VisualizerBars[i] = height;
                }
            }
            else
            {
                for (int i = 0; i < VisualizerBars.Count; i++)
                {
                    if (VisualizerBars[i] > 4.0)
                    {
                        VisualizerBars[i] = Math.Max(4.0, VisualizerBars[i] * 0.7);
                    }
                }
            }
        };
        _visualizerTimer.Start();

        ToggleModeCommand = new RelayCommand(() =>
        {
            Mode = Mode switch
            {
                NowPlayingMode.ArtistCanvas => NowPlayingMode.MosaicWall,
                NowPlayingMode.MosaicWall => NowPlayingMode.Video,
                _ => NowPlayingMode.ArtistCanvas
            };
        });

        ToggleBioDrawerCommand = new RelayCommand(() =>
        {
            IsBioDrawerOpen = !IsBioDrawerOpen;
            if (IsBioDrawerOpen) IsShowlistOpen = false;
            TriggerHudActivity();
            UpdateDrawerTargets();
        });

        ToggleShowlistCommand = new RelayCommand(() =>
        {
            IsShowlistOpen = !IsShowlistOpen;
            if (IsShowlistOpen)
            {
                IsBioDrawerOpen = false;
                UpdateUpcomingQueue();
            }
            TriggerHudActivity();
            UpdateDrawerTargets();
        });

        PlayQueueTrackCommand = new AsyncRelayCommand<Track>(async track =>
        {
            if (track != null)
            {
                await _playerCoordinator.PlayTrackAsync(track);
                TriggerHudActivity();
            }
        });

        ResetHudTimerCommand = new RelayCommand(TriggerHudActivity);

        PlayPauseCommand = new AsyncRelayCommand(async () =>
        {
            await _playerCoordinator.PlayPauseAsync();
            TriggerHudActivity();
        });

        NextCommand = new AsyncRelayCommand(async () =>
        {
            await _playerCoordinator.NextAsync();
            TriggerHudActivity();
        });

        PreviousCommand = new AsyncRelayCommand(async () =>
        {
            await _playerCoordinator.PreviousAsync();
            TriggerHudActivity();
        });

        ToggleFavoriteCommand = new AsyncRelayCommand(async () =>
        {
            if (CurrentTrack == null) return;
            var newRating = CurrentTrack.Rating == HeartRating.Favorite ? HeartRating.None : HeartRating.Favorite;
            await _playerCoordinator.SetRatingAsync(CurrentTrack.Id, newRating);
            await _libraryService.SetTrackRatingAsync(CurrentTrack.Id, newRating);
            TriggerHudActivity();
        });

        ToggleDislikeCommand = new AsyncRelayCommand(async () =>
        {
            if (CurrentTrack == null) return;
            var newRating = CurrentTrack.Rating == HeartRating.Dislike ? HeartRating.None : HeartRating.Dislike;
            await _playerCoordinator.SetRatingAsync(CurrentTrack.Id, newRating);
            await _libraryService.SetTrackRatingAsync(CurrentTrack.Id, newRating);
            if (newRating == HeartRating.Dislike)
            {
                await _playerCoordinator.NextAsync();
            }
            TriggerHudActivity();
        });

        LaunchMixviewCommand = new RelayCommand(() =>
        {
            LaunchMixviewRequested?.Invoke(this, ArtistName);
        });

        _playerCoordinator.TrackChanged += OnTrackChanged;
        _playerCoordinator.StateChanged += OnStateChanged;
        _playerCoordinator.RatingChanged += OnRatingChanged;

        _ = LoadMosaicWallAsync();
        UpdateUpcomingQueue();
        TriggerHudActivity();
    }

    public void TriggerHudActivity()
    {
        IsHudVisible = true;
        _hudIdleTimer.Stop();
        _hudIdleTimer.Start();
    }

    private async Task LoadMosaicWallAsync()
    {
        var albums = await _libraryService.GetAllAlbumsAsync();
        MosaicWallAlbums.Clear();
        foreach (var album in albums)
        {
            MosaicWallAlbums.Add(album);
        }
    }

    public void UpdateUpcomingQueue()
    {
        UpcomingQueue.Clear();
        foreach (var track in _playerCoordinator.Queue)
        {
            UpcomingQueue.Add(track);
        }
        OnPropertyChanged(nameof(UpcomingQueueCount));
    }

    private void OnTrackChanged(object? sender, TrackChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CurrentTrack));
        OnPropertyChanged(nameof(TrackTitle));
        OnPropertyChanged(nameof(ArtistName));
        OnPropertyChanged(nameof(AlbumTitle));
        OnPropertyChanged(nameof(Genre));
        OnPropertyChanged(nameof(Year));
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(CurrentPosition));
        OnPropertyChanged(nameof(ProgressPercentage));
        OnPropertyChanged(nameof(ElapsedTimeText));
        OnPropertyChanged(nameof(RemainingTimeText));
        OnPropertyChanged(nameof(IsFavorite));
        OnPropertyChanged(nameof(IsDisliked));
        UpdateUpcomingQueue();
        TriggerHudActivity();

        BeginArtistEnrichment();
        _ = LoadLyricsForCurrentTrackAsync();
    }

    private void BeginArtistEnrichment()
    {
        if (_enrichmentService == null)
        {
            return;
        }

        var artist = ArtistName;
        if (string.IsNullOrWhiteSpace(artist))
        {
            return;
        }

        var cached = _enrichmentService.GetCached(artist);
        if (cached != null)
        {
            ApplyEnrichmentSnapshot(cached);
            return;
        }

        if (_activeBackdropArtist != artist)
        {
            ResetBackdropsToTheme();
            _enrichedBiography = null;
            _biographySource = null;
            OnPropertyChanged(nameof(BiographyText));
            OnPropertyChanged(nameof(BiographySource));
        }

        _ = LoadPersistedBiographyAsync(artist);
        _enrichmentService.RequestEnrichment(artist);
    }

    private async System.Threading.Tasks.Task LoadPersistedBiographyAsync(string artistName)
    {
        try
        {
            var artists = await _libraryService.GetAllArtistsAsync();
            var match = artists.FirstOrDefault(a => a.Name.Equals(artistName, StringComparison.OrdinalIgnoreCase));
            if (match == null || string.IsNullOrWhiteSpace(match.Biography))
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!ArtistName.Equals(artistName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (_enrichedBiography == null && _enrichmentService?.GetCached(artistName) == null)
                {
                    _enrichedBiography = match.Biography;
                    _biographySource = "Library";
                    OnPropertyChanged(nameof(BiographyText));
                    OnPropertyChanged(nameof(BiographySource));
                }
            });
        }
        catch (Exception)
        {
            // Persisted biography load is best-effort.
        }
    }

    private async System.Threading.Tasks.Task LoadLyricsForCurrentTrackAsync()
    {
        if (_enrichmentService == null)
        {
            return;
        }

        var artist = ArtistName;
        var title = TrackTitle;
        var duration = CurrentTrack?.Duration;

        var result = await _enrichmentService.GetLyricsAsync(artist, title, duration);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!ArtistName.Equals(artist, StringComparison.OrdinalIgnoreCase) || !TrackTitle.Equals(title, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _lyrics = result is { Found: true }
                ? (!string.IsNullOrWhiteSpace(result.PlainLyrics) ? result.PlainLyrics : LrcLibStrip(result.SyncedLyrics))
                : null;
            OnPropertyChanged(nameof(LyricsText));
        });
    }

    private static string? LrcLibStrip(string? syncedLyrics) => string.IsNullOrWhiteSpace(syncedLyrics) ? null : syncedLyrics;

    private void OnEnrichmentCompleted(object? sender, ArtistEnrichmentSnapshot snapshot)
    {
        Dispatcher.UIThread.Post(() => ApplyEnrichmentSnapshot(snapshot));
    }

    private void ApplyEnrichmentSnapshot(ArtistEnrichmentSnapshot snapshot)
    {
        if (!ArtistName.Equals(snapshot.ArtistName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _enrichedBiography = snapshot.Biography;
        _biographySource = snapshot.BiographySource;
        OnPropertyChanged(nameof(BiographyText));
        OnPropertyChanged(nameof(BiographySource));

        if (snapshot.BackdropLocalPaths.Count > 0)
        {
            _activeBackdropArtist = snapshot.ArtistName;
            _activeBackdrops = snapshot.BackdropLocalPaths.ToList();
            _backdropIndex = 0;
            CurrentBackdropImage = _activeBackdrops[0];
            KenBurnsScale = 1.05 + (_random.NextDouble() * 0.12);
            KenBurnsTranslateX = (_random.NextDouble() * 40) - 20;
            KenBurnsTranslateY = (_random.NextDouble() * 30) - 15;
        }
    }

    private void ResetBackdropsToTheme()
    {
        _activeBackdropArtist = null;
        _activeBackdrops = new List<string>(_themeBackdrops);
        _backdropIndex = 0;
        CurrentBackdropImage = _activeBackdrops[0];
    }

    private void OnStateChanged(object? sender, PlaybackStateChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(PlayPauseIcon));
        OnPropertyChanged(nameof(CurrentPosition));
        OnPropertyChanged(nameof(ProgressPercentage));
        OnPropertyChanged(nameof(ElapsedTimeText));
        OnPropertyChanged(nameof(RemainingTimeText));
    }

    private void OnRatingChanged(object? sender, HeartRatingChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsFavorite));
        OnPropertyChanged(nameof(IsDisliked));
    }

    // Tier A4: drawer slide-in / fade targets. When a drawer opens, target = (0, 1);
    // when it closes, target = (-400 or 400, 0). A 60fps timer lerps the live values.
    private void UpdateDrawerTargets()
    {
        _bioTargetOffset = IsBioDrawerOpen ? 0.0 : -400.0;
        _bioTargetOpacity = IsBioDrawerOpen ? 1.0 : 0.0;
        _showlistTargetOffset = IsShowlistOpen ? 0.0 : 400.0;
        _showlistTargetOpacity = IsShowlistOpen ? 1.0 : 0.0;

        _drawerAnimTimer ??= new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        if (_drawerAnimTimer.IsEnabled)
        {
            return;
        }
        _drawerAnimTimer.Tick -= OnDrawerAnimTick;
        _drawerAnimTimer.Tick += OnDrawerAnimTick;
        _drawerAnimTimer.Start();
    }

    private void OnDrawerAnimTick(object? sender, EventArgs e)
    {
        var step = 0.18; // ~280ms full traversal at 60fps
        var done = true;

        done = StepToward(ref _bioDrawerOffsetX, _bioTargetOffset, step) && done;
        done = StepToward(ref _bioDrawerOpacity, _bioTargetOpacity, step) && done;
        done = StepToward(ref _showlistDrawerOffsetX, _showlistTargetOffset, step) && done;
        done = StepToward(ref _showlistDrawerOpacity, _showlistTargetOpacity, step) && done;

        OnPropertyChanged(nameof(BioDrawerOffsetX));
        OnPropertyChanged(nameof(BioDrawerOpacity));
        OnPropertyChanged(nameof(ShowlistDrawerOffsetX));
        OnPropertyChanged(nameof(ShowlistDrawerOpacity));

        if (done)
        {
            _drawerAnimTimer?.Stop();
        }
    }

    private static bool StepToward(ref double current, double target, double step)
    {
        if (Math.Abs(current - target) <= step)
        {
            if (current != target)
            {
                current = target;
                return false; // caller will still report a change on the next OnPropertyChanged
            }
            return true;
        }
        current += Math.Sign(target - current) * step;
        return false;
    }
}
