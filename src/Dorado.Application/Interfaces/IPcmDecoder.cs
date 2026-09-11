namespace Dorado.Application.Interfaces;

/// <summary>Decoded mono PCM samples plus their sample rate.</summary>
public sealed record PcmSamples(float[] Mono, int SampleRate);

/// <summary>
/// Decodes a bounded window of an audio file to mono float PCM for on-device
/// audio-feature analysis. Implemented over the real decoder in Infrastructure;
/// returning null/throws simply falls back to the metadata prior.
/// </summary>
public interface IPcmDecoder
{
    /// <summary>
    /// Decodes up to <paramref name="maxDuration"/> of mono PCM (samples in [-1, 1]),
    /// or null when the file cannot be decoded.
    /// </summary>
    Task<PcmSamples?> DecodeMonoAsync(
        string filePath,
        TimeSpan maxDuration,
        CancellationToken cancellationToken = default);
}
