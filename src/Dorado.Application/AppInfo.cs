namespace Dorado.Application;

/// <summary>
/// Central application identity, used by the About page, the What's New dialog, and window titles.
/// </summary>
public static class AppInfo
{
    public const string Version = "1.1.0";
    public const string ProductName = "Dorado";
    public const string Tagline = "The Modern Spiritual Successor to Microsoft Zune Desktop";
    public const string CopyrightLine = "© 2026 Heretek-AI. Built on the shoulders of the Zune team.";
    public const string EulaLink = "https://github.com/project-dorado/dorado/blob/main/LICENSE";

    public static string VersionDisplay => $"{ProductName} v{Version} (True-Parity Engine)";

    /// <summary>Build identifier (commit + date) — filled by the platform layer when available.</summary>
    public static string BuildIdentifier { get; set; } = "local-dev";

    /// <summary>Runtime identifier (e.g. .NET 8 on Linux X11) — filled by the platform layer.</summary>
    public static string RuntimeIdentifier { get; set; } = string.Empty;

    /// <summary>Highlights shown in the What's New dialog when the version changes.</summary>
    public static IReadOnlyList<string> WhatsNewHighlights { get; } = new List<string>
    {
        "Real audio playback — BASS engine with gapless chaining and equal-power crossfade",
        "ReplayGain volume leveling and a live FFT spectrum visualizer",
        "Smart playlists — build rule-based auto playlists that grow with your collection",
        "Find Album Info now matches tracks on MusicBrainz with a review dialog",
        "Podcasts are fully audible — episodes stream over the internet",
        "Search autocomplete, back-stack navigation (Escape / back arrow), and Mixview like/hate/info/add tiles"
    };
}

