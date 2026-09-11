using System.Diagnostics;
using System.Text.Json;
using Dorado.Application.Interfaces;

namespace Dorado.Infrastructure.External;

/// <summary>
/// Chromaprint fingerprint provider backed by the external <c>fpcalc</c> tool
/// (shipped with Chromaprint). Returns null when the tool is missing or fails,
/// so AcoustID enrichment degrades to a no-op.
/// </summary>
public sealed class FpcalcFingerprintProvider : IFingerprintProvider
{
    private readonly string _fpcalcPath;

    public FpcalcFingerprintProvider(string fpcalcPath = "fpcalc") => _fpcalcPath = fpcalcPath;

    public async Task<(double Duration, string Fingerprint)?> GetFingerprintAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _fpcalcPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("-json");
            startInfo.ArgumentList.Add("-length");
            startInfo.ArgumentList.Add("120");
            startInfo.ArgumentList.Add(filePath);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            {
                return null;
            }

            using var document = JsonDocument.Parse(stdout);
            var root = document.RootElement;
            var fingerprint = root.TryGetProperty("fingerprint", out var f) ? f.GetString() : null;
            var duration = root.TryGetProperty("duration", out var d) && d.TryGetDouble(out var parsed) ? parsed : 0;

            return string.IsNullOrWhiteSpace(fingerprint) ? null : (duration, fingerprint!);
        }
        catch
        {
            return null;
        }
    }
}
