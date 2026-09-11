using Dorado.Application.Events;
using Dorado.Application.Interfaces;
using Dorado.Domain.Enums;

namespace Dorado.Application.Services;

/// <summary>
/// Bridges <see cref="IPlayerCoordinator"/> to an <see cref="ISystemMediaControls"/>
/// implementation: player state → OS, and OS commands (media keys / shell widget) →
/// player. Platform-agnostic and safe with a null implementation.
/// </summary>
public sealed class SystemMediaControlsCoordinator : IDisposable
{
    private readonly IPlayerCoordinator _player;
    private readonly ISystemMediaControls _controls;
    private bool _disposed;

    public SystemMediaControlsCoordinator(IPlayerCoordinator player, ISystemMediaControls controls)
    {
        _player = player;
        _controls = controls;

        _player.TrackChanged += OnTrackChanged;
        _player.StateChanged += OnStateChanged;

        _controls.PlayPauseRequested += OnPlayPauseRequested;
        _controls.NextRequested += OnNextRequested;
        _controls.PreviousRequested += OnPreviousRequested;
        _controls.StopRequested += OnStopRequested;
        _controls.SeekRequested += OnSeekRequested;

        Push();
    }

    public bool IsAvailable => _controls.IsAvailable;

    public void Push() =>
        _controls.Update(
            _player.CurrentTrack,
            _player.State == PlaybackState.Playing,
            _player.CurrentPosition,
            _player.Duration);

    private void OnTrackChanged(object? sender, TrackChangedEventArgs e) => Push();
    private void OnStateChanged(object? sender, PlaybackStateChangedEventArgs e) => Push();

    private void OnPlayPauseRequested(object? sender, EventArgs e) => _ = _player.PlayPauseAsync();
    private void OnNextRequested(object? sender, EventArgs e) => _ = _player.NextAsync();
    private void OnPreviousRequested(object? sender, EventArgs e) => _ = _player.PreviousAsync();
    private void OnStopRequested(object? sender, EventArgs e) => _ = _player.StopAsync();
    private void OnSeekRequested(object? sender, TimeSpan position) => _ = _player.SeekAsync(position);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _player.TrackChanged -= OnTrackChanged;
        _player.StateChanged -= OnStateChanged;
        _controls.PlayPauseRequested -= OnPlayPauseRequested;
        _controls.NextRequested -= OnNextRequested;
        _controls.PreviousRequested -= OnPreviousRequested;
        _controls.StopRequested -= OnStopRequested;
        _controls.SeekRequested -= OnSeekRequested;
    }
}
