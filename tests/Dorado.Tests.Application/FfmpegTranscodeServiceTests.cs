using Dorado.Application.Interfaces;
using Dorado.Infrastructure.Audio;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Covers the FFmpeg transcoder: codec mapping, and a real end-to-end encode
/// (skipped when FFmpeg is not installed on the machine).
/// </summary>
public sealed class FfmpegTranscodeServiceTests
{
    [Theory]
    [InlineData("m4a", "-c:a aac -b:a 256k")]
    [InlineData("aac", "-c:a aac -b:a 256k")]
    [InlineData("mp3", "-c:a libmp3lame -b:a 320k")]
    [InlineData("flac", "-c:a flac")]
    [InlineData("opus", "-c:a libopus -b:a 192k")]
    [InlineData("unknown", "-c:a aac -b:a 256k")]
    public void CodecArgs_MapContainers(string container, string expected)
        => Assert.Equal(expected, FfmpegTranscodeService.CodecArgs(container));

    [Theory]
    [InlineData(".M4A", "m4a")]
    [InlineData(" mp3 ", "mp3")]
    [InlineData("", "m4a")]
    public void NormalizeContainer_TrimsAndLowercases(string input, string expected)
        => Assert.Equal(expected, FfmpegTranscodeService.NormalizeContainer(input));

    [Fact]
    public async Task TranscodesWavToM4a()
    {
        var service = new FfmpegTranscodeService();
        if (!service.IsAvailable)
        {
            return; // FFmpeg not installed: the sync pipeline falls back to verbatim copy.
        }

        var directory = Path.Combine(Path.GetTempPath(), "dorado-transcode-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "tone.wav");
        WriteSineWav(source, seconds: 0.4);

        try
        {
            var progress = new List<double>();
            var result = await service.TranscodeAsync(source, "m4a", directory, new Progress<double>(progress.Add));

            Assert.True(result.Success, result.Error);
            Assert.NotNull(result.OutputPath);
            Assert.True(File.Exists(result.OutputPath));
            Assert.True(new FileInfo(result.OutputPath!).Length > 0);
            Assert.EndsWith(".m4a", result.OutputPath);
            Assert.Contains(1.0, progress);
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task MissingSource_Fails()
    {
        var service = new FfmpegTranscodeService();
        if (!service.IsAvailable)
        {
            return;
        }

        var result = await service.TranscodeAsync("/nonexistent/source.wma", "m4a", Path.GetTempPath());
        Assert.False(result.Success);
    }

    /// <summary>Writes a minimal 16-bit mono PCM WAV containing a sine tone.</summary>
    private static void WriteSineWav(string path, double seconds, int sampleRate = 44_100, double frequency = 440)
    {
        var samples = (int)(sampleRate * seconds);
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        var dataSize = samples * 2;

        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);          // PCM
        writer.Write((short)1);          // mono
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);    // byte rate
        writer.Write((short)2);          // block align
        writer.Write((short)16);         // bits per sample
        writer.Write("data"u8.ToArray());
        writer.Write(dataSize);

        for (var i = 0; i < samples; i++)
        {
            var value = Math.Sin(2 * Math.PI * frequency * i / sampleRate);
            writer.Write((short)(value * short.MaxValue * 0.6));
        }
    }
}
