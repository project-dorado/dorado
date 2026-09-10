using System.Diagnostics;
using System.Runtime.InteropServices;
using Dorado.Application.Interfaces;

namespace Dorado.Infrastructure.Audio;

public class SoundEffectService : ISoundEffectService
{
    private readonly string _soundsDirectory;
    public bool SoundEffectsEnabled { get; set; } = true;

    public SoundEffectService()
    {
        // Try multiple locations for sound assets
        var appBase = AppContext.BaseDirectory;
        var candidate1 = Path.Combine(appBase, "Assets", "Zune", "Sounds");
        var candidate2 = Path.Combine(Directory.GetCurrentDirectory(), "src", "Dorado.UI", "Assets", "Zune", "Sounds");

        if (Directory.Exists(candidate1))
        {
            _soundsDirectory = candidate1;
        }
        else if (Directory.Exists(candidate2))
        {
            _soundsDirectory = candidate2;
        }
        else
        {
            _soundsDirectory = candidate1;
        }
    }

    // Clean-room synthesized chimes (see tools/gen_sounds.py); the legacy
    // Microsoft COMPLETEDSYNCBURNCD/DOWNLOAD/COMPLETEDRIPREVERSESYNC/INBOX assets
    // are no longer bundled.
    public void PlaySyncComplete() => PlaySound("DORADO-CHIME-SYNC.WAV");
    public void PlayDownloadComplete() => PlaySound("DORADO-CHIME-DOWNLOAD.WAV");
    public void PlayRipComplete() => PlaySound("DORADO-CHIME-RIP.WAV");
    public void PlayBurnComplete() => PlaySound("DORADO-CHIME-SYNC.WAV");
    public void PlayNotification() => PlaySound("DORADO-CHIME-INBOX.WAV");

    private void PlaySound(string filename)
    {
        if (!SoundEffectsEnabled) return;

        try
        {
            var soundPath = Path.Combine(_soundsDirectory, filename);
            if (!File.Exists(soundPath))
            {
                // Fallback check in parent directories
                var current = Directory.GetCurrentDirectory();
                var alt = Path.Combine(current, "src", "Dorado.UI", "Assets", "Zune", "Sounds", filename);
                if (File.Exists(alt)) soundPath = alt;
            }

            if (!File.Exists(soundPath)) return;

            Task.Run(() =>
            {
                try
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        using var proc = Process.Start(new ProcessStartInfo
                        {
                            FileName = "powershell",
                            Arguments = $"-NoProfile -NonInteractive -Command \"(New-Object Media.SoundPlayer '{soundPath.Replace("'", "''")}').PlaySync()\"",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        });
                        proc?.WaitForExit(3000);
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        using var proc = Process.Start(new ProcessStartInfo
                        {
                            FileName = "afplay",
                            Arguments = $"\"{soundPath}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                        proc?.WaitForExit(3000);
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        var player = FindLinuxPlayer();
                        if (player != null)
                        {
                            using var proc = Process.Start(new ProcessStartInfo
                            {
                                FileName = player,
                                Arguments = $"\"{soundPath}\"",
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            });
                            proc?.WaitForExit(3000);
                        }
                    }
                }
                catch
                {
                    // Non-critical background audio playback failure
                }
            });
        }
        catch
        {
            // Suppress background sound playback error
        }
    }

    private static string? FindLinuxPlayer()
    {
        string[] players = ["pw-play", "paplay", "aplay"];
        foreach (var p in players)
        {
            try
            {
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = p,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                });
                proc?.WaitForExit(500);
                if (proc?.ExitCode == 0) return p;
            }
            catch
            {
            }
        }
        return null;
    }
}
