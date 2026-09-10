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
    public bool WriteTagsToFile { get; set; } = true;
    public bool MusicBrainzEnabled { get; set; } = true;
    public bool LastFmEnabled { get; set; } = true;
    public bool LrcLibEnabled { get; set; } = true;
    public string FanartTvApiKey { get; set; } = string.Empty;

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
    public bool ShowRatings { get; set; } = true;
    public List<string> FirstConnectCompletedSerials { get; set; } = new();
    public string FirstConnectDeviceName { get; set; } = string.Empty;

    // Display
    public string SelectedAccentName { get; set; } = string.Empty;
    public string SelectedBackgroundName { get; set; } = string.Empty;
    public string SelectedThemeName { get; set; } = string.Empty;

    // Onboarding
    public bool FirstLaunchCompleted { get; set; }
    public string WhatsNewSeenVersion { get; set; } = string.Empty;
}
