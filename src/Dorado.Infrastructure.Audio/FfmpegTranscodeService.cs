using System.Diagnostics;
using System.Globalization;
using System.Text;
using Dorado.Application.Interfaces;

namespace Dorado.Infrastructure.Audio;

/// <summary>
/// FFmpeg-backed transcoder. Maps a target container to a codec profile and
/// runs <c>ffmpeg -i source -vn -c:a … output.ext</c>. Progress is derived from
/// FFmpeg's <c>-progress pipe:1</c> stream against the duration reported by
/// <c>ffprobe</c> (when available); otherwise only completion is reported.
/// </summary>
public sealed class FfmpegTranscodeService : ITranscodeService
{
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;
    private readonly Lazy<bool> _available;

    public FfmpegTranscodeService(string ffmpegPath = "ffmpeg", string ffprobePath = "ffprobe")
    {
        _ffmpegPath = ffmpegPath;
        _ffprobePath = ffprobePath;
        _available = new Lazy<bool>(ProbeAvailability);
    }

    public bool IsAvailable => _available.Value;

    public async Task<TranscodeResult> TranscodeAsync(
        string sourcePath,
        string targetContainer,
        string outputDirectory,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return new TranscodeResult(false, null, "FFmpeg is not available.");
        }

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return new TranscodeResult(false, null, $"Source not found: {sourcePath}");
        }

        Directory.CreateDirectory(outputDirectory);
        var extension = NormalizeContainer(targetContainer);
        var outputPath = Path.Combine(
            outputDirectory,
            $"{Path.GetFileNameWithoutExtension(sourcePath)}-{Guid.NewGuid():N}.{extension}");

        var seconds = await TryGetDurationSecondsAsync(sourcePath, cancellationToken).ConfigureAwait(false);

        var arguments =
            $"-hide_banner -nostdin -y -i \"{sourcePath}\" -vn {CodecArgs(extension)} " +
            $"-progress pipe:1 -nostats \"{outputPath}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var process = new Process { StartInfo = startInfo };
            var errorBuilder = new StringBuilder();
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };

            if (!process.Start())
            {
                return new TranscodeResult(false, null, "Failed to start FFmpeg.");
            }

            process.BeginErrorReadLine();

            while (true)
            {
                var line = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                if (progress is not null && seconds > 0 && line.StartsWith("out_time_us=", StringComparison.Ordinal))
                {
                    var microseconds = line["out_time_us=".Length..];
                    if (long.TryParse(microseconds, NumberStyles.Integer, CultureInfo.InvariantCulture, out var us) && us > 0)
                    {
                        progress.Report(Math.Clamp(us / 1_000_000.0 / seconds, 0.0, 1.0));
                    }
                }
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            if (process.ExitCode != 0 || !File.Exists(outputPath))
            {
                return new TranscodeResult(false, null, errorBuilder.Length > 0 ? errorBuilder.ToString().Trim() : $"FFmpeg exited with {process.ExitCode}.");
            }

            progress?.Report(1.0);
            return new TranscodeResult(true, outputPath, null);
        }
        catch (Exception ex)
        {
            return new TranscodeResult(false, null, ex.Message);
        }
    }

    /// <summary>Container → codec profile. Mirrors the device-playable targets.</summary>
    public static string CodecArgs(string container) => container switch
    {
        "m4a" or "aac" => "-c:a aac -b:a 256k",
        "mp3" => "-c:a libmp3lame -b:a 320k",
        "flac" => "-c:a flac",
        "ogg" => "-c:a libvorbis -q:a 6",
        "opus" => "-c:a libopus -b:a 192k",
        _ => "-c:a aac -b:a 256k",
    };

    public static string NormalizeContainer(string container)
        => container.Trim().TrimStart('.').ToLowerInvariant() is { Length: > 0 } value ? value : "m4a";

    private bool ProbeAvailability()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            process.WaitForExit(5000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<double> TryGetDurationSecondsAsync(string sourcePath, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _ffprobePath,
                Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{sourcePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return 0;
            }

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) ? seconds : 0;
        }
        catch
        {
            return 0;
        }
    }
}
