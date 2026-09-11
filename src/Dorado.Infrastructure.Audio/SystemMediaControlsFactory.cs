using Dorado.Application.Interfaces;
using Dorado.Application.Services;

namespace Dorado.Infrastructure.Audio;

/// <summary>
/// Selects the OS media-controls implementation by capability.
///
/// The platform-agnostic bridge (<see cref="ISystemMediaControls"/> +
/// <c>SystemMediaControlsCoordinator</c>) is complete and wired. A concrete
/// Linux MPRIS2 server and a Windows SMTC binding are the remaining work; both
/// are blocked here:
///   * Linux MPRIS2 — the API-compatible D-Bus library
///     (Tmds.DBus.Protocol 0.20.0–0.23.0) is flagged by advisory
///     GHSA-xrw6-gwf8-vvr9 (NU1903, high severity) and cannot ship under the
///     zero-warning policy; the fixed releases (0.90+) are a breaking rewrite.
///   * Windows SMTC — requires a Windows-targeted TFM (WinRT), which the
///     single cross-platform build does not currently emit.
///
/// Until one of those lands, this returns the inert implementation so the
/// wiring and tests remain meaningful.
/// </summary>
public static class SystemMediaControlsFactory
{
    public static ISystemMediaControls Create() => NullSystemMediaControls.Instance;
}
