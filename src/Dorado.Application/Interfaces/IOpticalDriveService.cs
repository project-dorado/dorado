using Dorado.Application.Models;

namespace Dorado.Application.Interfaces;

/// <summary>
/// Thin seam over external process execution so the optical-drive pipeline is
/// unit-testable without a real drive or toolchain.
/// </summary>
public interface IProcessRunner
{
    /// <summary>True when an executable is resolvable on PATH (or the given absolute path exists).</summary>
    bool Exists(string fileName);

    Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default,
        Action<string>? onStandardOutputLine = null);
}

/// <summary>
/// Capability-gated Audio CD rip/burn pipeline (Linux: cdparanoia + ffmpeg +
/// cdrdao/wodim; Windows: IMAPI2 — not yet wired). When no drive or toolchain is
/// present, <see cref="IsAvailable"/> is false and the caller keeps its
/// honest simulated path.
/// </summary>
public interface IOpticalDriveService
{
    bool IsAvailable { get; }

    /// <summary>Human-readable summary of the detected drive/toolchain (for diagnostics).</summary>
    string CapabilitySummary { get; }

    Task<IReadOnlyList<OpticalTrack>> ReadTocAsync(CancellationToken cancellationToken = default);

    /// <summary>Rips one track to <paramref name="destinationFolder"/>; returns the output file path.</summary>
    Task<string> RipTrackAsync(
        OpticalTrack track,
        string destinationFolder,
        string format,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    Task BurnAsync(
        IReadOnlyList<string> trackFilePaths,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
