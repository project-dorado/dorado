using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;
using Dorado.UI.Controls;

namespace Dorado.UI.ViewModels;

public class DeviceViewModel : ViewModelBase
{
    private readonly IDeviceSyncService _deviceSyncService;
    private readonly IMediaLibraryService? _libraryService;
    private readonly ISyncEngine? _syncEngine;
    private readonly ISyncGroupService? _syncGroupService;
    private readonly ISettingsStore? _settingsStore;
    private readonly IVideoLibraryService? _videoLibraryService;
    private readonly IPhotoLibraryService? _photoLibraryService;
    private readonly IPodcastService? _podcastService;
    private readonly ISoundEffectService? _soundService;

    public ObservableCollection<ZuneDevice> Devices { get; } = new();

    private ZuneDevice? _selectedDevice;
    public ZuneDevice? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
            {
                OnPropertyChanged(nameof(HasDevice));
                OnPropertyChanged(nameof(DeviceName));
                OnPropertyChanged(nameof(SerialNumber));
                OnPropertyChanged(nameof(FirmwareVersion));
                OnPropertyChanged(nameof(StorageText));
                OnPropertyChanged(nameof(StorageUsedPercentage));
                OnPropertyChanged(nameof(TotalGb));
                OnPropertyChanged(nameof(FreeGb));
                OnPropertyChanged(nameof(MusicGb));
                OnPropertyChanged(nameof(VideoGb));
                OnPropertyChanged(nameof(PhotoGb));
                OnPropertyChanged(nameof(PodcastGb));
                OnPropertyChanged(nameof(SystemGb));
                OnPropertyChanged(nameof(MusicText));
                OnPropertyChanged(nameof(VideoText));
                OnPropertyChanged(nameof(PhotoText));
                OnPropertyChanged(nameof(PodcastText));
                OnPropertyChanged(nameof(SystemText));
                OnPropertyChanged(nameof(FreeText));
                OnPropertyChanged(nameof(GasGaugeColumns));
                OnPropertyChanged(nameof(StorageSegments));
            }
        }
    }

    public bool HasDevice => SelectedDevice != null;
    public string DeviceName => SelectedDevice?.ModelName ?? "No Zune Connected";
    public string SerialNumber => SelectedDevice?.SerialNumber ?? "Unknown";
    public string FirmwareVersion => SelectedDevice?.FirmwareVersion ?? "4.8";

    public double TotalGb => (SelectedDevice?.CapacityBytes ?? 0) / (1024.0 * 1024 * 1024);
    public double FreeGb => (SelectedDevice?.FreeSpaceBytes ?? 0) / (1024.0 * 1024 * 1024);
    public double MusicGb => (SelectedDevice?.MusicBytes ?? 0) / (1024.0 * 1024 * 1024);
    public double VideoGb => (SelectedDevice?.VideoBytes ?? 0) / (1024.0 * 1024 * 1024);
    public double PhotoGb => (SelectedDevice?.PhotoBytes ?? 0) / (1024.0 * 1024 * 1024);
    public double PodcastGb => (SelectedDevice?.PodcastBytes ?? 0) / (1024.0 * 1024 * 1024);
    public double SystemGb => (SelectedDevice?.SystemBytes ?? 0) / (1024.0 * 1024 * 1024);

    public string MusicText => $"MUSIC: {MusicGb:F1} GB";
    public string VideoText => $"VIDEO: {VideoGb:F1} GB";
    public string PhotoText => $"PICTURES: {PhotoGb:F1} GB";
    public string PodcastText => $"PODCASTS: {PodcastGb:F1} GB";
    public string SystemText => $"SYSTEM: {SystemGb:F1} GB";
    public string FreeText => $"FREE: {FreeGb:F1} GB";

    public string GasGaugeColumns
    {
        get
        {
            double m = Math.Max(0.001, (double)(SelectedDevice?.MusicBytes ?? 0));
            double v = Math.Max(0.001, (double)(SelectedDevice?.VideoBytes ?? 0));
            double p = Math.Max(0.001, (double)(SelectedDevice?.PhotoBytes ?? 0));
            double pod = Math.Max(0.001, (double)(SelectedDevice?.PodcastBytes ?? 0));
            double s = Math.Max(0.001, (double)(SelectedDevice?.SystemBytes ?? 0));
            double f = Math.Max(0.001, (double)(SelectedDevice?.FreeSpaceBytes ?? 1));
            return FormattableString.Invariant($"{m:F3}*,{v:F3}*,{p:F3}*,{pod:F3}*,{s:F3}*,{f:F3}*");
        }
    }

    /// <summary>
    /// Ordered gas-gauge segments using the authentic Zune 4.8 media-type colours:
    /// Music magenta, Video purple, Pictures cyan, Podcasts amber, System grey, Free charcoal.
    /// </summary>
    public IReadOnlyList<ZuneStorageSegment> StorageSegments => new[]
    {
        new ZuneStorageSegment("MUSIC", SelectedDevice?.MusicBytes ?? 0, Color.Parse("#FA2A55"), MusicText),
        new ZuneStorageSegment("VIDEO", SelectedDevice?.VideoBytes ?? 0, Color.Parse("#A200FF"), VideoText),
        new ZuneStorageSegment("PICTURES", SelectedDevice?.PhotoBytes ?? 0, Color.Parse("#1BA1E2"), PhotoText),
        new ZuneStorageSegment("PODCASTS", SelectedDevice?.PodcastBytes ?? 0, Color.Parse("#F09609"), PodcastText),
        new ZuneStorageSegment("SYSTEM", SelectedDevice?.SystemBytes ?? 0, Color.Parse("#444444"), SystemText),
        new ZuneStorageSegment("FREE", SelectedDevice?.FreeSpaceBytes ?? 0, Color.Parse("#222222"), FreeText)
    };

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
            }
        }
    }

    public string ReservedGbText => $"{(TotalGb * (_spaceReservationPercent / 100.0)):F1} GB";
    public string SyncSpaceGbText => $"{(TotalGb * (1.0 - (_spaceReservationPercent / 100.0))):F1} GB";
    public string SpaceReservationSummaryText => $"{SpaceReservationPercent}% ({ReservedGbText}) reserved for device buffer";

    public string StorageText
    {
        get
        {
            if (SelectedDevice == null) return "Connect a Zune device via USB cable.";
            return $"{FreeGb:F1} GB free of {TotalGb:F1} GB";
        }
    }

    public double StorageUsedPercentage
    {
        get
        {
            if (SelectedDevice == null || SelectedDevice.CapacityBytes == 0) return 0.0;
            return (double)(SelectedDevice.CapacityBytes - SelectedDevice.FreeSpaceBytes) / SelectedDevice.CapacityBytes;
        }
    }

    private bool _isSyncing;
    public bool IsSyncing
    {
        get => _isSyncing;
        set
        {
            if (SetProperty(ref _isSyncing, value))
            {
                OnPropertyChanged(nameof(HasSyncToast));
                OnPropertyChanged(nameof(SyncToastInstruction));
            }
        }
    }

    private double _syncProgress;
    public double SyncProgress
    {
        get => _syncProgress;
        set
        {
            if (SetProperty(ref _syncProgress, value))
            {
                OnPropertyChanged(nameof(SyncProgressPercent));
                OnPropertyChanged(nameof(SyncToastText));
            }
        }
    }

    public double SyncProgressPercent => Math.Round(SyncProgress * 100);

    private int _syncItemCount;
    public int SyncItemCount
    {
        get => _syncItemCount;
        set
        {
            if (SetProperty(ref _syncItemCount, value))
            {
                OnPropertyChanged(nameof(SyncToastText));
            }
        }
    }

    /// <summary>SYNCANIMATION / SYNCINSTRUCTIONTOAST / SYNCNOTIFICATION parity.</summary>
    public bool HasSyncToast => IsSyncing;

    public string SyncToastText => IsSyncing
        ? $"SYNCING {SyncItemCount} ITEM{(SyncItemCount == 1 ? string.Empty : "S")} — {SyncProgressPercent}% COMPLETE"
        : string.Empty;

    public string SyncToastInstruction => IsGuestSession
        ? "Guest session — content is copied to the device without removing anything."
        : "Keep your Zune connected via USB. Wireless sync can be enabled in Settings → Device.";

    public string SyncStatusText => IsSyncing ? "SYNCING..." : (HasDevice ? "CONNECTED" : "CONNECT USB");

    private string? _statusText;
    public string? StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public ICommand SyncCommand { get; }

    // ==========================================
    // SYNC ENGINE (SchemaSyncGroup parity)
    // ==========================================
    public ObservableCollection<TransferItem> PlannedItems { get; } = new();
    public ObservableCollection<DeviceContentItem> DeviceContents { get; } = new();
    public ObservableCollection<DeviceContentItem> PendingImports { get; } = new();

    private SyncPlan? _currentPlan;
    public SyncPlan? CurrentPlan
    {
        get => _currentPlan;
        private set
        {
            if (SetProperty(ref _currentPlan, value))
            {
                OnPropertyChanged(nameof(HasSyncPlan));
                OnPropertyChanged(nameof(PlanSummaryText));
                OnPropertyChanged(nameof(PlanFreeSpaceText));
                PlannedItems.Clear();
                if (value != null)
                {
                    foreach (var item in value.Items)
                    {
                        PlannedItems.Add(item);
                    }
                }
            }
        }
    }

    public bool HasSyncPlan => CurrentPlan != null && CurrentPlan.Items.Any(i => i.Action != TransferAction.Keep);

    public string PlanSummaryText => CurrentPlan == null
        ? string.Empty
        : $"{CurrentPlan.AddCount} to add • {CurrentPlan.RemoveCount} to remove • {CurrentPlan.KeepCount} kept";

    public string PlanFreeSpaceText => CurrentPlan == null
        ? string.Empty
        : $"Projected free: {(SelectedDevice != null ? (FreeGb + (CurrentPlan.TotalRemoveBytes - CurrentPlan.TotalAddBytes) / (1024.0 * 1024 * 1024)) : 0):F1} GB";

    private bool _isGuestSession;
    public bool IsGuestSession
    {
        get => _isGuestSession;
        private set
        {
            if (SetProperty(ref _isGuestSession, value))
            {
                OnPropertyChanged(nameof(GuestSessionBadgeText));
                OnPropertyChanged(nameof(SyncToastInstruction));
            }
        }
    }

    public string GuestSessionBadgeText => IsGuestSession ? "GUEST SESSION" : string.Empty;

    private DeviceContentItem? _selectedDeviceContent;
    public DeviceContentItem? SelectedDeviceContent
    {
        get => _selectedDeviceContent;
        set => SetProperty(ref _selectedDeviceContent, value);
    }

    public ICommand BuildSyncPlanCommand { get; }
    public ICommand StartGuestSessionCommand { get; }
    public ICommand EndGuestSessionCommand { get; }
    public ICommand QueueCopyBackCommand { get; }
    public ICommand RefreshDeviceContentsCommand { get; }

    public DeviceViewModel(
        IDeviceSyncService deviceSyncService,
        IMediaLibraryService? libraryService = null,
        ISyncEngine? syncEngine = null,
        ISettingsStore? settingsStore = null,
        IVideoLibraryService? videoLibraryService = null,
        IPhotoLibraryService? photoLibraryService = null,
        IPodcastService? podcastService = null,
        ISoundEffectService? soundService = null,
        ISyncGroupService? syncGroupService = null)
    {
        _deviceSyncService = deviceSyncService;
        _libraryService = libraryService;
        _syncEngine = syncEngine;
        _syncGroupService = syncGroupService;
        _settingsStore = settingsStore;
        _videoLibraryService = videoLibraryService;
        _photoLibraryService = photoLibraryService;
        _podcastService = podcastService;
        _soundService = soundService;

        _deviceSyncService.DeviceConnected += OnDeviceConnected;
        _deviceSyncService.DeviceDisconnected += OnDeviceDisconnected;

        SyncCommand = new AsyncRelayCommand(OnSyncAsync);
        BuildSyncPlanCommand = new AsyncRelayCommand(OnBuildSyncPlanAsync);
        StartGuestSessionCommand = new AsyncRelayCommand(OnStartGuestSessionAsync);
        EndGuestSessionCommand = new RelayCommand(OnEndGuestSession);
        QueueCopyBackCommand = new RelayCommand<DeviceContentItem>(OnQueueCopyBack);
        RefreshDeviceContentsCommand = new RelayCommand(RefreshDeviceContents);

        RefreshDevices();
    }

    private void RefreshDevices()
    {
        Devices.Clear();
        foreach (var dev in _deviceSyncService.ConnectedDevices)
        {
            Devices.Add(dev);
        }
        SelectedDevice = Devices.FirstOrDefault();
    }

    private void OnDeviceConnected(object? sender, ZuneDevice dev)
    {
        Devices.Add(dev);
        if (SelectedDevice == null) SelectedDevice = dev;
    }

    private void OnDeviceDisconnected(object? sender, string serial)
    {
        var existing = Devices.FirstOrDefault(d => d.SerialNumber == serial);
        if (existing != null) Devices.Remove(existing);
        if (SelectedDevice?.SerialNumber == serial) SelectedDevice = Devices.FirstOrDefault();
    }

    private async Task OnBuildSyncPlanAsync()
    {
        if (_syncEngine == null || SelectedDevice == null)
        {
            return;
        }

        try
        {
            var settings = _settingsStore?.Load() ?? new AppSettings();
            var group = BuildGroupForDevice(settings);
            var transport = _syncEngine.GetTransport(SelectedDevice.SerialNumber, SelectedDevice.ModelName, SelectedDevice.CapacityBytes);
            var input = await BuildSyncInputAsync();
            var plan = _syncEngine.BuildPlan(group, input, transport);
            CurrentPlan = plan;
            RefreshDeviceContents();
        }
        catch (Exception ex)
        {
            StatusText = $"Sync plan failed: {ex.Message}";
        }
    }

    private SyncGroup BuildGroupForDevice(AppSettings settings)
    {
        var serial = SelectedDevice?.SerialNumber ?? string.Empty;
        if (_syncGroupService != null && !IsGuestSession)
        {
            // Prefer the persisted group (ZMDB sync-group parity); fall back to defaults.
            var persisted = _syncGroupService.GetForDeviceAsync(serial).GetAwaiter().GetResult();
            if (persisted != null)
            {
                return persisted;
            }
        }

        return _syncEngine!.BuildDefaultGroup(serial, settings, IsGuestSession);
    }

    private async Task<SyncInput> BuildSyncInputAsync()
    {
        var tracks = _libraryService != null ? await _libraryService.GetAllTracksAsync() : Array.Empty<Track>();
        var videos = _videoLibraryService != null ? await _videoLibraryService.GetAllVideosAsync() : Array.Empty<Video>();
        var photos = _photoLibraryService != null ? await _photoLibraryService.GetAllPhotosAsync() : Array.Empty<Photo>();
        var episodes = Array.Empty<PodcastEpisode>();
        if (_podcastService != null)
        {
            try
            {
                var series = await _podcastService.GetAllPodcastsAsync();
                episodes = series.SelectMany(s => s.Episodes).ToArray();
            }
            catch
            {
            }
        }

        return new SyncInput
        {
            Tracks = tracks,
            Videos = videos,
            Photos = photos,
            PodcastEpisodes = episodes
        };
    }

    private async Task OnStartGuestSessionAsync()
    {
        IsGuestSession = true;
        CurrentPlan = null;
        await OnBuildSyncPlanAsync();
    }

    private void OnEndGuestSession()
    {
        IsGuestSession = false;
        CurrentPlan = null;
    }

    private void OnQueueCopyBack(DeviceContentItem? item)
    {
        if (item == null || PendingImports.Any(i => i.EntityId == item.EntityId))
        {
            return;
        }

        PendingImports.Add(item);
    }

    private void RefreshDeviceContents()
    {
        DeviceContents.Clear();
        if (_syncEngine == null || SelectedDevice == null)
        {
            return;
        }

        try
        {
            var transport = _syncEngine.GetTransport(SelectedDevice.SerialNumber, SelectedDevice.ModelName, SelectedDevice.CapacityBytes);
            foreach (var content in transport.GetContents())
            {
                DeviceContents.Add(content);
            }
        }
        catch
        {
        }
    }

    /// <summary>Folds the transport's live byte accounting into the gas gauge (ZuneDevice).</summary>
    private void ApplyTransportToGauge()
    {
        if (_syncEngine == null || SelectedDevice == null)
        {
            return;
        }

        try
        {
            var transport = _syncEngine.GetTransport(SelectedDevice.SerialNumber, SelectedDevice.ModelName, SelectedDevice.CapacityBytes);
            var contents = transport.GetContents();
            SelectedDevice.MusicBytes = contents.Where(c => c.Category == SyncCategoryType.Music).Sum(c => c.SizeBytes);
            SelectedDevice.PodcastBytes = contents.Where(c => c.Category == SyncCategoryType.Podcasts).Sum(c => c.SizeBytes);
            SelectedDevice.VideoBytes = contents.Where(c => c.Category == SyncCategoryType.Videos).Sum(c => c.SizeBytes);
            SelectedDevice.PhotoBytes = contents.Where(c => c.Category == SyncCategoryType.Pictures).Sum(c => c.SizeBytes);
            SelectedDevice.SystemBytes = transport.SystemBytes;
            SelectedDevice.FreeSpaceBytes = transport.FreeBytes;

            // Re-raise all gauge bindings.
            OnPropertyChanged(nameof(StorageText));
            OnPropertyChanged(nameof(StorageUsedPercentage));
            OnPropertyChanged(nameof(TotalGb));
            OnPropertyChanged(nameof(FreeGb));
            OnPropertyChanged(nameof(MusicGb));
            OnPropertyChanged(nameof(VideoGb));
            OnPropertyChanged(nameof(PhotoGb));
            OnPropertyChanged(nameof(PodcastGb));
            OnPropertyChanged(nameof(SystemGb));
            OnPropertyChanged(nameof(MusicText));
            OnPropertyChanged(nameof(VideoText));
            OnPropertyChanged(nameof(PhotoText));
            OnPropertyChanged(nameof(PodcastText));
            OnPropertyChanged(nameof(SystemText));
            OnPropertyChanged(nameof(FreeText));
            OnPropertyChanged(nameof(GasGaugeColumns));
            OnPropertyChanged(nameof(ReservedGbText));
            OnPropertyChanged(nameof(SyncSpaceGbText));
            OnPropertyChanged(nameof(SpaceReservationSummaryText));
        }
        catch
        {
        }
    }

    private async Task OnSyncAsync()
    {
        if (SelectedDevice == null) return;
        try
        {
            IsSyncing = true;
            SyncProgress = 0.0;

            if (_syncEngine != null)
            {
                var settings = _settingsStore?.Load() ?? new AppSettings();
                var group = BuildGroupForDevice(settings);
                var transport = _syncEngine.GetTransport(SelectedDevice.SerialNumber, SelectedDevice.ModelName, SelectedDevice.CapacityBytes);

                if (CurrentPlan == null)
                {
                    var input = await BuildSyncInputAsync();
                    CurrentPlan = _syncEngine.BuildPlan(group, input, transport);
                }

                var workItems = CurrentPlan.Items.Count(i => i.Action != TransferAction.Keep);
                SyncItemCount = workItems;

                await _syncEngine.ApplyPlanAsync(CurrentPlan, transport, new Progress<double>(p => SyncProgress = p));
                ApplyTransportToGauge();
                RefreshDeviceContents();
                CurrentPlan = null;
                _soundService?.PlaySyncComplete();
            }
            else
            {
                // Legacy fallback: no engine wired (older tests), plain progress blob.
                var progressReporter = new Progress<double>(p => SyncProgress = p);
                await _deviceSyncService.SyncDeviceAsync(SelectedDevice.SerialNumber, progressReporter);
                SyncProgress = 1.0;
            }
        }
        finally
        {
            IsSyncing = false;
        }
    }
}
