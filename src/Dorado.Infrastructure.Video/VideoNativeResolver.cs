using System.Runtime.InteropServices;

namespace Dorado.Infrastructure.Video;

/// <summary>
/// Resolves libvlc/libvlccore DllImports to system-installed VLC 3.x libraries on Linux
/// (distros ship versioned sonames like libvlc.so.5 without an unversioned symlink).
/// On Windows the VideoLAN.LibVLC.Windows package ships natives next to the app, so the
/// default probing already succeeds.
/// </summary>
public static class VideoNativeResolver
{
    private static bool _registered;
    private static readonly object Gate = new();

    public static void Register()
    {
        lock (Gate)
        {
            if (_registered || !OperatingSystem.IsLinux())
            {
                _registered = true;
                return;
            }

            _registered = true;

            try
            {
                NativeLibrary.SetDllImportResolver(typeof(LibVLCSharp.Shared.LibVLC).Assembly, Resolve);
            }
            catch
            {
                // Resolver registration is best-effort; default probing may still succeed.
            }
        }
    }

    private static IntPtr Resolve(string libraryName, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != "libvlc" && libraryName != "libvlccore")
        {
            return IntPtr.Zero;
        }

        foreach (var candidate in CandidatePaths(libraryName))
        {
            if (NativeLibrary.TryLoad(candidate, out var handle))
            {
                return handle;
            }
        }

        return IntPtr.Zero;
    }

    private static IEnumerable<string> CandidatePaths(string libraryName)
    {
        var isCore = libraryName == "libvlccore";
        var versioned = isCore ? "libvlccore.so.9" : "libvlc.so.5";
        var architectures = new[]
        {
            Environment.Is64BitProcess ? "x86_64-linux-gnu" : "i386-linux-gnu",
            Environment.Is64BitProcess ? "x86_64" : "i686",
            "aarch64-linux-gnu"
        };

        foreach (var arch in architectures)
        {
            yield return $"/usr/lib/{arch}/{versioned}";
        }

        yield return $"/lib64/{versioned}";
        yield return $"/usr/lib64/{versioned}";
        yield return $"/usr/lib/{versioned}";
    }
}
