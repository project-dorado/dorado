namespace Dorado.Application.Services;

/// <summary>Named 10-band EQ presets (bands 31 Hz → 16 kHz).</summary>
public static class EqualizerPresets
{
    public const string Flat = "Flat";

    private static readonly Dictionary<string, double[]> Presets = new(StringComparer.OrdinalIgnoreCase)
    {
        [Flat] = new double[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
        ["Rock"] = new double[] { 5, 4, 2, -1, -2, 0, 2, 3, 4, 4 },
        ["Pop"] = new double[] { -1, 0, 2, 4, 4, 3, 0, -1, -1, -1 },
        ["Jazz"] = new double[] { 3, 2, 1, 2, -1, -1, 0, 1, 2, 3 },
        ["Classical"] = new double[] { 4, 3, 2, 1, -1, -1, 0, 2, 3, 4 },
        ["Bass Boost"] = new double[] { 7, 6, 5, 3, 1, 0, 0, 0, 0, 0 },
        ["Treble Boost"] = new double[] { 0, 0, 0, 0, 0, 1, 3, 5, 6, 7 },
        ["Vocal"] = new double[] { -2, -1, 1, 3, 4, 4, 3, 1, 0, -1 }
    };

    public static IReadOnlyList<string> Names { get; } = new[] { "Flat", "Rock", "Pop", "Jazz", "Classical", "Bass Boost", "Treble Boost", "Vocal" };

    public static IReadOnlyList<double> GetGains(string? preset)
        => preset is not null && Presets.TryGetValue(preset, out var gains) ? gains : Presets[Flat];
}
