namespace Dorado.Application.Interfaces;

/// <summary>
/// Reads ReplayGain metadata from audio files on disk.
/// </summary>
public interface IReplayGainService
{
    /// <summary>
    /// Returns the REPLAYGAIN_TRACK_GAIN value in decibels for the given file, or null when absent/unsupported.
    /// </summary>
    double? ReadTrackGainDb(string filePath);
}
