using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;

namespace Dorado.Infrastructure.Audio;

/// <summary>Real process runner used by the optical pipeline (and any other tool invocation).</summary>
public sealed class SystemProcessRunner : IProcessRunner
{
    public bool Exists(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        if (Path.IsPathRooted(fileName)) return File.Exists(fileName);

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrEmpty(dir)) continue;
            try
            {
                if (File.Exists(Path.Combine(dir, fileName))) return true;
            }
            catch
            {
                // ignore malformed PATH entries
            }
        }
        return false;
    }

    public async Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default,
        Action<string>? onStandardOutputLine = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in arguments) psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            stdout.AppendLine(e.Data);
            onStandardOutputLine?.Invoke(e.Data);
        };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            throw;
        }

        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}

/// <summary>
/// Audio CD rip/burn backed by the platform toolchain (cdparanoia + ffmpeg +
/// cdrdao on Linux). Availability is capability-gated: with no optical drive or
/// toolchain present it reports unavailable and the UI keeps its simulated path.
/// </summary>
public sealed class ProcessOpticalDriveService : IOpticalDriveService
{
    private readonly IProcessRunner _runner;
    private readonly Func<bool> _drivePresent;
    private readonly string _devicePath;

    public ProcessOpticalDriveService(
        IProcessRunner runner,
        Func<bool>? drivePresent = null,
        string devicePath = "/dev/sr0")
    {
        _runner = runner;
        _drivePresent = drivePresent ?? (() => File.Exists("/dev/sr0") || File.Exists("/dev/cdrom"));
        _devicePath = devicePath;
    }

    public bool IsAvailable => _drivePresent() && _runner.Exists("cdparanoia");

    public string CapabilitySummary =>
        $"drive={(_drivePresent() ? "present" : "absent")} cdparanoia={_runner.Exists("cdparanoia")} " +
        $"ffmpeg={_runner.Exists("ffmpeg")} cdrdao={_runner.Exists("cdrdao")} device={_devicePath}";

    public async Task<IReadOnlyList<OpticalTrack>> ReadTocAsync(CancellationToken cancellationToken = default)
    {
        EnsureAvailable();
        var result = await _runner.RunAsync("cdparanoia", new[] { "-Q" }, cancellationToken);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"cdparanoia -Q failed (exit {result.ExitCode}): {result.StandardError.Trim()}");
        }
        return ParseToc(result.StandardOutput);
    }

    public async Task<string> RipTrackAsync(
        OpticalTrack track,
        string destinationFolder,
        string format,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureAvailable();
        Directory.CreateDirectory(destinationFolder);

        var safeTitle = Sanitize(track.Title);
        var stem = Path.Combine(destinationFolder, $"{track.Number:00} - {safeTitle}");
        var wavPath = stem + ".wav";
        var targetPath = stem + "." + ExtensionFor(format);

        // Stage 1: extract PCM (cdparanoia handles jitter/error correction).
        var rip = await _runner.RunAsync(
            "cdparanoia",
            new[] { "-w", track.Number.ToString(CultureInfo.InvariantCulture), wavPath, _devicePath },
            cancellationToken,
            line =>
            {
                var pct = ParsePercent(line);
                if (pct.HasValue) progress?.Report(pct.Value * 0.7);
            });
        if (!rip.Succeeded)
        {
            throw new InvalidOperationException($"cdparanoia rip failed (exit {rip.ExitCode}): {rip.StandardError.Trim()}");
        }

        if (string.Equals(ExtensionFor(format), "wav", StringComparison.OrdinalIgnoreCase))
        {
            progress?.Report(1.0);
            return wavPath;
        }

        // Stage 2: encode with ffmpeg.
        if (!_runner.Exists("ffmpeg"))
        {
            throw new InvalidOperationException("ffmpeg is required to encode the ripped audio.");
        }

        var codecArgs = string.Equals(ExtensionFor(format), "flac", StringComparison.OrdinalIgnoreCase)
            ? new[] { "-c:a", "flac", "-compression_level", "5" }
            : new[] { "-c:a", "libmp3lame", "-q:a", "0" };
        var ffmpegArgs = new List<string> { "-y", "-i", wavPath, "-map_metadata", "-1" };
        ffmpegArgs.AddRange(codecArgs);
        ffmpegArgs.Add(targetPath);

        var encode = await _runner.RunAsync(
            "ffmpeg",
            ffmpegArgs,
            cancellationToken,
            line =>
            {
                var pct = ParsePercent(line);
                if (pct.HasValue) progress?.Report(0.7 + (pct.Value * 0.3));
            });
        if (!encode.Succeeded)
        {
            throw new InvalidOperationException($"ffmpeg encode failed (exit {encode.ExitCode}): {encode.StandardError.Trim()}");
        }

        progress?.Report(1.0);
        return targetPath;
    }

    public async Task BurnAsync(
        IReadOnlyList<string> trackFilePaths,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureAvailable();
        if (trackFilePaths.Count == 0)
        {
            throw new InvalidOperationException("No tracks queued to burn.");
        }
        if (!_runner.Exists("cdrdao"))
        {
            throw new InvalidOperationException("cdrdao is required for burning an Audio CD.");
        }

        var tocPath = Path.Combine(Path.GetTempPath(), $"dorado-burn-{Guid.NewGuid():N}.toc");
        await File.WriteAllTextAsync(tocPath, BuildCdrdaoToc(trackFilePaths), cancellationToken);

        progress?.Report(0.05);
        var burn = await _runner.RunAsync(
            "cdrdao",
            new[] { "write", "--device", _devicePath, "--speed", "16", tocPath },
            cancellationToken,
            line =>
            {
                var pct = ParsePercent(line);
                if (pct.HasValue) progress?.Report(0.05 + (pct.Value * 0.95));
            });
        if (!burn.Succeeded)
        {
            throw new InvalidOperationException($"cdrdao burn failed (exit {burn.ExitCode}): {burn.StandardError.Trim()}");
        }
        progress?.Report(1.0);
    }

    private void EnsureAvailable()
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException(
                $"No optical drive / ripping toolchain available. {CapabilitySummary}");
        }
    }

    internal static string ExtensionFor(string format)
    {
        var f = (format ?? string.Empty).ToLowerInvariant();
        if (f.Contains("flac")) return "flac";
        if (f.Contains("mp3")) return "mp3";
        if (f.Contains("wav")) return "wav";
        return "flac";
    }

    internal static string Sanitize(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(title.Length);
        foreach (var c in title)
        {
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        }
        var cleaned = sb.ToString().Trim();
        return cleaned.Length == 0 ? "Track" : cleaned;
    }

    internal static string BuildCdrdaoToc(IReadOnlyList<string> trackFilePaths)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CD_DA");
        foreach (var path in trackFilePaths)
        {
            sb.AppendLine("TRACK AUDIO");
            sb.AppendLine($"FILE \"{path}\" 0");
        }
        return sb.ToString();
    }

    /// <summary>Parses <c>cdparanoia -Q</c> TOC output into tracks with computed durations.</summary>
    internal static IReadOnlyList<OpticalTrack> ParseToc(string output)
    {
        var starts = new List<(int Number, int StartSector)>();
        int? leadOut = null;

        foreach (var raw in output.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var total = Regex.Match(line, @"^\s*TOTAL\s+(\d+):(\d+)\.(\d+)");
            if (total.Success)
            {
                leadOut = FramesToSectors(total.Groups[1], total.Groups[2], total.Groups[3]);
                continue;
            }

            var track = Regex.Match(line, @"^\s*(\d+)\.\s+(\d+):(\d+)\.(\d+)");
            if (track.Success)
            {
                var number = int.Parse(track.Groups[1].Value, CultureInfo.InvariantCulture);
                var start = FramesToSectors(track.Groups[2], track.Groups[3], track.Groups[4]);
                starts.Add((number, start));
            }
        }

        if (starts.Count == 0) return Array.Empty<OpticalTrack>();
        starts.Sort((a, b) => a.Number.CompareTo(b.Number));

        var tracks = new List<OpticalTrack>(starts.Count);
        for (int i = 0; i < starts.Count; i++)
        {
            var start = starts[i].StartSector;
            var end = i + 1 < starts.Count ? starts[i + 1].StartSector : leadOut ?? start;
            var length = Math.Max(0, end - start);
            tracks.Add(new OpticalTrack(
                starts[i].Number,
                start,
                length,
                TimeSpan.FromSeconds(length / 75.0),
                $"Track {starts[i].Number:00}"));
        }
        return tracks;
    }

    private static int FramesToSectors(Group minutes, Group seconds, Group frames)
        => (int.Parse(minutes.Value, CultureInfo.InvariantCulture) * 60 + int.Parse(seconds.Value, CultureInfo.InvariantCulture)) * 75
           + int.Parse(frames.Value, CultureInfo.InvariantCulture);

    private static double? ParsePercent(string line)
    {
        // cdparanoia "-w" prints e.g. "##: 42.3%" ; ffmpeg prints "time=00:00:12.34"
        var pct = Regex.Match(line, @"([0-9]+(?:\.[0-9]+)?)\s*%");
        if (pct.Success && double.TryParse(pct.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return Math.Clamp(value / 100.0, 0, 1);
        }
        return null;
    }
}
