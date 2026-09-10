namespace Dorado.Infrastructure.Audio;

/// <summary>
/// 10-band peaking-EQ (RBJ biquads) applied as a BASS DSP on the mixer output.
/// Pure managed DSP — no native bass_fx dependency.
/// </summary>
public sealed class Equalizer
{
    public static readonly double[] CenterFrequencies = { 31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };

    private const double Q = 1.0;

    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly Band[] _bands;
    private readonly double[] _gainsDb = new double[CenterFrequencies.Length];
    private double _preampLinear = 1.0;

    public Equalizer(int sampleRate, int channels)
    {
        _sampleRate = sampleRate;
        _channels = channels;
        _bands = new Band[CenterFrequencies.Length];
        for (var i = 0; i < _bands.Length; i++)
        {
            _bands[i] = new Band(channels);
        }

        SetGains(_gainsDb, 0);
    }

    public IReadOnlyList<double> GainsDb => _gainsDb;

    public void SetGains(IReadOnlyList<double> gainsDb, double preampDb)
    {
        _preampLinear = Math.Pow(10, preampDb / 20.0);
        for (var i = 0; i < _bands.Length; i++)
        {
            var gain = i < gainsDb.Count ? gainsDb[i] : 0;
            _gainsDb[i] = gain;
            SetPeakingCoefficients(_bands[i], CenterFrequencies[i], gain);
        }
    }

    public void Reset()
    {
        foreach (var band in _bands)
        {
            band.Reset();
        }
    }

    /// <summary>Processes interleaved float samples in place.</summary>
    public void Process(float[] buffer, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var sample = buffer[i] * _preampLinear;
            var channel = i % _channels;
            for (var b = 0; b < _bands.Length; b++)
            {
                sample = _bands[b].Process(sample, channel);
            }

            buffer[i] = (float)sample;
        }
    }

    private void SetPeakingCoefficients(Band band, double frequency, double gainDb)
    {
        var a = Math.Pow(10, gainDb / 40.0);
        var w0 = 2 * Math.PI * frequency / _sampleRate;
        var alpha = Math.Sin(w0) / (2 * Q);
        var cosW0 = Math.Cos(w0);

        var b0 = 1 + alpha * a;
        var b1 = -2 * cosW0;
        var b2 = 1 - alpha * a;
        var a0 = 1 + alpha / a;
        var a1 = -2 * cosW0;
        var a2 = 1 - alpha / a;

        band.SetCoefficients(b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
    }

    private sealed class Band
    {
        private readonly double[] _x1;
        private readonly double[] _x2;
        private readonly double[] _y1;
        private readonly double[] _y2;

        private double _b0 = 1, _b1, _b2, _a1, _a2;

        public Band(int channels)
        {
            _x1 = new double[channels];
            _x2 = new double[channels];
            _y1 = new double[channels];
            _y2 = new double[channels];
        }

        public void SetCoefficients(double b0, double b1, double b2, double a1, double a2)
        {
            _b0 = b0;
            _b1 = b1;
            _b2 = b2;
            _a1 = a1;
            _a2 = a2;
        }

        public double Process(double x, int channel)
        {
            var y = _b0 * x + _b1 * _x1[channel] + _b2 * _x2[channel] - _a1 * _y1[channel] - _a2 * _y2[channel];
            _x2[channel] = _x1[channel];
            _x1[channel] = x;
            _y2[channel] = _y1[channel];
            _y1[channel] = y;
            return y;
        }

        public void Reset()
        {
            Array.Clear(_x1);
            Array.Clear(_x2);
            Array.Clear(_y1);
            Array.Clear(_y2);
        }
    }
}
