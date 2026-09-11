namespace Dorado.Application.Models;

public class AppSettings
{
    public int SchemaVersion { get; set; } = 1;

    // Collection
    public string MusicFolderPath { get; set; } = string.Empty;
    public bool AutoWatchFolder { get; set; } = true;
    public string StartupView { get; set; } = "Quickplay";

    // Playback & Audio
    public bool CrossfadeEnabled { get; set; } = true;
    public double CrossfadeDurationSeconds { get; set; } = 2.0;
    public bool GaplessPlaybackEnabled { get; set; } = true;
    public bool SoundEffectsEnabled { get; set; } = true;
    public bool VolumeLevelingEnabled { get; set; } = true;
    public bool EqualizerEnabled { get; set; }
    public string EqualizerPreset { get; set; } = "Flat";
    public bool CompactModeAlwaysOnTop { get; set; } = true;

    // Rip
    public string SelectedRipFormat { get; set; } = "FLAC (Lossless Free Audio)";
    public string SelectedRipBitrate { get; set; } = "Lossless (Maximum Fidelity)";
    public string RipDestinationFolder { get; set; } = string.Empty;
    public bool AutoRipCdOnInsert { get; set; }
    public bool EjectCdAfterRip { get; set; } = true;

    // Burn
    public string SelectedDiscType { get; set; } = "Audio CD (Red Book standard, playable in car/home stereos)";
    public string SelectedBurnSpeed { get; set; } = "16x (Recommended for Audio CD)";
    public bool ApplyVolumeLevelingToBurn { get; set; } = true;

    // Metadata / Online Enrichment
    public bool AutoFetchMetadata { get; set; } = true;
    public bool AutoDownloadArtistArt { get; set; } = true;
    public bool ArtistImageFallbackEnabled { get; set; } = true;
    public string CommunityArtistImageBaseUrl { get; set; } = string.Empty;
    public bool WriteTagsToFile { get; set; } = true;
    public bool MusicBrainzEnabled { get; set; } = true;
    public bool LastFmEnabled { get; set; } = true;
    public bool LrcLibEnabled { get; set; } = true;
    public string FanartTvApiKey { get; set; } = string.Empty;

    // AcoustID (scan-time metadata enrichment + acoustic duplicate detection).
    // Requires the external `fpcalc` (Chromaprint) tool on PATH; disabled when
    // the API key is empty.
    public string AcoustIdApiKey { get; set; } = string.Empty;
    public string AcoustIdFpcalcPath { get; set; } = "fpcalc";

    // Dorado Cloud — community cloud services (catalog, artwork CDN, identity,
    // OTA updates, social). When enabled and reachable, the cloud becomes the
    // authoritative source for catalog/artwork; the direct MusicBrainz/Cover Art
    // Archive clients remain as the offline fallback.
    public bool CloudEnabled { get; set; }
    public string CloudBaseUrl { get; set; } = string.Empty;
    /// <summary>Social handle whose live Zune Card is shown on the card page.</summary>
    public string CloudHandle { get; set; } = string.Empty;
    /// <summary>Bearer token issued by the OIDC flow; persisted after PKCE sign-in.</summary>
    public string CloudAccessToken { get; set; } = string.Empty;
    public DateTime? CloudAccessTokenExpiresAtUtc { get; set; }
    /// <summary>OIDC refresh token (offline_access); used to renew the access token silently.</summary>
    public string CloudRefreshToken { get; set; } = string.Empty;
    /// <summary>When true (default), the cloud response wins on a conflict; the inner service is only consulted on cloud failure.</summary>
    public bool CloudPreferCloud { get; set; } = true;

    // Emulator (XNA .ccgame / .zcp execution via the dorado-emu CLI IPC bridge)
    public bool EmulatorEnabled { get; set; } = true;
    public string EmulatorCliPath { get; set; } = "dorado";

    // LAN sync (phone ↔ desktop over the sync.* JSON-RPC TCP protocol)
    public bool LanSyncEnabled { get; set; }
    public int LanSyncPort { get; set; } = 8787;
    public string LanSyncPairingCode { get; set; } = string.Empty;

    // Device
    public int SpaceReservationPercent { get; set; } = 10;
    public string MusicSyncRule { get; set; } = "All Music (Automatic Sync)";
    public string PodcastSyncRule { get; set; } = "3 Newest Episodes";
    public string VideoSyncRule { get; set; } = "All Videos & Pictures";
    public string PicturesSyncRule { get; set; } = "Newest 25 Items";
    public bool WirelessSyncEnabled { get; set; } = true;
    public string NetworkName { get; set; } = "Home-WiFi (WPA2)";

    public string PodcastKeepEpisodes { get; set; } = "All Unplayed";
    public bool PodcastAutoDownload { get; set; } = true;

    // File Types (library ingest extensions — Zune's FILETYPES.UIX parity)
    public string IngestExtensions { get; set; } = "mp3,m4a,m4b,wma,mp4,m4v,flac,ogg,opus,aac";

    // Privacy
    public bool UsageDataOptIn { get; set; }
    public bool AutoCheckForUpdates { get; set; } = true;

    // Photos
    public string PhotoFolderPath { get; set; } = string.Empty;
    public bool SlideshowShuffle { get; set; } = true;
    public bool SlideshowRepeat { get; set; } = true;
    public bool DeletePhotosAfterReverseSync { get; set; }

    // General
    public string Language { get; set; } = "en";
    public bool ShowRatings { get; set; } = true;
    public List<string> FirstConnectCompletedSerials { get; set; } = new();
    public string FirstConnectDeviceName { get; set; } = string.Empty;

    // Display
    public string SelectedAccentName { get; set; } = string.Empty;
    public string SelectedBackgroundName { get; set; } = string.Empty;
    public string SelectedThemeName { get; set; } = string.Empty;

    // Zune Card (local, user-authored profile — a substitute for the dead Zune Social layer)
    public string ZuneTag { get; set; } = string.Empty;
    public string ZuneStatusMessage { get; set; } = string.Empty;
    public string ZuneAvatarUri { get; set; } = string.Empty;

    // Onboarding
    public bool FirstLaunchCompleted { get; set; }
    public string WhatsNewSeenVersion { get; set; } = string.Empty;
}
