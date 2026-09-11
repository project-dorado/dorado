using Dorado.Application.Interfaces;
using Dorado.Domain.Models;

namespace Dorado.Application.Services;

/// <summary>
/// No-op fallback used when no OS media integration is available for the current
/// platform/session. Keeps the coordinator wiring unconditional.
/// </summary>
public sealed class NullSystemMediaControls : ISystemMediaControls
{
    public static NullSystemMediaControls Instance { get; } = new();

    public bool IsAvailable => false;

    public void Update(Track? track, bool isPlaying, TimeSpan position, TimeSpan duration) { }

#pragma warning disable CS0067
    public event EventHandler? PlayPauseRequested;
    public event EventHandler? NextRequested;
    public event EventHandler? PreviousRequested;
    public event EventHandler? StopRequested;
    public event EventHandler<TimeSpan>? SeekRequested;
#pragma warning restore CS0067
}
