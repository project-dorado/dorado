namespace Dorado.Application.Interfaces;

/// <summary>A resolved AcoustID recording match (best-scoring fingerprint hit).</summary>
public sealed record AcoustIdMatch(
    string? RecordingId,
    string? Title,
    string? Artist,
    string? Album,
    int Score);

/// <summary>
/// Optional scan-time metadata enrichment + acoustic duplicate detection via
/// AcoustID (chromaprint fingerprint). <see cref="IsConfigured"/> is false when
/// no API key is set, so the scan simply skips it.
/// </summary>
public interface IAcoustIdService
{
    bool IsConfigured { get; }

    /// <summary>Fingerprints <paramref name="filePath"/> and looks it up; null on no match/unconfigured.</summary>
    Task<AcoustIdMatch?> LookupAsync(string filePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Produces a chromaprint fingerprint for a file. Implemented over the external
/// <c>fpcalc</c> tool; a stub is used in tests. Returns null when unavailable.
/// </summary>
public interface IFingerprintProvider
{
    Task<(double Duration, string Fingerprint)?> GetFingerprintAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
