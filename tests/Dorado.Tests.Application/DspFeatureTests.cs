using Dorado.Application.Services;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Unit tests for the real DSP feature extractor (Phase 4): synthetic PCM with
/// known spectral/energy characteristics, asserted against the six dimensions.
/// </summary>
public sealed class DspFeatureTests
{
    private const int Rate = 44100;

    [Fact]
    public void Silence_HasNearZeroEnergyAndTempoDefault()
    {
        var features = DspFeatureExtractor.Extract(Guid.NewGuid(), Silence(2), Rate);

        Assert.True(features.Energy <= 0.05, $"energy was {features.Energy}");
        Assert.InRange(features.Bpm, 60, 200);
    }

    [Fact]
    public void Tone_ProducesNormalizedFeaturesInRange()
    {
        var features = DspFeatureExtractor.Extract(Guid.NewGuid(), Sine(440, 2), Rate);

        Assert.InRange(features.Energy, 0.0, 1.0);
        Assert.InRange(features.Valence, 0.0, 1.0);
        Assert.InRange(features.Acousticness, 0.0, 1.0);
        Assert.InRange(features.Danceability, 0.0, 1.0);
        Assert.InRange(features.SpectralCentroid, 0.0, 1.0);
        Assert.InRange(features.Bpm, 60, 200);
        Assert.True(features.Energy > 0.05, "a 440 Hz tone should carry energy");
    }

    [Fact]
    public void Noise_HasHigherSpectralCentroidThanPureTone()
    {
        var tone = DspFeatureExtractor.Extract(Guid.NewGuid(), Sine(440, 2), Rate);
        var noise = DspFeatureExtractor.Extract(Guid.NewGuid(), WhiteNoise(2, seed: 12345), Rate);

        Assert.True(
            noise.SpectralCentroid > tone.SpectralCentroid + 0.1,
            $"noise centroid {noise.SpectralCentroid} should exceed tone centroid {tone.SpectralCentroid}");
    }

    [Fact]
    public void TooShortInput_FallsBackToTheMetadataPrior()
    {
        var features = DspFeatureExtractor.Extract(Guid.NewGuid(), new float[100], Rate);

        Assert.InRange(features.Energy, 0.0, 1.0);
        Assert.InRange(features.Bpm, 60, 200);
    }

    private static float[] Silence(double seconds)
        => new float[(int)(Rate * seconds)];

    private static float[] Sine(double frequency, double seconds, float amplitude = 0.5f)
    {
        var samples = new float[(int)(Rate * seconds)];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = amplitude * (float)Math.Sin(2 * Math.PI * frequency * i / Rate);
        }

        return samples;
    }

    private static float[] WhiteNoise(double seconds, ulong seed)
    {
        // Deterministic xorshift so the test is reproducible.
        var samples = new float[(int)(Rate * seconds)];
        ulong state = seed;
        for (var i = 0; i < samples.Length; i++)
        {
            state ^= state << 13;
            state ^= state >> 7;
            state ^= state << 17;
            samples[i] = (float)((((state >> 11) / (double)(1UL << 53)) - 0.5) * 0.8);
        }

        return samples;
    }
}
