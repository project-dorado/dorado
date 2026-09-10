using Dorado.Application.Events;
using Dorado.Application.Interfaces;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;

namespace Dorado.Infrastructure.Audio;

public class AudioEngine : IDisposable
{
    private readonly IPlayerCoordinator _playerCoordinator;
    private readonly System.Timers.Timer _positionTimer;
    private readonly SynchronizationContext? _syncContext;
    private bool _disposed;

    public AudioEngine(IPlayerCoordinator playerCoordinator)
    {
        _playerCoordinator = playerCoordinator;
        // Captured on the constructing (UI) thread so simulated ticks raise StateChanged
        // on the UI thread instead of a thread-pool thread, which Avalonia bindings require.
        _syncContext = SynchronizationContext.Current;
        _positionTimer = new System.Timers.Timer(250);
        _positionTimer.Elapsed += OnPositionTimerElapsed;
        _positionTimer.AutoReset = true;

        _playerCoordinator.StateChanged += OnPlaybackStateChanged;
    }

    private void OnPlaybackStateChanged(object? sender, PlaybackStateChangedEventArgs e)
    {
        if (e.State == PlaybackState.Playing)
        {
            _positionTimer.Start();
        }
        else
        {
            _positionTimer.Stop();
        }
    }

    private void OnPositionTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        // Only drive the simulated clock when there is no real audio output
        // (demo data without file sources, or no available audio device).
        if (_playerCoordinator.State != PlaybackState.Playing || !_playerCoordinator.IsSimulatedPlayback)
        {
            return;
        }

        var nextPos = _playerCoordinator.CurrentPosition + TimeSpan.FromMilliseconds(250);
        var reachedEnd = _playerCoordinator.Duration > TimeSpan.Zero && nextPos >= _playerCoordinator.Duration;

        if (_syncContext != null && _syncContext != SynchronizationContext.Current)
        {
            _syncContext.Post(_ =>
            {
                if (reachedEnd)
                {
                    _ = _playerCoordinator.NextAsync();
                }
                else
                {
                    _ = _playerCoordinator.SeekAsync(nextPos);
                }
            }, null);
        }
        else if (reachedEnd)
        {
            _ = _playerCoordinator.NextAsync();
        }
        else
        {
            _ = _playerCoordinator.SeekAsync(nextPos);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _positionTimer.Stop();
            _positionTimer.Dispose();
            _playerCoordinator.StateChanged -= OnPlaybackStateChanged;
        }
    }
}
