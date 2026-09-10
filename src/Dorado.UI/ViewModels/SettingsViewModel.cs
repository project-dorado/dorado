using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.Plugins.Host;

namespace Dorado.UI.ViewModels;

public record AccentColorOption(string Name, string HexCode);
public record BackgroundThemeOption(string Name, string? AssetUri);

public record ThemeOption(string Name, bool IsDark)
{
    public override string ToString() => Name;
}

public enum SettingsTopLevelPivot
{
    Software,
    Device
}

public enum SoftwareSubPivot
{
    Collection,
    Playback,
    Podcasts,
    FileTypes,
    Privacy,
    Photos,
    Rip,
    Burn,
    Metadata,
    Display,
    General,
    About,
    Plugins
}

public enum DeviceSubPivot
{
    SyncOptions,
    SpaceReservation,
    WirelessSync,
    DeviceInfo
}

/// <summary>Row model for the Settings → Software → Plugins page.</summary>
public sealed class PluginRow : ViewModelBase
{
    public PluginRow(string id, string name, string version, string author, string description)
    {
        Id = id;
        Name = name;
        Version = version;
        Author = author;
        Description = description;
    }

    public string Id { get; }
    public string Name { get; }
    public string Version { get; }
    public string Author { get; }
    public string Description { get; }

    public string Summary => $"{Name}  ·  v{Version}  ·  {Author}";

    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                Toggled?.Invoke(this, value);
            }
        }
    }

    private string _status = string.Empty;
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public Action<PluginRow, bool>? Toggled { get; set; }

    public void SetEnabledSilent(bool value) => _isEnabled = value;
}

public class SettingsViewModel : ViewModelBase
{
    private readonly ISoundEffectService? _soundService;
    private readonly IFolderPickerService? _folderPicker;
    private readonly IMediaLibraryService? _libraryService;
    private readonly IPlayerCoordinator? _playerCoordinator;
    private readonly IDeviceSyncService? _deviceSyncService;
    private readonly ISettingsStore? _settingsStore;
    private readonly PluginManager? _pluginManager;
    private bool _isRestoringSettings = true;
    private bool _firstLaunchCompleted;
    private string _whatsNewSeenVersion = string.Empty;

    /// <summary>Set once the first-launch wizard (or a manual skip) has completed.</summary>
    public bool FirstLaunchCompleted
    {
        get => _firstLaunchCompleted;
        set
        {
            if (SetProperty(ref _firstLaunchCompleted, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    /// <summary>Per-serial list of devices that have completed the FirstConnect wizard.</summary>
    public System.Collections.Generic.List<string> FirstConnectCompletedSerials { get; } = new();

    /// <summary>The last application version whose What's New dialog was acknowledged.</summary>
    public string WhatsNewSeenVersion
    {
        get => _whatsNewSeenVersion;
        set
        {
            if (SetProperty(ref _whatsNewSeenVersion, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    public event EventHandler<string?>? BackgroundArtChanged;

    // ==========================================
    // TWO-TIER PIVOT NAVIGATION
    // ==========================================
    private SettingsTopLevelPivot _topLevelPivot = SettingsTopLevelPivot.Software;
    public SettingsTopLevelPivot TopLevelPivot
    {
        get => _topLevelPivot;
        set
        {
            if (SetProperty(ref _topLevelPivot, value))
            {
                OnPropertyChanged(nameof(IsSoftwarePivotActive));
                OnPropertyChanged(nameof(IsDevicePivotActive));
                // Content panels compose top-level + sub-pivot state.
                OnPropertyChanged(nameof(IsCollectionSubPivotActive));
                OnPropertyChanged(nameof(IsPlaybackSubPivotActive));
                OnPropertyChanged(nameof(IsPodcastsSubPivotActive));
                OnPropertyChanged(nameof(IsFileTypesSubPivotActive));
                OnPropertyChanged(nameof(IsPrivacySubPivotActive));
                OnPropertyChanged(nameof(IsPhotosSubPivotActive));
                OnPropertyChanged(nameof(IsRipSubPivotActive));
                OnPropertyChanged(nameof(IsBurnSubPivotActive));
                OnPropertyChanged(nameof(IsMetadataSubPivotActive));
                OnPropertyChanged(nameof(IsDisplaySubPivotActive));
                OnPropertyChanged(nameof(IsGeneralSubPivotActive));
                OnPropertyChanged(nameof(IsAboutSubPivotActive));
                OnPropertyChanged(nameof(IsPluginsSubPivotActive));
                OnPropertyChanged(nameof(IsSyncOptionsSubPivotActive));
                OnPropertyChanged(nameof(IsSpaceReservationSubPivotActive));
                OnPropertyChanged(nameof(IsWirelessSyncSubPivotActive));
                OnPropertyChanged(nameof(IsDeviceInfoSubPivotActive));
            }
        }
    }

    public bool IsSoftwarePivotActive => TopLevelPivot == SettingsTopLevelPivot.Software;
    public bool IsDevicePivotActive => TopLevelPivot == SettingsTopLevelPivot.Device;

    private SoftwareSubPivot _softwarePivot = SoftwareSubPivot.Collection;
    public SoftwareSubPivot SoftwarePivot
    {
        get => _softwarePivot;
        set
        {
            if (SetProperty(ref _softwarePivot, value))
            {
                OnPropertyChanged(nameof(IsCollectionSubPivotActive));
                OnPropertyChanged(nameof(IsPlaybackSubPivotActive));
                OnPropertyChanged(nameof(IsPodcastsSubPivotActive));
                OnPropertyChanged(nameof(IsFileTypesSubPivotActive));
                OnPropertyChanged(nameof(IsPrivacySubPivotActive));
                OnPropertyChanged(nameof(IsPhotosSubPivotActive));
                OnPropertyChanged(nameof(IsRipSubPivotActive));
                OnPropertyChanged(nameof(IsBurnSubPivotActive));
                OnPropertyChanged(nameof(IsMetadataSubPivotActive));
                OnPropertyChanged(nameof(IsDisplaySubPivotActive));
                OnPropertyChanged(nameof(IsGeneralSubPivotActive));
                OnPropertyChanged(nameof(IsAboutSubPivotActive));
                OnPropertyChanged(nameof(IsPluginsSubPivotActive));
            }
        }
    }

    // Sub-pivot flags are composed with the top-level pivot: each pivot remembers its
    // last sub-pivot, so without the gate two content panels would render at once.
    public bool IsCollectionSubPivotActive => IsSoftwarePivotActive && SoftwarePivot == SoftwareSubPivot.Collection;
    public bool IsPlaybackSubPivotActive => IsSoftwarePivotActive && SoftwarePivot == SoftwareSubPivot.Playback;
    public bool IsPodcastsSubPivotActive => IsSoftwarePivotActive && SoftwarePivot == SoftwareSubPivot.Podcasts;
    public bool IsFileTypesSubPivotActive => IsSoftwarePivotActive && SoftwarePivot == SoftwareSubPivot.FileTypes;
    public bool IsPrivacySubPivotActive => IsSoftwarePivotActive && SoftwarePivot == SoftwareSubPivot.Privacy;
    public bool IsPhotosSubPivotActive => IsSoftwarePivotActive && SoftwarePivot == SoftwareSubPivot.Photos;
    public bool IsRipSubPivotActive => IsSoftwarePivotActive && SoftwarePivot == SoftwareSubPivot.Rip;
    public bool IsBurnSubPivotActive => IsSoftwarePivotActive && SoftwarePivot == SoftwareSubPivot.Burn;
    public bool IsMetadataSubPivotActive => IsSoftwarePivotActive && SoftwarePivot == SoftwareSubPivot.Metadata;
    public bool IsDisplaySubPivotActive => IsSoftwarePivotActive && SoftwarePivot == SoftwareSubPivot.Display;
    public bool IsGeneralSubPivotActive => IsSoftwarePivotActive && SoftwarePivot == SoftwareSubPivot.General;
    public bool IsAboutSubPivotActive => IsSoftwarePivotActive && SoftwarePivot == SoftwareSubPivot.About;
    public bool IsPluginsSubPivotActive => IsSoftwarePivotActive && SoftwarePivot == SoftwareSubPivot.Plugins;

    // ==========================================
    // PLUGINS (Phase 12)
    // ==========================================
    public ObservableCollection<PluginRow> Plugins { get; } = new();
    public ICommand InstallPluginCommand { get; }
    public ICommand OpenPluginsFolderCommand { get; }
    public bool HasPlugins => Plugins.Count > 0;

    private void RefreshPlugins()
    {
        if (_pluginManager is null)
        {
            return;
        }

        Plugins.Clear();
        foreach (var info in _pluginManager.Plugins)
        {
            var row = new PluginRow(info.Id, info.Name, info.Version, info.Author, info.Description)
            {
                Status = string.IsNullOrEmpty(info.LastError)
                    ? info.Status.ToString()
                    : $"{info.Status} — {info.LastError}"
            };
            row.Toggled = async (pluginRow, enabled) =>
            {
                if (_pluginManager is not null)
                {
                    await _pluginManager.SetEnabledAsync(pluginRow.Id, enabled);
                }
            };
            row.SetEnabledSilent(info.Enabled);
            Plugins.Add(row);
        }

        OnPropertyChanged(nameof(HasPlugins));
    }

    private async Task OnInstallPluginAsync()
    {
        if (_folderPicker is null || _pluginManager is null)
        {
            return;
        }

        var path = await _folderPicker.PickFileAsync("Install Plugin Package", ".znp");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            await _pluginManager.InstallAsync(path);
        }
        catch
        {
            // Install errors surface through the manager's plugin state.
        }

        RefreshPlugins();
    }

    private void OnOpenPluginsFolder()
    {
        var directory = _pluginManager?.PluginsDirectory ?? PluginManagerOptions.DefaultPluginsDirectory();
        try
        {
            Directory.CreateDirectory(directory);
            Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
        }
        catch
        {
            // Best-effort convenience; ignore shell failures.
        }
    }

    private DeviceSubPivot _devicePivot = DeviceSubPivot.SyncOptions;
    public DeviceSubPivot DevicePivot
    {
        get => _devicePivot;
        set
        {
            if (SetProperty(ref _devicePivot, value))
            {
                OnPropertyChanged(nameof(IsSyncOptionsSubPivotActive));
                OnPropertyChanged(nameof(IsSpaceReservationSubPivotActive));
                OnPropertyChanged(nameof(IsWirelessSyncSubPivotActive));
                OnPropertyChanged(nameof(IsDeviceInfoSubPivotActive));
            }
        }
    }

    public bool IsSyncOptionsSubPivotActive => IsDevicePivotActive && DevicePivot == DeviceSubPivot.SyncOptions;
    public bool IsSpaceReservationSubPivotActive => IsDevicePivotActive && DevicePivot == DeviceSubPivot.SpaceReservation;
    public bool IsWirelessSyncSubPivotActive => IsDevicePivotActive && DevicePivot == DeviceSubPivot.WirelessSync;
    public bool IsDeviceInfoSubPivotActive => IsDevicePivotActive && DevicePivot == DeviceSubPivot.DeviceInfo;

    // ==========================================
    // THEMES & ACCENTS
    // ==========================================
    public ObservableCollection<AccentColorOption> AccentColors { get; } = new()
    {
        new("Zune Pink (Signature)", "#F10DA2"),
        new("Zune Orange", "#EC6922"),
        new("Zune Electric Cyan", "#1BA1E2"),
        new("Zune Vivid Lime", "#339933"),
        new("Zune Deep Purple", "#A200FF")
    };

    public ObservableCollection<ThemeOption> ThemeOptions { get; } = new()
    {
        new("Matte Black (Dark)", true),
        new("Soft White (Light)", false)
    };

    private ThemeOption _selectedTheme = new("Matte Black (Dark)", true);
    public ThemeOption SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                ApplyTheme(value);
                SaveCurrentSettings();
            }
        }
    }

    public ObservableCollection<BackgroundThemeOption> BackgroundThemes { get; } = new()
    {
        new("Classic Minimal (Matte Black)", null),
        new("Vector Ribbon 10", "avares://Dorado.UI/Assets/Zune/Backgrounds/USERBACKGROUND-ART-536X196-10.JPG"),
        new("Abstract Aurora 15", "avares://Dorado.UI/Assets/Zune/Backgrounds/USERBACKGROUND-ART-536X196-15.JPG"),
        new("Geometric Mesh 20", "avares://Dorado.UI/Assets/Zune/Backgrounds/USERBACKGROUND-ART-536X196-20.JPG"),
        new("Cosmic Gradient 25", "avares://Dorado.UI/Assets/Zune/Backgrounds/USERBACKGROUND-ART-536X196-25.JPG"),
        new("Circuit Flow 30", "avares://Dorado.UI/Assets/Zune/Backgrounds/USERBACKGROUND-ART-536X196-30.JPG"),
        new("Prism Waves 35", "avares://Dorado.UI/Assets/Zune/Backgrounds/USERBACKGROUND-ART-536X196-35.JPG"),
        new("Retro Horizon 40", "avares://Dorado.UI/Assets/Zune/Backgrounds/USERBACKGROUND-ART-536X196-40.JPG"),
        new("Radiant Bloom 45", "avares://Dorado.UI/Assets/Zune/Backgrounds/USERBACKGROUND-ART-536X196-45.JPG"),
        new("Neon Drift 47", "avares://Dorado.UI/Assets/Zune/Backgrounds/USERBACKGROUND-ART-536X196-47.JPG")
    };

    private AccentColorOption _selectedAccent;
    public AccentColorOption SelectedAccent
    {
        get => _selectedAccent;
        set
        {
            if (SetProperty(ref _selectedAccent, value))
            {
                ApplyAccent(value);
                SaveCurrentSettings();
            }
        }
    }

    private BackgroundThemeOption _selectedBackground;
    public BackgroundThemeOption SelectedBackground
    {
        get => _selectedBackground;
        set
        {
            if (SetProperty(ref _selectedBackground, value))
            {
                BackgroundArtChanged?.Invoke(this, value.AssetUri);
                SaveCurrentSettings();
            }
        }
    }

    // ==========================================
    // AUDIO & PLAYBACK SETTINGS
    // ==========================================
    private bool _crossfadeEnabled = true;
    public bool CrossfadeEnabled
    {
        get => _crossfadeEnabled;
        set
        {
            if (SetProperty(ref _crossfadeEnabled, value))
            {
                if (_playerCoordinator != null)
                {
                    _playerCoordinator.CrossfadeDurationSeconds = value ? _crossfadeDurationSeconds : 0.0;
                }

                SaveCurrentSettings();
            }
        }
    }

    private double _crossfadeDurationSeconds = 2.0;
    public double CrossfadeDurationSeconds
    {
        get => _crossfadeDurationSeconds;
        set
        {
            var clamped = Math.Clamp(value, 0.0, 10.0);
            if (SetProperty(ref _crossfadeDurationSeconds, clamped))
            {
                if (_playerCoordinator != null && _crossfadeEnabled)
                {
                    _playerCoordinator.CrossfadeDurationSeconds = clamped;
                }
                OnPropertyChanged(nameof(CrossfadeDurationText));
                SaveCurrentSettings();
            }
        }
    }

    public string CrossfadeDurationText => $"{CrossfadeDurationSeconds:0.0} seconds";

    private bool _gaplessPlaybackEnabled = true;
    public bool GaplessPlaybackEnabled
    {
        get => _gaplessPlaybackEnabled;
        set
        {
            if (SetProperty(ref _gaplessPlaybackEnabled, value))
            {
                if (_playerCoordinator != null)
                {
                    _playerCoordinator.GaplessEnabled = value;
                }

                SaveCurrentSettings();
            }
        }
    }

    public bool SoundEffectsEnabled
    {
        get => _soundService?.SoundEffectsEnabled ?? true;
        set
        {
            if (_soundService != null)
            {
                _soundService.SoundEffectsEnabled = value;
                OnPropertyChanged();
                SaveCurrentSettings();
            }
        }
    }

    private bool _volumeLevelingEnabled = true;
    public bool VolumeLevelingEnabled
    {
        get => _volumeLevelingEnabled;
        set
        {
            if (SetProperty(ref _volumeLevelingEnabled, value))
            {
                if (_playerCoordinator != null)
                {
                    _playerCoordinator.VolumeLevelingEnabled = value;
                }

                SaveCurrentSettings();
            }
        }
    }

    private bool _compactModeAlwaysOnTop = true;
    public bool CompactModeAlwaysOnTop
    {
        get => _compactModeAlwaysOnTop;
        set
        {
            if (SetProperty(ref _compactModeAlwaysOnTop, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    // ==========================================
    // PODCAST SETTINGS
    // ==========================================
    public ObservableCollection<string> PodcastKeepEpisodesOptions { get; } = new()
    {
        "Everything",
        "All Unplayed",
        "1 Newest Unplayed",
        "Nothing"
    };

    private string _selectedPodcastKeepEpisodes = "All Unplayed";
    public string SelectedPodcastKeepEpisodes
    {
        get => _selectedPodcastKeepEpisodes;
        set
        {
            if (SetProperty(ref _selectedPodcastKeepEpisodes, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    private bool _podcastAutoDownload = true;
    public bool PodcastAutoDownload
    {
        get => _podcastAutoDownload;
        set
        {
            if (SetProperty(ref _podcastAutoDownload, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    // ==========================================
    // FILE TYPES (library ingest extensions)
    // ==========================================
    public ObservableCollection<string> FileTypesPresets { get; } = new()
    {
        "mp3,m4a,m4b,wma,mp4,m4v,flac,ogg,opus,aac",
        "mp3,m4a,flac,wma",
        "mp3,m4a,wav,flac",
        "mp3 only"
    };

    private string _ingestExtensions = "mp3,m4a,m4b,wma,mp4,m4v,flac,ogg,opus,aac";
    public string IngestExtensions
    {
        get => _ingestExtensions;
        set
        {
            if (SetProperty(ref _ingestExtensions, value))
            {
                OnPropertyChanged(nameof(IngestExtensionsList));
                SaveCurrentSettings();
            }
        }
    }

    public System.Collections.Generic.List<string> IngestExtensionsList =>
        _ingestExtensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    // ==========================================
    // PRIVACY
    // ==========================================
    private bool _usageDataOptIn;
    public bool UsageDataOptIn
    {
        get => _usageDataOptIn;
        set
        {
            if (SetProperty(ref _usageDataOptIn, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    private bool _autoCheckForUpdates = true;
    public bool AutoCheckForUpdates
    {
        get => _autoCheckForUpdates;
        set
        {
            if (SetProperty(ref _autoCheckForUpdates, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    // ==========================================
    // PHOTOS
    // ==========================================
    private string _photoFolderPath = string.Empty;
    public string PhotoFolderPath
    {
        get => _photoFolderPath;
        set
        {
            if (SetProperty(ref _photoFolderPath, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    private bool _slideshowShuffle = true;
    public bool SlideshowShuffle
    {
        get => _slideshowShuffle;
        set
        {
            if (SetProperty(ref _slideshowShuffle, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    private bool _slideshowRepeat = true;
    public bool SlideshowRepeat
    {
        get => _slideshowRepeat;
        set
        {
            if (SetProperty(ref _slideshowRepeat, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    private bool _deletePhotosAfterReverseSync;
    public bool DeletePhotosAfterReverseSync
    {
        get => _deletePhotosAfterReverseSync;
        set
        {
            if (SetProperty(ref _deletePhotosAfterReverseSync, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    // ==========================================
    // GENERAL
    // ==========================================
    public ObservableCollection<string> StartupViewOptions { get; } = new()
    {
        "Quickplay",
        "Collection"
    };

    private bool _showRatings = true;
    public bool ShowRatings
    {
        get => _showRatings;
        set
        {
            if (SetProperty(ref _showRatings, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    private string _firstConnectDeviceName = string.Empty;
    public string FirstConnectDeviceName
    {
        get => _firstConnectDeviceName;
        set => SetProperty(ref _firstConnectDeviceName, value);
    }

    // ==========================================
    // RIP SETTINGS
    // ==========================================
    public ObservableCollection<string> RipAudioFormats { get; } = new()
    {
        "FLAC (Lossless Free Audio)",
        "MP3 (High Quality VBR)",
        "WMA (Windows Media Audio 9.2)",
        "AAC (Advanced Audio Coding)"
    };

    private string _selectedRipFormat = "FLAC (Lossless Free Audio)";
    public string SelectedRipFormat
    {
        get => _selectedRipFormat;
        set
        {
            if (SetProperty(ref _selectedRipFormat, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    public ObservableCollection<string> RipBitrates { get; } = new()
    {
        "Lossless (Maximum Fidelity)",
        "320 kbps (Extreme)",
        "256 kbps (High Quality)",
        "192 kbps (Standard)",
        "128 kbps (Compact)"
    };

    private string _selectedRipBitrate = "Lossless (Maximum Fidelity)";
    public string SelectedRipBitrate
    {
        get => _selectedRipBitrate;
        set
        {
            if (SetProperty(ref _selectedRipBitrate, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    private string _ripDestinationFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Dorado Rips");
    public string RipDestinationFolder
    {
        get => _ripDestinationFolder;
        set
        {
            if (SetProperty(ref _ripDestinationFolder, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    private bool _autoRipCdOnInsert = false;
    public bool AutoRipCdOnInsert
    {
        get => _autoRipCdOnInsert;
        set
        {
            if (SetProperty(ref _autoRipCdOnInsert, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    private bool _ejectCdAfterRip = true;
    public bool EjectCdAfterRip
    {
        get => _ejectCdAfterRip;
        set
        {
            if (SetProperty(ref _ejectCdAfterRip, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    // ==========================================
    // BURN SETTINGS
    // ==========================================
    public ObservableCollection<string> DiscTypes { get; } = new()
    {
        "Audio CD (Red Book standard, playable in car/home stereos)",
        "Data Disc (MP3/FLAC disc, holds up to 150+ tracks)"
    };

    private string _selectedDiscType = "Audio CD (Red Book standard, playable in car/home stereos)";
    public string SelectedDiscType
    {
        get => _selectedDiscType;
        set
        {
            if (SetProperty(ref _selectedDiscType, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    public ObservableCollection<string> BurnSpeeds { get; } = new()
    {
        "Maximum Drive Speed",
        "24x",
        "16x (Recommended for Audio CD)",
        "8x",
        "4x"
    };

    private string _selectedBurnSpeed = "16x (Recommended for Audio CD)";
    public string SelectedBurnSpeed
    {
        get => _selectedBurnSpeed;
        set
        {
            if (SetProperty(ref _selectedBurnSpeed, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    private bool _applyVolumeLevelingToBurn = true;
    public bool ApplyVolumeLevelingToBurn
    {
        get => _applyVolumeLevelingToBurn;
        set
        {
            if (SetProperty(ref _applyVolumeLevelingToBurn, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    // ==========================================
    // METADATA SETTINGS
    // ==========================================
    private bool _autoFetchMetadata = true;
    public bool AutoFetchMetadata
    {
        get => _autoFetchMetadata;
        set
        {
            if (SetProperty(ref _autoFetchMetadata, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    private bool _autoDownloadArtistArt = true;
    public bool AutoDownloadArtistArt
    {
        get => _autoDownloadArtistArt;
        set
        {
            if (SetProperty(ref _autoDownloadArtistArt, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    private bool _writeTagsToFile = true;
    public bool WriteTagsToFile
    {
        get => _writeTagsToFile;
        set
        {
            if (SetProperty(ref _writeTagsToFile, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    private bool _musicBrainzEnabled = true;
    public bool MusicBrainzEnabled
    {
        get => _musicBrainzEnabled;
        set
        {
            if (SetProperty(ref _musicBrainzEnabled, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    private bool _lastFmEnabled = true;
    public bool LastFmEnabled
    {
        get => _lastFmEnabled;
        set
        {
            if (SetProperty(ref _lastFmEnabled, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    private bool _lrcLibEnabled = true;
    public bool LrcLibEnabled
    {
        get => _lrcLibEnabled;
        set
        {
            if (SetProperty(ref _lrcLibEnabled, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    private string _fanartTvApiKey = string.Empty;
    public string FanartTvApiKey
    {
        get => _fanartTvApiKey;
        set
        {
            if (SetProperty(ref _fanartTvApiKey, value?.Trim() ?? string.Empty))
            {
                SaveCurrentSettings();
            }
        }
    }

    // ==========================================
    // COLLECTION SETTINGS
    // ==========================================
    private bool _autoWatchFolder = true;
    public bool AutoWatchFolder
    {
        get => _autoWatchFolder;
        set
        {
            if (SetProperty(ref _autoWatchFolder, value))
            {
                if (value)
                {
                    _libraryService?.StartDirectoryWatcher(NormalizePath(MusicFolderPath));
                }
                else
                {
                    _libraryService?.StopDirectoryWatcher();
                }

                SaveCurrentSettings();
            }
        }
    }

    private string _startupView = "Quickplay";
    public string StartupView
    {
        get => _startupView;
        set
        {
            if (SetProperty(ref _startupView, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    private string _musicFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) is { Length: > 0 } myMusic
        ? myMusic
        : "~/Music";
    public string MusicFolderPath
    {
        get => _musicFolderPath;
        set
        {
            if (SetProperty(ref _musicFolderPath, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set => SetProperty(ref _isScanning, value);
    }

    private double _scanProgress;
    public double ScanProgress
    {
        get => _scanProgress;
        set => SetProperty(ref _scanProgress, value);
    }

    private string? _scanStatusText;
    public string? ScanStatusText
    {
        get => _scanStatusText;
        set
        {
            if (SetProperty(ref _scanStatusText, value))
            {
                OnPropertyChanged(nameof(HasScanStatus));
            }
        }
    }

    public bool HasScanStatus => !string.IsNullOrEmpty(ScanStatusText);

    // ==========================================
    // DEVICE SETTINGS (SYNC & SPACE RESERVATION)
    // ==========================================
    public bool IsDeviceConnected => _deviceSyncService?.ConnectedDevices.Count > 0;
    public string DeviceModelName => IsDeviceConnected 
        ? _deviceSyncService!.ConnectedDevices[0].ModelName 
        : "Zune HD (Simulated Standby)";
    public string DeviceSerialNumber => IsDeviceConnected 
        ? _deviceSyncService!.ConnectedDevices[0].SerialNumber 
        : "0001020304050607";
    public string FirmwareVersion => "v4.5 (3084)";
    public string DeviceBatteryText => "88% (Charging)";

    public double TotalCapacityGb => 32.0;

    private int _spaceReservationPercent = 10;
    public int SpaceReservationPercent
    {
        get => _spaceReservationPercent;
        set
        {
            var clamped = Math.Clamp(value, 0, 50);
            if (SetProperty(ref _spaceReservationPercent, clamped))
            {
                OnPropertyChanged(nameof(ReservedGbText));
                OnPropertyChanged(nameof(SyncSpaceGbText));
                OnPropertyChanged(nameof(SpaceReservationSummaryText));
                SaveCurrentSettings();
            }
        }
    }

    public string ReservedGbText => $"{(TotalCapacityGb * (_spaceReservationPercent / 100.0)):0.0} GB";
    public string SyncSpaceGbText => $"{(TotalCapacityGb * (1.0 - (_spaceReservationPercent / 100.0))):0.0} GB";
    public string SpaceReservationSummaryText => $"{SpaceReservationPercent}% ({ReservedGbText}) reserved for device cache and non-sync files";

    public ObservableCollection<string> SyncRules { get; } = new()
    {
        "All Music (Automatic Sync)",
        "Selected Playlists, Artists & Genres",
        "Manual Sync Only (Drag and Drop)"
    };

    private string _musicSyncRule = "All Music (Automatic Sync)";
    public string MusicSyncRule
    {
        get => _musicSyncRule;
        set
        {
            if (SetProperty(ref _musicSyncRule, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    public ObservableCollection<string> PodcastSyncRules { get; } = new()
    {
        "All Unplayed Episodes",
        "3 Newest Episodes",
        "5 Newest Episodes",
        "All Episodes"
    };

    private string _podcastSyncRule = "3 Newest Episodes";
    public string PodcastSyncRule
    {
        get => _podcastSyncRule;
        set
        {
            if (SetProperty(ref _podcastSyncRule, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    public ObservableCollection<string> MediaSyncRules { get; } = new()
    {
        "All Videos & Pictures",
        "Newest 25 Items",
        "Nothing (Manual Drag and Drop)"
    };

    private string _videoSyncRule = "All Videos & Pictures";
    public string VideoSyncRule
    {
        get => _videoSyncRule;
        set
        {
            if (SetProperty(ref _videoSyncRule, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    private string _picturesSyncRule = "Newest 25 Items";
    public string PicturesSyncRule
    {
        get => _picturesSyncRule;
        set
        {
            if (SetProperty(ref _picturesSyncRule, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    private bool _wirelessSyncEnabled = true;
    public bool WirelessSyncEnabled
    {
        get => _wirelessSyncEnabled;
        set
        {
            if (SetProperty(ref _wirelessSyncEnabled, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    private string _networkName = "Home-WiFi (WPA2)";
    public string NetworkName
    {
        get => _networkName;
        set
        {
            if (SetProperty(ref _networkName, value))
            {
                SaveCurrentSettings();
            }
        }
    }

    public string ProductName => Dorado.Application.AppInfo.ProductName;
    public string ProductTagline => Dorado.Application.AppInfo.Tagline;
    public string ProductCopyright => Dorado.Application.AppInfo.CopyrightLine;
    public string ProductEulaLink => Dorado.Application.AppInfo.EulaLink;
    public string ProductVersion => Dorado.Application.AppInfo.Version;
    public string ProductRuntime => string.IsNullOrWhiteSpace(Dorado.Application.AppInfo.RuntimeIdentifier)
        ? PlatformInfo
        : Dorado.Application.AppInfo.RuntimeIdentifier;
    public string ProductBuildId => Dorado.Application.AppInfo.BuildIdentifier;
    public string PlatformInfo => $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription} ({System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture})";
    public string VersionInfo => Dorado.Application.AppInfo.VersionDisplay;

    // ==========================================
    // COMMANDS
    // ==========================================
    public ICommand SelectTopLevelPivotCommand { get; }
    public ICommand SelectSoftwarePivotCommand { get; }
    public ICommand SelectDevicePivotCommand { get; }
    public ICommand SelectFolderCommand { get; }
    public ICommand RescanCommand { get; }
    public ICommand PickPhotoFolderCommand { get; }
    public ICommand ClearDemoLibraryCommand { get; }
    public ICommand SelectAccentCommand { get; }
    public ICommand SelectBackgroundCommand { get; }
    public ICommand SelectThemeCommand { get; }
    public ICommand TestSoundCommand { get; }

    public SettingsViewModel(
        ISoundEffectService? soundService = null,
        IFolderPickerService? folderPicker = null,
        IMediaLibraryService? libraryService = null,
        IPlayerCoordinator? playerCoordinator = null,
        IDeviceSyncService? deviceSyncService = null,
        ISettingsStore? settingsStore = null,
        PluginManager? pluginManager = null)
    {
        _soundService = soundService;
        _folderPicker = folderPicker;
        _libraryService = libraryService;
        _playerCoordinator = playerCoordinator;
        _deviceSyncService = deviceSyncService;
        _settingsStore = settingsStore;
        _pluginManager = pluginManager;

        if (_pluginManager is not null)
        {
            _pluginManager.Changed += (_, _) => RefreshPlugins();
        }

        InstallPluginCommand = new AsyncRelayCommand(OnInstallPluginAsync);
        OpenPluginsFolderCommand = new RelayCommand(OnOpenPluginsFolder);
        RefreshPlugins();

        _selectedAccent = AccentColors[0];
        _selectedBackground = BackgroundThemes[1]; // Default to authentic Zune Vector Ribbon

        SelectTopLevelPivotCommand = new RelayCommand<string>(pivotStr =>
        {
            if (Enum.TryParse<SettingsTopLevelPivot>(pivotStr, true, out var p))
            {
                TopLevelPivot = p;
            }
        });

        SelectSoftwarePivotCommand = new RelayCommand<string>(pivotStr =>
        {
            if (Enum.TryParse<SoftwareSubPivot>(pivotStr, true, out var p))
            {
                SoftwarePivot = p;
            }
        });

        SelectDevicePivotCommand = new RelayCommand<string>(pivotStr =>
        {
            if (Enum.TryParse<DeviceSubPivot>(pivotStr, true, out var p))
            {
                DevicePivot = p;
            }
        });

        SelectFolderCommand = new AsyncRelayCommand(OnSelectFolderAsync);
        RescanCommand = new AsyncRelayCommand(OnRescanAsync);
        ClearDemoLibraryCommand = new AsyncRelayCommand(OnClearDemoLibraryAsync);
        PickPhotoFolderCommand = new AsyncRelayCommand(OnPickPhotoFolderAsync);

        SelectAccentCommand = new RelayCommand<AccentColorOption>(accent =>
        {
            if (accent != null)
            {
                SelectedAccent = accent;
            }
        });
        SelectBackgroundCommand = new RelayCommand<BackgroundThemeOption>(theme =>
        {
            if (theme != null)
            {
                SelectedBackground = theme;
            }
        });
        SelectThemeCommand = new RelayCommand<ThemeOption>(theme =>
        {
            if (theme != null)
            {
                SelectedTheme = theme;
            }
        });
        TestSoundCommand = new RelayCommand(() => _soundService?.PlaySyncComplete());

        LoadPersistedSettings();
        _isRestoringSettings = false;
    }

    // ==========================================
    // SETTINGS PERSISTENCE
    // ==========================================
    private void LoadPersistedSettings()
    {
        if (_settingsStore == null)
        {
            return;
        }

        try
        {
            var settings = _settingsStore.Load();

            if (!string.IsNullOrWhiteSpace(settings.MusicFolderPath))
            {
                _musicFolderPath = settings.MusicFolderPath;
                OnPropertyChanged(nameof(MusicFolderPath));
            }

            CrossfadeEnabled = settings.CrossfadeEnabled;
            _crossfadeDurationSeconds = Math.Clamp(settings.CrossfadeDurationSeconds, 0.0, 10.0);
            OnPropertyChanged(nameof(CrossfadeDurationSeconds));
            OnPropertyChanged(nameof(CrossfadeDurationText));
            if (_playerCoordinator != null)
            {
                _playerCoordinator.CrossfadeDurationSeconds = CrossfadeEnabled ? _crossfadeDurationSeconds : 0.0;
                _playerCoordinator.GaplessEnabled = settings.GaplessPlaybackEnabled;
                _playerCoordinator.VolumeLevelingEnabled = settings.VolumeLevelingEnabled;
            }

            if (_soundService != null)
            {
                _soundService.SoundEffectsEnabled = settings.SoundEffectsEnabled;
            }
            OnPropertyChanged(nameof(SoundEffectsEnabled));

            VolumeLevelingEnabled = settings.VolumeLevelingEnabled;
            CompactModeAlwaysOnTop = settings.CompactModeAlwaysOnTop;

            _selectedRipFormat = settings.SelectedRipFormat;
            OnPropertyChanged(nameof(SelectedRipFormat));
            _selectedRipBitrate = settings.SelectedRipBitrate;
            OnPropertyChanged(nameof(SelectedRipBitrate));
            _ripDestinationFolder = settings.RipDestinationFolder;
            OnPropertyChanged(nameof(RipDestinationFolder));
            AutoRipCdOnInsert = settings.AutoRipCdOnInsert;
            EjectCdAfterRip = settings.EjectCdAfterRip;

            _selectedDiscType = settings.SelectedDiscType;
            OnPropertyChanged(nameof(SelectedDiscType));
            _selectedBurnSpeed = settings.SelectedBurnSpeed;
            OnPropertyChanged(nameof(SelectedBurnSpeed));
            _applyVolumeLevelingToBurn = settings.ApplyVolumeLevelingToBurn;
            OnPropertyChanged(nameof(ApplyVolumeLevelingToBurn));

            AutoFetchMetadata = settings.AutoFetchMetadata;
            AutoDownloadArtistArt = settings.AutoDownloadArtistArt;
            WriteTagsToFile = settings.WriteTagsToFile;
            _musicBrainzEnabled = settings.MusicBrainzEnabled;
            OnPropertyChanged(nameof(MusicBrainzEnabled));
            _lastFmEnabled = settings.LastFmEnabled;
            OnPropertyChanged(nameof(LastFmEnabled));
            _lrcLibEnabled = settings.LrcLibEnabled;
            OnPropertyChanged(nameof(LrcLibEnabled));
            _fanartTvApiKey = settings.FanartTvApiKey;
            OnPropertyChanged(nameof(FanartTvApiKey));

            _startupView = settings.StartupView;
            OnPropertyChanged(nameof(StartupView));

            _selectedPodcastKeepEpisodes = settings.PodcastKeepEpisodes;
            OnPropertyChanged(nameof(SelectedPodcastKeepEpisodes));
            _podcastAutoDownload = settings.PodcastAutoDownload;
            OnPropertyChanged(nameof(PodcastAutoDownload));

            _ingestExtensions = settings.IngestExtensions;
            OnPropertyChanged(nameof(IngestExtensions));
            OnPropertyChanged(nameof(IngestExtensionsList));

            _usageDataOptIn = settings.UsageDataOptIn;
            OnPropertyChanged(nameof(UsageDataOptIn));
            _autoCheckForUpdates = settings.AutoCheckForUpdates;
            OnPropertyChanged(nameof(AutoCheckForUpdates));

            _photoFolderPath = settings.PhotoFolderPath;
            OnPropertyChanged(nameof(PhotoFolderPath));
            _slideshowShuffle = settings.SlideshowShuffle;
            OnPropertyChanged(nameof(SlideshowShuffle));
            _slideshowRepeat = settings.SlideshowRepeat;
            OnPropertyChanged(nameof(SlideshowRepeat));
            _deletePhotosAfterReverseSync = settings.DeletePhotosAfterReverseSync;
            OnPropertyChanged(nameof(DeletePhotosAfterReverseSync));

            _showRatings = settings.ShowRatings;
            OnPropertyChanged(nameof(ShowRatings));
            _firstConnectDeviceName = settings.FirstConnectDeviceName;
            OnPropertyChanged(nameof(FirstConnectDeviceName));

            _spaceReservationPercent = Math.Clamp(settings.SpaceReservationPercent, 0, 50);
            OnPropertyChanged(nameof(SpaceReservationPercent));
            OnPropertyChanged(nameof(ReservedGbText));
            OnPropertyChanged(nameof(SyncSpaceGbText));
            OnPropertyChanged(nameof(SpaceReservationSummaryText));

            _musicSyncRule = settings.MusicSyncRule;
            OnPropertyChanged(nameof(MusicSyncRule));
            _podcastSyncRule = settings.PodcastSyncRule;
            OnPropertyChanged(nameof(PodcastSyncRule));
            _videoSyncRule = settings.VideoSyncRule;
            OnPropertyChanged(nameof(VideoSyncRule));
            _picturesSyncRule = settings.PicturesSyncRule;
            OnPropertyChanged(nameof(PicturesSyncRule));
            _wirelessSyncEnabled = settings.WirelessSyncEnabled;
            OnPropertyChanged(nameof(WirelessSyncEnabled));
            _networkName = settings.NetworkName;
            OnPropertyChanged(nameof(NetworkName));

            _firstLaunchCompleted = settings.FirstLaunchCompleted;
            OnPropertyChanged(nameof(FirstLaunchCompleted));
            _whatsNewSeenVersion = settings.WhatsNewSeenVersion;
            OnPropertyChanged(nameof(WhatsNewSeenVersion));

            FirstConnectCompletedSerials.Clear();
            if (settings.FirstConnectCompletedSerials != null)
            {
                FirstConnectCompletedSerials.AddRange(settings.FirstConnectCompletedSerials);
            }

            if (!string.IsNullOrWhiteSpace(settings.SelectedAccentName))
            {
                var accent = AccentColors.FirstOrDefault(a => a.Name == settings.SelectedAccentName);
                if (accent != null)
                {
                    _selectedAccent = accent;
                    ApplyAccent(accent);
                    OnPropertyChanged(nameof(SelectedAccent));
                }
            }

            if (!string.IsNullOrWhiteSpace(settings.SelectedBackgroundName))
            {
                var theme = BackgroundThemes.FirstOrDefault(t => t.Name == settings.SelectedBackgroundName);
                if (theme != null)
                {
                    _selectedBackground = theme;
                    BackgroundArtChanged?.Invoke(this, theme.AssetUri);
                    OnPropertyChanged(nameof(SelectedBackground));
                }
            }

            if (!string.IsNullOrWhiteSpace(settings.SelectedThemeName))
            {
                var theme = ThemeOptions.FirstOrDefault(t => t.Name == settings.SelectedThemeName);
                if (theme != null)
                {
                    _selectedTheme = theme;
                    ApplyTheme(theme);
                    OnPropertyChanged(nameof(SelectedTheme));
                }
            }
        }
        catch (Exception)
        {
            // Corrupt settings should never break startup; defaults apply.
        }
    }

    /// <summary>Explicit save (used by external callers, e.g. MainShell's FirstConnect wizard).</summary>
    public void Persist() => SaveCurrentSettings();

    private void SaveCurrentSettings()
    {
        if (_settingsStore == null || _isRestoringSettings)
        {
            return;
        }

        _settingsStore.Save(new AppSettings
        {
            MusicFolderPath = MusicFolderPath,
            AutoWatchFolder = AutoWatchFolder,
            StartupView = StartupView,
            CrossfadeEnabled = CrossfadeEnabled,
            CrossfadeDurationSeconds = CrossfadeDurationSeconds,
            GaplessPlaybackEnabled = GaplessPlaybackEnabled,
            SoundEffectsEnabled = SoundEffectsEnabled,
            VolumeLevelingEnabled = VolumeLevelingEnabled,
            CompactModeAlwaysOnTop = CompactModeAlwaysOnTop,
            SelectedRipFormat = SelectedRipFormat,
            SelectedRipBitrate = SelectedRipBitrate,
            RipDestinationFolder = RipDestinationFolder,
            AutoRipCdOnInsert = AutoRipCdOnInsert,
            EjectCdAfterRip = EjectCdAfterRip,
            SelectedDiscType = SelectedDiscType,
            SelectedBurnSpeed = SelectedBurnSpeed,
            ApplyVolumeLevelingToBurn = ApplyVolumeLevelingToBurn,
            AutoFetchMetadata = AutoFetchMetadata,
            AutoDownloadArtistArt = AutoDownloadArtistArt,
            WriteTagsToFile = WriteTagsToFile,
            MusicBrainzEnabled = MusicBrainzEnabled,
            LastFmEnabled = LastFmEnabled,
            LrcLibEnabled = LrcLibEnabled,
            FanartTvApiKey = FanartTvApiKey,
            SpaceReservationPercent = SpaceReservationPercent,
            MusicSyncRule = MusicSyncRule,
            PodcastSyncRule = PodcastSyncRule,
            VideoSyncRule = VideoSyncRule,
            PicturesSyncRule = PicturesSyncRule,
            WirelessSyncEnabled = WirelessSyncEnabled,
            NetworkName = NetworkName,
            SelectedAccentName = SelectedAccent.Name,
            SelectedBackgroundName = SelectedBackground.Name,
            SelectedThemeName = SelectedTheme.Name,
            FirstLaunchCompleted = _firstLaunchCompleted,
            WhatsNewSeenVersion = _whatsNewSeenVersion,
            PodcastKeepEpisodes = SelectedPodcastKeepEpisodes,
            PodcastAutoDownload = PodcastAutoDownload,
            IngestExtensions = IngestExtensions,
            UsageDataOptIn = UsageDataOptIn,
            AutoCheckForUpdates = AutoCheckForUpdates,
            PhotoFolderPath = PhotoFolderPath,
            SlideshowShuffle = SlideshowShuffle,
            SlideshowRepeat = SlideshowRepeat,
            DeletePhotosAfterReverseSync = DeletePhotosAfterReverseSync,
            ShowRatings = ShowRatings,
            FirstConnectCompletedSerials = FirstConnectCompletedSerials,
            FirstConnectDeviceName = FirstConnectDeviceName,
        });
    }

    private async Task OnSelectFolderAsync()
    {
        if (_folderPicker == null) return;
        var selected = await _folderPicker.PickFolderAsync("Select Music Collection Folder");
        if (!string.IsNullOrWhiteSpace(selected))
        {
            MusicFolderPath = selected;
            await ScanFolderAsync(selected);
        }
    }

    private async Task OnPickPhotoFolderAsync()
    {
        if (_folderPicker == null) return;
        var selected = await _folderPicker.PickFolderAsync("Select Photo Collection Folder");
        if (!string.IsNullOrWhiteSpace(selected))
        {
            PhotoFolderPath = selected;
        }
    }

    private async Task OnRescanAsync()
    {
        await ScanFolderAsync(MusicFolderPath);
    }

    private async Task OnClearDemoLibraryAsync()
    {
        if (_libraryService != null)
        {
            await _libraryService.ClearDemoDataAsync();
            ScanStatusText = "Demo placeholder data cleared.";
        }
    }

    private static string NormalizePath(string path)
    {
        if (path.StartsWith("~/") || path.StartsWith("~\\"))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path.Substring(2));
        }
        return path;
    }

    private async Task ScanFolderAsync(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            ScanStatusText = "Please enter or select a valid music directory.";
            return;
        }

        var folderPath = NormalizePath(rawPath);
        if (!Directory.Exists(folderPath))
        {
            ScanStatusText = $"Directory not found: {folderPath}";
            return;
        }

        if (_libraryService == null)
        {
            ScanStatusText = "Library service unavailable.";
            return;
        }

        IsScanning = true;
        ScanProgress = 0.0;
        ScanStatusText = $"Scanning {folderPath}...";

        try
        {
            var progress = new Progress<double>(p =>
            {
                ScanProgress = p;
                ScanStatusText = $"Scanning audio files: {(int)(p * 100)}%";
            });

            await _libraryService.ScanDirectoryAsync(folderPath, progress);
            _soundService?.PlaySyncComplete();
            ScanStatusText = "Library scan complete.";
        }
        catch (Exception ex)
        {
            ScanStatusText = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void ApplyAccent(AccentColorOption accent)
    {
        // Dynamically updates the Application Resource dictionary accent brush
        if (Avalonia.Application.Current?.Resources != null)
        {
            if (Avalonia.Media.Color.TryParse(accent.HexCode, out var color))
            {
                Avalonia.Application.Current.Resources["ZuneAccentBrush"] = new Avalonia.Media.SolidColorBrush(color);
                Avalonia.Application.Current.Resources["SystemAccentColor"] = color;
                var hoverColor = Avalonia.Media.Color.FromArgb(
                    255,
                    (byte)Math.Min(255, color.R + 25),
                    (byte)Math.Min(255, color.G + 25),
                    (byte)Math.Min(255, color.B + 25));
                Avalonia.Application.Current.Resources["ZuneAccentHoverBrush"] = new Avalonia.Media.SolidColorBrush(hoverColor);
            }
        }
    }

    /// <summary>
    /// Light/Dark theme swap (Zune 4.8 shipped a light theme by default for fresh installs
    /// when the startup page was set to Collection). We rewrite the surface/text brush
    /// colors in the application resource dictionary so DynamicResource bindings update
    /// everywhere without restyling every view.
    /// </summary>
    private void ApplyTheme(ThemeOption theme)
    {
        if (Avalonia.Application.Current?.Resources == null)
        {
            return;
        }

        static void SwapBrushColor(System.Collections.Generic.KeyValuePair<object, object?> entry, string newHex)
        {
            if (Avalonia.Media.Color.TryParse(newHex, out var color) && entry.Value is Avalonia.Media.SolidColorBrush brush)
            {
                brush.Color = color;
            }
        }

        // Dark mode = no change (the brush colors were initialised to the dark values).
        // Light mode = overwrite the runtime brush colors with the light values.
        if (theme.IsDark)
        {
            SwapBrushColor(new KeyValuePair<object, object?>("ZuneBackgroundBrush",      Avalonia.Application.Current.Resources["ZuneBackgroundBrush"]),      "#11090F");
            SwapBrushColor(new KeyValuePair<object, object?>("ZuneSurfaceElevatedBrush", Avalonia.Application.Current.Resources["ZuneSurfaceElevatedBrush"]), "#181818");
            SwapBrushColor(new KeyValuePair<object, object?>("ZuneCardBrush",           Avalonia.Application.Current.Resources["ZuneCardBrush"]),           "#202020");
            SwapBrushColor(new KeyValuePair<object, object?>("ZuneCardHoverBrush",      Avalonia.Application.Current.Resources["ZuneCardHoverBrush"]),      "#282828");
            SwapBrushColor(new KeyValuePair<object, object?>("ZuneBorderBrush",         Avalonia.Application.Current.Resources["ZuneBorderBrush"]),         "#252525");
            SwapBrushColor(new KeyValuePair<object, object?>("ZuneSubtleBorderBrush",   Avalonia.Application.Current.Resources["ZuneSubtleBorderBrush"]),   "#2C2C2C");
            SwapBrushColor(new KeyValuePair<object, object?>("ZuneInputBackgroundBrush", Avalonia.Application.Current.Resources["ZuneInputBackgroundBrush"]), "#1A1A1A");
            SwapBrushColor(new KeyValuePair<object, object?>("ZuneTextActiveBrush",     Avalonia.Application.Current.Resources["ZuneTextActiveBrush"]),     "#FFFFFF");
            SwapBrushColor(new KeyValuePair<object, object?>("ZuneTextHoverBrush",      Avalonia.Application.Current.Resources["ZuneTextHoverBrush"]),      "#D0D0D0");
            SwapBrushColor(new KeyValuePair<object, object?>("ZuneTextSecondaryBrush",  Avalonia.Application.Current.Resources["ZuneTextSecondaryBrush"]),  "#888888");
            SwapBrushColor(new KeyValuePair<object, object?>("ZuneTextDimmedBrush",     Avalonia.Application.Current.Resources["ZuneTextDimmedBrush"]),     "#555555");
            SwapBrushColor(new KeyValuePair<object, object?>("ZuneTextWatermarkBrush",  Avalonia.Application.Current.Resources["ZuneTextWatermarkBrush"]),  "#1E1E1E");
        }
        else
        {
            SwapBrushColor(new KeyValuePair<object, object?>("ZuneBackgroundBrush",      Avalonia.Application.Current.Resources["ZuneBackgroundBrush"]),      "#F3EFF1");
            SwapBrushColor(new KeyValuePair<object, object?>("ZuneSurfaceElevatedBrush", Avalonia.Application.Current.Resources["ZuneSurfaceElevatedBrush"]), "#FFFFFF");
            SwapBrushColor(new KeyValuePair<object, object?>("ZuneCardBrush",           Avalonia.Application.Current.Resources["ZuneCardBrush"]),           "#FFFFFF");
            SwapBrushColor(new KeyValuePair<object, object?>("ZuneCardHoverBrush",      Avalonia.Application.Current.Resources["ZuneCardHoverBrush"]),      "#F1EAED");
            SwapBrushColor(new KeyValuePair<object, object?>("ZuneBorderBrush",         Avalonia.Application.Current.Resources["ZuneBorderBrush"]),         "#D8CFD3");
            SwapBrushColor(new KeyValuePair<object, object?>("ZuneSubtleBorderBrush",   Avalonia.Application.Current.Resources["ZuneSubtleBorderBrush"]),   "#E5DDE0");
            SwapBrushColor(new KeyValuePair<object, object?>("ZuneInputBackgroundBrush", Avalonia.Application.Current.Resources["ZuneInputBackgroundBrush"]), "#FFFFFF");
            SwapBrushColor(new KeyValuePair<object, object?>("ZuneTextActiveBrush",     Avalonia.Application.Current.Resources["ZuneTextActiveBrush"]),     "#1A1A1A");
            SwapBrushColor(new KeyValuePair<object, object?>("ZuneTextHoverBrush",      Avalonia.Application.Current.Resources["ZuneTextHoverBrush"]),      "#2A2A2A");
            SwapBrushColor(new KeyValuePair<object, object?>("ZuneTextSecondaryBrush",  Avalonia.Application.Current.Resources["ZuneTextSecondaryBrush"]),  "#6B5F66");
            SwapBrushColor(new KeyValuePair<object, object?>("ZuneTextDimmedBrush",     Avalonia.Application.Current.Resources["ZuneTextDimmedBrush"]),     "#A0939B");
            SwapBrushColor(new KeyValuePair<object, object?>("ZuneTextWatermarkBrush",  Avalonia.Application.Current.Resources["ZuneTextWatermarkBrush"]),  "#E5DDE0");
        }
    }
}
