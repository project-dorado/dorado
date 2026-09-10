namespace Dorado.Domain.Models;

/// <summary>
/// The shared media-format contract between the Dorado desktop, the Dorado-HD
/// Android client (Media3/ExoPlayer), and the emulator's content pipeline.
///
/// The desktop can ingest a wide set of formats; the HD device can only play
/// what its Media3 extractors/decoders support. Anything outside that set is
/// transcoded by the desktop during sync to a device-playable target. This type
/// is the single source of truth for both sides — the Kotlin mirror lives at
/// <c>dorado-hd/.../data/model/MediaFormats.kt</c> and a parity test on each
/// side asserts the two lists agree.
/// </summary>
public static class MediaFormats
{
    /// <summary>Extensions the desktop library scanner ingests (Settings → File Types).</summary>
    public static readonly IReadOnlyList<string> IngestExtensions = new[]
    {
        "mp3", "m4a", "m4b", "wma", "mp4", "m4v", "flac", "ogg", "opus", "aac",
    };

    /// <summary>
    /// Extensions Media3/ExoPlayer plays natively on the Zune HD client. WMA and
    /// M4B are deliberately absent: open-source Media3 has no WMA extractor, and
    /// audiobook M4B is not a device target in this generation.
    /// </summary>
    public static readonly IReadOnlyList<string> HdPlayableExtensions = new[]
    {
        "mp3", "m4a", "aac", "flac", "ogg", "opus", "mp4", "m4v",
    };

    /// <summary>Device-playable container the desktop transcodes to, keyed by source extension.</summary>
    public const string DefaultTranscodeTarget = "m4a";

    /// <summary>Lossless sources keep their quality when the device supports them; otherwise → AAC.</summary>
    public static readonly IReadOnlyDictionary<string, string> TranscodeTargets =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["wma"] = "m4a",
            ["m4b"] = "m4a",
            ["ape"] = "m4a",
            ["wav"] = "flac",
        };

    public static bool IsHdPlayable(string extension)
        => HdPlayableExtensions.Contains(Normalize(extension), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the extension the desktop should transfer to the device, or null
    /// when the source is already device-playable and can be copied verbatim.
    /// </summary>
    public static string? TranscodeTargetFor(string extension)
    {
        var normalized = Normalize(extension);
        if (IsHdPlayable(normalized))
        {
            return null;
        }

        return TranscodeTargets.TryGetValue(normalized, out var target) ? target : DefaultTranscodeTarget;
    }

    public static bool NeedsTranscode(string extension) => TranscodeTargetFor(extension) is not null;

    private static string Normalize(string extension)
        => extension.Trim().TrimStart('.').ToLowerInvariant();
}
