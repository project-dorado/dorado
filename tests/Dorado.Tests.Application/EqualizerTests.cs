using Dorado.Application.Services;
using Dorado.Infrastructure.Audio;

namespace Dorado.Tests.Application;

public class EqualizerTests
{
    [Fact]
    public void Flat_preset_is_identity()
    {
        var equalizer = new Equalizer(44100, 1);
        var input = new float[512];
        for (var i = 0; i < input.Length; i++)
        {
            input[i] = (float)Math.Sin(2 * Math.PI * 440 * i / 44100.0) * 0.5f;
        }

        var output = (float[])input.Clone();
        equalizer.Process(output, output.Length);

        for (var i = 0; i < input.Length; i++)
        {
            Assert.InRange(output[i], input[i] - 1e-4f, input[i] + 1e-4f);
        }
    }

    [Fact]
    public void Bass_boost_raises_low_frequency_energy()
    {
        var equalizer = new Equalizer(44100, 1);
        var gains = new double[10];
        gains[1] = 9; // +9 dB at 62 Hz
        equalizer.SetGains(gains, 0);

        var input = new float[8192];
        for (var i = 0; i < input.Length; i++)
        {
            input[i] = (float)Math.Sin(2 * Math.PI * 62 * i / 44100.0) * 0.25f;
        }

        var output = (float[])input.Clone();
        equalizer.Process(output, output.Length);

        Assert.True(Rms(output) > Rms(input) * 1.3);
    }

    [Fact]
    public void Presets_are_ten_bands_and_unknown_falls_back_to_flat()
    {
        foreach (var name in EqualizerPresets.Names)
        {
            Assert.Equal(10, EqualizerPresets.GetGains(name).Count);
        }

        Assert.All(EqualizerPresets.GetGains("does-not-exist"), gain => Assert.Equal(0, gain));
    }

    private static double Rms(float[] buffer)
    {
        double sum = 0;
        foreach (var sample in buffer)
        {
            sum += sample * sample;
        }

        return Math.Sqrt(sum / buffer.Length);
    }
}
