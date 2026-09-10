namespace Dorado.Application.Interfaces;

/// <summary>
/// Transcodes an audio source to a device-playable container, aligned with
/// <c>Dorado.Domain.Models.MediaFormats</c>. The default implementation shells
/// out to FFmpeg; when FFmpeg is absent, <see cref="IsAvailable"/> is false and
/// the sync pipeline copies files verbatim.
/// </summary>
public interface ITranscodeService
{
    /// <summary>True when the backend (e.g. FFmpeg) is present and runnable.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Transcodes <paramref name="sourcePath"/> into <paramref name="outputDirectory"/>
    /// as <paramref name="targetContainer"/> (e.g. "m4a", "mp3", "flac").
    /// </summary>
    Task<TranscodeResult> TranscodeAsync(
        string sourcePath,
        string targetContainer,
        string outputDirectory,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed record TranscodeResult(bool Success, string? OutputPath, string? Error);
