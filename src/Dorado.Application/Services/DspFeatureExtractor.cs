using Dorado.Domain.Models;

namespace Dorado.Application.Services;

/// <summary>
/// Computes the six audio-feature dimensions from decoded PCM with real signal
/// processing: short-time FFT spectral centroid, RMS energy, zero-crossing rate,
/// and an onset-envelope autocorrelation tempo estimate. Deterministic and pure,
/// so the DSP core is unit-testable without a decoder or audio device.
///
/// The mapping from DSP measurements to the perceptual dimensions (valence,
/// danceability, acousticness) is a documented heuristic — the goal is a stable,
/// comparable vector, not perceptual ground truth.
/// </summary>
public static class DspFeatureExtractor
{
    private const int FrameSize = 2048;
    private const int Hop = 1024;

    public static AudioFeatures Extract(Guid trackId, IReadOnlyList<float> mono, int sampleRate)
    {
        if (mono.Count < FrameSize || sampleRate <= 0)
        {
            return AudioFeatureExtractor.Extract(new Track { Id = trackId });
        }

        var window = Hann(FrameSize);
        var re = new double[FrameSize];
        var im = new double[FrameSize];
        var magnitudes = new double[FrameSize / 2 + 1];

        double rmsSum = 0;
        double centroidSum = 0;
        double zcrSum = 0;
        var frames = 0;
        var envelope = new List<double>((mono.Count - FrameSize) / Hop + 1);

        for (var start = 0; start + FrameSize <= mono.Count; start += Hop)
        {
            double rms = 0;
            var crossings = 0;
            for (var i = 0; i < FrameSize; i++)
            {
                var sample = mono[start + i];
                var previous = i == 0 ? mono[start] : mono[start + i - 1];
                if ((sample >= 0 && previous < 0) || (sample < 0 && previous >= 0))
                {
                    crossings++;
                }

                var windowed = sample * window[i];
                re[i] = windowed;
                im[i] = 0;
                rms += sample * sample;
            }

            Fft(re, im);

            var magSum = 0.0;
            var weighted = 0.0;
            for (var k = 1; k < magnitudes.Length; k++)
            {
                var mag = Math.Sqrt((re[k] * re[k]) + (im[k] * im[k]));
                magnitudes[k] = mag;
                magSum += mag;
                weighted += k * mag;
            }

            var frameRms = Math.Sqrt(rms / FrameSize);
            envelope.Add(frameRms);
            rmsSum += frameRms;
            centroidSum += magSum > 0 ? (weighted / magSum) / (FrameSize / 2.0) : 0;
            zcrSum += (double)crossings / FrameSize;
            frames++;
        }

        if (frames == 0)
        {
            return AudioFeatureExtractor.Extract(new Track { Id = trackId });
        }

        var energy = Clamp01((rmsSum / frames) * 4.0);
        var centroid = Clamp01(centroidSum / frames);
        var zcr = Math.Clamp(zcrSum / frames, 0, 1);

        var (bpm, regularity) = EstimateTempo(envelope, sampleRate);

        var acousticness = Clamp01(((1 - energy) * 0.5) + ((1 - centroid) * 0.5) - (zcr * 0.2));
        var danceability = Clamp01(0.4 + ((energy - 0.3) * 0.6) + (regularity * 0.4));
        var valence = Clamp01(0.5 + ((centroid - 0.5) * 0.4) + ((energy - 0.5) * 0.3));

        return new AudioFeatures
        {
            TrackId = trackId,
            Bpm = bpm,
            Energy = energy,
            Valence = valence,
            Acousticness = acousticness,
            Danceability = danceability,
            SpectralCentroid = centroid,
        };
    }

    /// <summary>
    /// Tempo from the autocorrelation of the RMS onset envelope. Returns the
    /// best BPM in [60, 200] and a 0..1 periodicity score.
    /// </summary>
    private static (double Bpm, double Regularity) EstimateTempo(IReadOnlyList<double> envelope, int sampleRate)
    {
        var frameRate = (double)sampleRate / Hop;
        if (envelope.Count < 8)
        {
            return (120.0, 0.0);
        }

        // Lag range corresponding to 200..60 BPM.
        var minLag = Math.Max(1, (int)Math.Floor(60.0 * frameRate / 200.0));
        var maxLag = Math.Min(envelope.Count - 1, (int)Math.Ceiling(60.0 * frameRate / 60.0));

        var mean = envelope.Average();
        var variance = envelope.Sum(v => (v - mean) * (v - mean));
        if (variance <= 1e-9 || maxLag <= minLag)
        {
            return (120.0, 0.0);
        }

        var bestLag = minLag;
        var bestScore = double.MinValue;
        for (var lag = minLag; lag <= maxLag; lag++)
        {
            double sum = 0;
            for (var i = 0; i + lag < envelope.Count; i++)
            {
                sum += (envelope[i] - mean) * (envelope[i + lag] - mean);
            }

            var score = sum / variance;
            if (score > bestScore)
            {
                bestScore = score;
                bestLag = lag;
            }
        }

        var bpm = Math.Clamp(60.0 * frameRate / bestLag, 60, 200);
        var regularity = Math.Clamp(bestScore, 0, 1);
        return (bpm, regularity);
    }

    /// <summary>Iterative in-place radix-2 Cooley-Tukey FFT. Length must be a power of two.</summary>
    internal static void Fft(double[] re, double[] im)
    {
        var n = re.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            var bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
            {
                j ^= bit;
            }

            j ^= bit;
            if (i < j)
            {
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
        }

        for (var len = 2; len <= n; len <<= 1)
        {
            var angle = -2 * Math.PI / len;
            var wRe = Math.Cos(angle);
            var wIm = Math.Sin(angle);
            for (var i = 0; i < n; i += len)
            {
                double curRe = 1, curIm = 0;
                for (var k = 0; k < len / 2; k++)
                {
                    var uRe = re[i + k];
                    var uIm = im[i + k];
                    var vRe = (re[i + k + (len / 2)] * curRe) - (im[i + k + (len / 2)] * curIm);
                    var vIm = (re[i + k + (len / 2)] * curIm) + (im[i + k + (len / 2)] * curRe);

                    re[i + k] = uRe + vRe;
                    im[i + k] = uIm + vIm;
                    re[i + k + (len / 2)] = uRe - vRe;
                    im[i + k + (len / 2)] = uIm - vIm;

                    var nextRe = (curRe * wRe) - (curIm * wIm);
                    curIm = (curRe * wIm) + (curIm * wRe);
                    curRe = nextRe;
                }
            }
        }
    }

    private static double[] Hann(int size)
    {
        var window = new double[size];
        for (var i = 0; i < size; i++)
        {
            window[i] = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (size - 1)));
        }

        return window;
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}
