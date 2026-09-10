namespace Dorado.Application.Interfaces;

/// <summary>
/// Optional playback-engine capability: a 10-band equalizer. Engines that cannot
/// equalize simply do not implement it; callers must null-check.
/// </summary>
public interface IEqualizerControl
{
    void ApplyEqualizer(bool enabled, IReadOnlyList<double> bandGainsDb, double preampDb);
}
