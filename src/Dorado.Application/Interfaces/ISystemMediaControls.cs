using Dorado.Domain.Models;

namespace Dorado.Application.Interfaces;

/// <summary>
/// Cross-platform seam for OS-level media integration (Linux MPRIS2, Windows SMTC,
/// macOS Now Playing, hardware media keys). Implementations are capability-guarded:
/// when unavailable, <see cref="IsAvailable"/> is false and all calls are no-ops.
/// Inbound events let the desktop environment drive playback (media keys, the
/// shell's media widget).
/// </summary>
public interface ISystemMediaControls
{
    bool IsAvailable { get; }

    /// <summary>Pushes the current now-playing state to the OS.</summary>
    void Update(Track? track, bool isPlaying, TimeSpan position, TimeSpan duration);

    event EventHandler? PlayPauseRequested;
    event EventHandler? NextRequested;
    event EventHandler? PreviousRequested;
    event EventHandler? StopRequested;
    event EventHandler<TimeSpan>? SeekRequested;
}
