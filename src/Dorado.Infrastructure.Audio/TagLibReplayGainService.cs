using Dorado.Application.Interfaces;

namespace Dorado.Infrastructure.Audio;

/// <summary>
/// Reads ReplayGain metadata (REPLAYGAIN_TRACK_GAIN) from audio files via TagLib#.
/// Supports FLAC/OGG/Opus (Xiph comments) and MP3 (ID3v2 TXXX user text); returns null elsewhere.
/// </summary>
public sealed class TagLibReplayGainService : IReplayGainService
{
    public double? ReadTrackGainDb(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            using var file = TagLib.File.Create(filePath);
            if (file.Tag is TagLib.Id3v2.Tag id3v2)
            {
                var frame = id3v2.GetFrames<TagLib.Id3v2.UserTextInformationFrame>()
                    .FirstOrDefault(f => string.Equals(f.Description, "REPLAYGAIN_TRACK_GAIN", StringComparison.OrdinalIgnoreCase));
                if (frame?.Text is { Length: > 0 } id3Values)
                {
                    return ParseDb(id3Values[0]);
                }

                return null;
            }

            if (file.Tag is TagLib.Ogg.XiphComment xiph)
            {
                var values = xiph.GetField("REPLAYGAIN_TRACK_GAIN");
                return values is { Length: > 0 } ? ParseDb(values[0]) : null;
            }

            if (file.Tag is TagLib.Ape.Tag ape)
            {
                var apeValues = ape.GetItem("REPLAYGAIN_TRACK_GAIN")?.ToStringArray();
                return apeValues is { Length: > 0 } ? ParseDb(apeValues[0]) : null;
            }
        }
        catch (Exception)
        {
            // Corrupt/unreadable tags must never break playback.
        }

        return null;
    }

    private static double? ParseDb(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var cleaned = raw.Trim();
        if (cleaned.EndsWith("dB", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[..^2].Trim();
        }

        return double.TryParse(cleaned, System.Globalization.CultureInfo.InvariantCulture, out var db)
            ? db
            : null;
    }
}
