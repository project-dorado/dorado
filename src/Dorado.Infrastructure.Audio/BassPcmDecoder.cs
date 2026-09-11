using Dorado.Application.Interfaces;
using ManagedBass;

namespace Dorado.Infrastructure.Audio;

/// <summary>
/// Decodes a bounded window of an audio file to mono float PCM using BASS's
/// decode stream (no playback device required — the same library the player
/// uses). Any failure returns null so audio-feature analysis falls back to the
/// metadata prior.
/// </summary>
public sealed class BassPcmDecoder : IPcmDecoder
{
    private const int ChunkFloats = 8192;

    public Task<PcmSamples?> DecodeMonoAsync(
        string filePath,
        TimeSpan maxDuration,
        CancellationToken cancellationToken = default)
        => Task.Run(() => Decode(filePath, maxDuration, cancellationToken), cancellationToken);

    private static PcmSamples? Decode(string filePath, TimeSpan maxDuration, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        var handle = Bass.CreateStream(filePath, 0, 0, BassFlags.Decode | BassFlags.Float);
        if (handle == 0)
        {
            return null;
        }

        try
        {
            var info = Bass.ChannelGetInfo(handle);
            var sampleRate = info.Frequency > 0 ? info.Frequency : 44100;
            var channels = Math.Max(1, info.Channels);
            var maxFrames = (long)(maxDuration.TotalSeconds * sampleRate);

            var mono = new List<float>((int)Math.Min(maxFrames, 4_000_000));
            var buffer = new float[ChunkFloats];
            long framesRead = 0;

            while (framesRead < maxFrames)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var bytes = Bass.ChannelGetData(handle, buffer, buffer.Length * sizeof(float));
                if (bytes <= 0)
                {
                    break;
                }

                var floats = bytes / sizeof(float);
                for (var i = 0; i + channels <= floats; i += channels)
                {
                    float sum = 0;
                    for (var c = 0; c < channels; c++)
                    {
                        sum += buffer[i + c];
                    }

                    mono.Add(sum / channels);
                    framesRead++;
                }
            }

            return mono.Count < 4096 ? null : new PcmSamples(mono.ToArray(), sampleRate);
        }
        catch
        {
            return null;
        }
        finally
        {
            Bass.StreamFree(handle);
        }
    }
}
