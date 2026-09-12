using Dorado.Application.Interfaces;
using Dorado.Application.Services;

namespace Dorado.Infrastructure.Audio;

/// <summary>
/// Selects the OS media-controls implementation by capability. On Linux with a
/// session bus the real MPRIS2 server is used; everything else falls back to the
/// inert no-op (Windows SMTC requires a Windows-targeted TFM and remains future work).
/// </summary>
public static class SystemMediaControlsFactory
{
    public static ISystemMediaControls Create()
    {
        if (OperatingSystem.IsLinux()
            && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS")))
        {
            try
            {
                var mpris = new MprisMediaControls();
                if (mpris.IsAvailable)
                {
                    return mpris;
                }
            }
            catch
            {
                // fall through to the null implementation
            }
        }

        return NullSystemMediaControls.Instance;
    }
}
