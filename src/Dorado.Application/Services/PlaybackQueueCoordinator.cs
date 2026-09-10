using Dorado.Application.Events;
using Dorado.Application.Interfaces;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;

namespace Dorado.Application.Services;

public class PlaybackQueueCoordinator : IPlayerCoordinator, IEqualizerControl
{
    private readonly List<Track> _queue = new();
    private readonly List<Track> _history = new();
    private readonly IAudioOutputEngine? _audioEngine;
    private readonly IReplayGainService? _replayGainService;
    private int _currentIndex = -1;
    private PlaybackState _state = PlaybackState.Stopped;
    private TimeSpan _currentPosition = TimeSpan.Zero;
    private double _volume = 1.0;
    private bool _isMuted;
    private bool _shuffle;
    private bool _repeat;
    private readonly Random _random = new();
    private Track? _pendingAutoTrack;
    private double _replayGainFactor = 1.0;

    public PlaybackQueueCoordinator(IAudioOutputEngine? audioEngine = null, IReplayGainService? replayGainService = null)
    {
        _audioEngine = audioEngine;
        _replayGainService = replayGainService;

        if (_audioEngine != null)
        {
            _audioEngine.TrackEnded += OnEngineTrackEnded;
            _audioEngine.TrackTransitioned += OnEngineTrackTransitioned;
        }
    }

    public PlaybackState State => _state;
    public Track? CurrentTrack => (_currentIndex >= 0 && _currentIndex < _queue.Count) ? _queue[_currentIndex] : null;
    public void ApplyEqualizer(bool enabled, IReadOnlyList<double> bandGainsDb, double preampDb)
    {
        if (_audioEngine is IEqualizerControl equalizer)
        {
            equalizer.ApplyEqualizer(enabled, bandGainsDb, preampDb);
        }
    }

    public TimeSpan CurrentPosition => _audioEngine is { IsAvailable: true } && !IsSimulatedPlayback
        ? _audioEngine.GetPosition()
        : _currentPosition;
    public TimeSpan Duration
    {
        get
        {
            if (_audioEngine is { IsAvailable: true } && !IsSimulatedPlayback)
            {
                var engineDuration = _audioEngine.GetDuration();
                if (engineDuration > TimeSpan.Zero)
                {
                    return engineDuration;
                }
            }

            return CurrentTrack?.Duration ?? TimeSpan.Zero;
        }
    }

    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0.0, 1.0);
            ApplyOutputVolume();
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            ApplyOutputVolume();
        }
    }

    public bool Shuffle
    {
        get => _shuffle;
        set
        {
            _shuffle = value;
            ScheduleAutoTransition();
        }
    }

    public bool Repeat
    {
        get => _repeat;
        set
        {
            _repeat = value;
            ScheduleAutoTransition();
        }
    }

    private double _crossfadeDurationSeconds = 2.0;
    public double CrossfadeDurationSeconds
    {
        get => _crossfadeDurationSeconds;
        set
        {
            _crossfadeDurationSeconds = Math.Clamp(value, 0.0, 10.0);
            ScheduleAutoTransition();
        }
    }

    private bool _isCrossfading;
    public bool IsCrossfading
    {
        get => _isCrossfading;
        private set => _isCrossfading = value;
    }

    private bool _gaplessEnabled = true;
    public bool GaplessEnabled
    {
        get => _gaplessEnabled;
        set => _gaplessEnabled = value;
    }

    private bool _volumeLevelingEnabled;
    public bool VolumeLevelingEnabled
    {
        get => _volumeLevelingEnabled;
        set
        {
            _volumeLevelingEnabled = value;
            if (CurrentTrack != null)
            {
                LoadReplayGainFactor(CurrentTrack);
            }

            ApplyOutputVolume();
        }
    }

    /// <summary>
    /// True when playback progress is simulated: no real engine, unavailable output, a source that
    /// failed to open, or demo tracks without an on-disk/stream source. The simulated clock
    /// (<c>AudioEngine</c>) only advances in this mode.
    /// </summary>
    public bool IsSimulatedPlayback =>
        _audioEngine is not { IsAvailable: true, HasActiveSource: true }
        || CurrentTrack == null
        || string.IsNullOrWhiteSpace(CurrentTrack.FilePath);

    public IReadOnlyList<Track> Queue
    {
        get
        {
            lock (_queue)
            {
                return _queue.ToList();
            }
        }
    }

    public event EventHandler<TrackChangedEventArgs>? TrackChanged;
    public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;
    public event EventHandler<HeartRatingChangedEventArgs>? RatingChanged;

    public Task PlayTrackAsync(Track track, IEnumerable<Track>? contextQueue = null)
    {
        lock (_queue)
        {
            if (contextQueue != null)
            {
                _queue.Clear();
                _queue.AddRange(contextQueue);
                _currentIndex = _queue.FindIndex(t => t.Id == track.Id);
                if (_currentIndex == -1)
                {
                    _queue.Insert(0, track);
                    _currentIndex = 0;
                }
            }
            else
            {
                _currentIndex = _queue.FindIndex(t => t.Id == track.Id);
                if (_currentIndex == -1)
                {
                    _queue.Add(track);
                    _currentIndex = _queue.Count - 1;
                }
            }

            _currentPosition = TimeSpan.Zero;
            _state = PlaybackState.Playing;
        }

        StartAudiblePlayback();
        TrackChanged?.Invoke(this, new TrackChangedEventArgs(CurrentTrack, _currentPosition));
        StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(_state, _currentPosition));
        return Task.CompletedTask;
    }

    public Task PlayPauseAsync()
    {
        if (CurrentTrack == null && _queue.Count > 0)
        {
            _currentIndex = 0;
            _state = PlaybackState.Playing;
            StartAudiblePlayback();
        }
        else if (_state == PlaybackState.Playing)
        {
            _state = PlaybackState.Paused;
            _audioEngine?.Pause();
        }
        else if (_state == PlaybackState.Paused || _state == PlaybackState.Stopped)
        {
            if (CurrentTrack != null)
            {
                _state = PlaybackState.Playing;
                if (!IsSimulatedPlayback)
                {
                    _audioEngine?.Play();
                }
            }
        }

        StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(_state, _currentPosition));
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _state = PlaybackState.Stopped;
        _currentPosition = TimeSpan.Zero;
        IsCrossfading = false;
        _audioEngine?.Stop();
        _pendingAutoTrack = null;
        StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(_state, _currentPosition));
        return Task.CompletedTask;
    }

    public Task NextAsync()
    {
        lock (_queue)
        {
            if (_queue.Count == 0)
            {
                return Task.CompletedTask;
            }

            if (_shuffle)
            {
                _currentIndex = _queue.Count == 1 ? 0 : PickDifferentRandomIndex();
            }
            else if (_currentIndex + 1 < _queue.Count)
            {
                _currentIndex++;
            }
            else if (_repeat)
            {
                _currentIndex = 0;
            }
            else
            {
                _state = PlaybackState.Stopped;
                _currentPosition = TimeSpan.Zero;
                IsCrossfading = false;
                _audioEngine?.Stop();
                _pendingAutoTrack = null;
                StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(_state, _currentPosition));
                return Task.CompletedTask;
            }

            _currentPosition = TimeSpan.Zero;
            IsCrossfading = false;
            _state = PlaybackState.Playing;
        }

        StartAudiblePlayback();
        TrackChanged?.Invoke(this, new TrackChangedEventArgs(CurrentTrack, _currentPosition));
        StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(_state, _currentPosition));
        return Task.CompletedTask;
    }

    public Task PreviousAsync()
    {
        var restartOnly = false;
        lock (_queue)
        {
            // If more than 3 seconds in, restart track
            if (_currentPosition.TotalSeconds > 3)
            {
                _currentPosition = TimeSpan.Zero;
                restartOnly = true;
            }
            else if (_currentIndex > 0)
            {
                _currentIndex--;
                _currentPosition = TimeSpan.Zero;
            }
            else if (_repeat)
            {
                _currentIndex = _queue.Count - 1;
                _currentPosition = TimeSpan.Zero;
            }
        }

        if (restartOnly && !IsSimulatedPlayback)
        {
            _audioEngine?.Seek(TimeSpan.Zero);
        }
        else
        {
            StartAudiblePlayback();
        }

        TrackChanged?.Invoke(this, new TrackChangedEventArgs(CurrentTrack, _currentPosition));
        StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(_state, _currentPosition));
        return Task.CompletedTask;
    }

    public Task SeekAsync(TimeSpan position)
    {
        if (CurrentTrack == null) return Task.CompletedTask;

        _currentPosition = position < TimeSpan.Zero ? TimeSpan.Zero :
                           position > Duration ? Duration : position;

        if (!IsSimulatedPlayback)
        {
            _audioEngine?.Seek(_currentPosition);
        }

        if (Duration > TimeSpan.Zero && _crossfadeDurationSeconds > 0)
        {
            IsCrossfading = (Duration - _currentPosition) <= TimeSpan.FromSeconds(_crossfadeDurationSeconds);
        }
        else
        {
            IsCrossfading = false;
        }

        StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(_state, _currentPosition));
        return Task.CompletedTask;
    }

    public Task SetRatingAsync(Guid trackId, HeartRating rating)
    {
        lock (_queue)
        {
            foreach (var t in _queue.Where(t => t.Id == trackId))
            {
                t.Rating = rating;
            }
        }

        RatingChanged?.Invoke(this, new HeartRatingChangedEventArgs(trackId, rating));
        return Task.CompletedTask;
    }

    public void Enqueue(IEnumerable<Track> tracks)
    {
        lock (_queue)
        {
            _queue.AddRange(tracks);
        }

        ScheduleAutoTransition();
    }

    public void PlayNext(IEnumerable<Track> tracks)
    {
        lock (_queue)
        {
            var insertPos = _currentIndex >= 0 ? _currentIndex + 1 : 0;
            _queue.InsertRange(insertPos, tracks);
        }

        ScheduleAutoTransition();
    }

    private void StartAudiblePlayback()
    {
        var track = CurrentTrack;
        if (track == null)
        {
            return;
        }

        _pendingAutoTrack = null;
        LoadReplayGainFactor(track);

        // Attempt real output whenever the track has a source. This must NOT be gated on
        // IsSimulatedPlayback: the engine only reports IsAvailable after its first
        // LoadAndPlay call, so gating on it created a deadlock where the engine was never
        // initialized and playback never produced sound.
        if (_audioEngine != null && !string.IsNullOrWhiteSpace(track.FilePath))
        {
            _audioEngine.LoadAndPlay(track.FilePath);
            ApplyOutputVolume();
        }

        ScheduleAutoTransition();
    }

    private void ScheduleAutoTransition()
    {
        if (_audioEngine is not { IsAvailable: true })
        {
            return;
        }

        var next = PeekNextTrack();
        _pendingAutoTrack = next;

        // Crossfade > 0: blended overlap. Crossfade == 0 + gapless: sample-boundary chain.
        // Crossfade == 0 + no gapless: no scheduling — TrackEnded drives a plain stop/advance.
        if (_crossfadeDurationSeconds > 0)
        {
            _audioEngine.SetAutoTransition(next?.FilePath, _crossfadeDurationSeconds);
        }
        else if (_gaplessEnabled)
        {
            _audioEngine.SetAutoTransition(next?.FilePath, 0.0);
        }
        else
        {
            _audioEngine.SetAutoTransition(null, 0.0);
            _pendingAutoTrack = null;
        }
    }

    private Track? PeekNextTrack()
    {
        lock (_queue)
        {
            if (_queue.Count == 0 || CurrentTrack == null)
            {
                return null;
            }

            if (_shuffle)
            {
                if (_queue.Count == 1)
                {
                    return null;
                }

                var index = PickDifferentRandomIndex();
                return _queue[index];
            }

            if (_currentIndex + 1 < _queue.Count)
            {
                return _queue[_currentIndex + 1];
            }

            return _repeat ? _queue[0] : null;
        }
    }

    private int PickDifferentRandomIndex()
    {
        if (_queue.Count <= 1)
        {
            return 0;
        }

        var next = _random.Next(0, _queue.Count);
        return next == _currentIndex ? (next + 1) % _queue.Count : next;
    }

    private void OnEngineTrackTransitioned(object? sender, EventArgs e)
    {
        var pending = _pendingAutoTrack;
        _pendingAutoTrack = null;
        if (pending == null)
        {
            return;
        }

        lock (_queue)
        {
            var index = _queue.FindIndex(t => t.Id == pending.Id);
            if (index >= 0)
            {
                _currentIndex = index;
            }

            _currentPosition = TimeSpan.Zero;
            IsCrossfading = _audioEngine is { IsAvailable: true } && _crossfadeDurationSeconds > 0;
            _state = PlaybackState.Playing;
        }

        LoadReplayGainFactor(pending);
        ApplyOutputVolume();
        ScheduleAutoTransition();
        TrackChanged?.Invoke(this, new TrackChangedEventArgs(CurrentTrack, _currentPosition));
    }

    private void OnEngineTrackEnded(object? sender, EventArgs e)
    {
        _pendingAutoTrack = null;
        _ = NextAsync();
    }

    private void LoadReplayGainFactor(Track track)
    {
        _replayGainFactor = 1.0;
        if (!_volumeLevelingEnabled || _replayGainService == null)
        {
            return;
        }

        var gainDb = _replayGainService.ReadTrackGainDb(track.FilePath);
        if (gainDb.HasValue)
        {
            // Convert the gain to a linear factor; pre-amp correction typical of ReplayGain.
            _replayGainFactor = Math.Clamp(Math.Pow(10, gainDb.Value / 20.0), 0.05, 1.25);
        }
    }

    private void ApplyOutputVolume()
    {
        if (_audioEngine is not { IsAvailable: true })
        {
            return;
        }

        var effective = Math.Clamp(_volume * _replayGainFactor, 0.0, 1.0);
        _audioEngine.SetVolume(effective, _isMuted);
    }
}
