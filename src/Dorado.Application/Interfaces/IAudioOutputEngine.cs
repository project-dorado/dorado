namespace Dorado.Application.Interfaces;

/// <summary>
/// The real-audio output boundary. The coordinator owns queue/state logic; this engine
/// owns decoding, mixing, output, crossfade timing, and FFT data.
/// All methods are safe to call when <see cref="IsAvailable"/> is false (no-ops).
/// </summary>
public interface IAudioOutputEngine : IDisposable
{
    bool IsAvailable { get; }

    /// <summary>
    /// True when the engine currently holds a decodable source and can report position/duration.
    /// Lets the coordinator fall back to the simulated clock when a source fails to open even
    /// though the output device initialized.
    /// </summary>
    bool HasActiveSource { get; }

    /// <summary>Replaces the audible output with the given file path or stream URL and begins playback.</summary>
    void LoadAndPlay(string sourceUri);

    void Play();

    void Pause();

    void Stop();

    void SetVolume(double volume, bool muted);

    void Seek(TimeSpan position);

    TimeSpan GetPosition();

    TimeSpan GetDuration();

    /// <summary>Returns 24 normalized (0..1) spectrum band magnitudes, or an empty array.</summary>
    float[] GetFftData();

    /// <summary>
    /// Schedules a seamless transition into <paramref name="nextSourceUri"/>:
    /// overlap/crossfade when <paramref name="crossfadeSeconds"/> &gt; 0, sample-boundary gapless otherwise.
    /// Passing null cancels a pending transition.
    /// </summary>
    void SetAutoTransition(string? nextSourceUri, double crossfadeSeconds);

    /// <summary>The audible output reached its natural end with no prepared successor.</summary>
    event EventHandler? TrackEnded;

    /// <summary>A scheduled transition became audible (fade-in started or gapless boundary crossed).</summary>
    event EventHandler? TrackTransitioned;
}
