using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Events;
using Dorado.Application.Interfaces;
using Dorado.Application.Services;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;
using Dorado.Infrastructure.Audio;
using Dorado.Plugins.Host;
using Dorado.UI.Navigation;

namespace Dorado.UI.ViewModels;

public enum NavigationPivot
{
    Quickplay,
    Collection,
    NowPlaying,
    Device,
    Settings,
    Social,
    Disc,
    Mixview
}

public class MainShellViewModel : ViewModelBase
{
    private readonly IPlayerCoordinator _playerCoordinator;
    private readonly IMediaLibraryService _libraryService;
    private readonly IDeviceSyncService _deviceSyncService;
    private readonly ISoundEffectService? _soundEffectService;
    private readonly IUserStatsService? _userStatsService;
    private readonly IPodcastService? _podcastService;
    private readonly IVideoLibraryService? _videoLibraryService;
    private readonly IDialogService? _dialogService;

    private NavigationPivot _activePivot = NavigationPivot.Quickplay;
    private readonly Stack<NavigationPivot> _navigationHistory = new();
    private bool _isNavigatingBack;
    private ViewModelBase _currentView = null!;

    public PageStack NavigationStack { get; } = new();
    public bool HasDisc => CDVM != null && (CDVM.HasDisc || IsDiscActive || CDVM.IsRealDriveAvailable);

    private bool _isCompactMode;
    private string? _selectedBackgroundArt = "avares://Dorado.UI/Assets/Zune/Backgrounds/DORADO-BACKGROUND-01.PNG";

    private int _equalizerFrame = 1;
    private readonly DispatcherTimer? _equalizerTimer;
    private readonly DispatcherTimer? _positionTimer;
    private DispatcherTimer? _nowPlayingIdleTimer;
    private DateTime _lastUserInputAt = DateTime.UtcNow;
    private bool _isNowPlayingButtonHovered;
    private bool _isNowPlayingButtonPressed;
    private bool _isNowPlayingPlaying;


    // In-shell modal dialog state (Phase 19a).
    private TaskCompletionSource<bool>? _dialogCompletion;
    private bool _isDialogOpen;
    private string _dialogTitle = string.Empty;
    private string _dialogMessage = string.Empty;
    private string _dialogConfirmText = "OK";
    private string _dialogCancelText = "CANCEL";
    private bool _hasDialogCancel = true;
    private bool _isDialogDestructive;
    private TaskCompletionSource<string?>? _promptCompletion;
    private bool _isDialogPrompt;
    private string _dialogInput = string.Empty;

    // Tier A3: idle screensaver for Now Playing. _nowPlayingIdleProgress 0..1 (1 = fully idle);
    // _nowPlayingArtRotation 0..360 degrees; both advance via the idle timer while the user is inactive
    // in Now Playing. ResetNowPlayingIdle() is called from any user input.
    private double _nowPlayingIdleProgress;
    private double _nowPlayingArtRotation;
    private const double IdleStartSeconds = 3.5;
    private const double IdleFullSeconds = 12.0;
    private const double ArtRotationPeriodSeconds = 90.0;

    public QuickplayViewModel QuickplayVM { get; }
    public CollectionViewModel CollectionVM { get; }
    public NowPlayingViewModel NowPlayingVM { get; }
    public DeviceViewModel DeviceVM { get; }
    public SettingsViewModel SettingsVM { get; }
    public ZuneCardViewModel ZuneCardVM { get; }
    public MixviewViewModel MixviewVM { get; } = null!;
    public CDViewModel CDVM { get; } = null!;

    public event EventHandler<bool>? CompactModeChanged;

    public bool IsCompactMode
    {
        get => _isCompactMode;
        set
        {
            if (SetProperty(ref _isCompactMode, value))
            {
                CompactModeChanged?.Invoke(this, value);
                OnPropertyChanged(nameof(IsQuickDockVisible));
            }
        }
    }

    public string? SelectedBackgroundArt
    {
        get => _selectedBackgroundArt;
        set
        {
            if (SetProperty(ref _selectedBackgroundArt, value))
            {
                OnPropertyChanged(nameof(HasBackgroundArt));
            }
        }
    }

    public bool HasBackgroundArt => !string.IsNullOrEmpty(SelectedBackgroundArt);

    /// <summary>
    /// Now Playing equalizer mark state (Vector 4). The view builds the geometry
    /// from these via <c>EqualizerGeometryConverter</c>, so the ViewModel never
    /// touches Avalonia geometry (and stays platform-free for tests).
    /// </summary>
    public int NowPlayingIconFrame => _equalizerFrame;

    public bool NowPlayingIconPlaying => _isNowPlayingPlaying;

    /// <summary>Pointer hover state of the Now Playing button (drives the HOVER icon variant).</summary>
    public bool NowPlayingIconHovered => _isNowPlayingButtonHovered;

    /// <summary>Pointer pressed state of the Now Playing button (drives the PRESSED icon variant).</summary>
    public bool NowPlayingIconPressed => _isNowPlayingButtonPressed;

    /// <summary>Tier A3: 0..1 idle progress in Now Playing (1 = fully idle, controls faded).</summary>
    public double NowPlayingIdleProgress => _nowPlayingIdleProgress;

    /// <summary>Tier A3: 0..360 degree rotation for the Now Playing album art while idle.</summary>
    public double NowPlayingArtRotation => _nowPlayingArtRotation;

    /// <summary>True once the idle threshold has been crossed and the screensaver is engaged.</summary>
    public bool IsNowPlayingIdle => _nowPlayingIdleProgress > 0.0;

    /// <summary>
    /// Code-behind hook: any user input (key, pointer, focus) resets the Now Playing idle clock.
    /// </summary>
    public void ResetNowPlayingIdle() => _lastUserInputAt = DateTime.UtcNow;

    /// <summary>Code-behind hook: pointer entered the Now Playing button.</summary>
    public void NotifyNowPlayingButtonHover(bool isHovering) => SetNowPlayingHoverState(isHovering);

    /// <summary>Code-behind hook: pointer pressed/released the Now Playing button.</summary>
    public void NotifyNowPlayingButtonPressed(bool isPressed) => SetNowPlayingPressedState(isPressed);

    private void SetNowPlayingHoverState(bool isHovering)
    {
        if (_isNowPlayingButtonHovered == isHovering) return;
        _isNowPlayingButtonHovered = isHovering;
        RefreshNowPlayingIcon();
    }

    private void SetNowPlayingPressedState(bool isPressed)
    {
        if (_isNowPlayingButtonPressed == isPressed) return;
        _isNowPlayingButtonPressed = isPressed;
        RefreshNowPlayingIcon();
    }

    /// <summary>
    /// Notifies the view that the Now Playing equalizer mark changed. The
    /// geometry itself is built by the view from <see cref="NowPlayingIconFrame"/>
    /// and <see cref="NowPlayingIconPlaying"/>.
    /// </summary>
    private void RefreshNowPlayingIcon()
    {
        OnPropertyChanged(nameof(NowPlayingIconFrame));
        OnPropertyChanged(nameof(NowPlayingIconPlaying));
        OnPropertyChanged(nameof(NowPlayingIconHovered));
        OnPropertyChanged(nameof(NowPlayingIconPressed));
    }

    private string _headerSearchQuery = string.Empty;
    public string HeaderSearchQuery
    {
        get => _headerSearchQuery;
        set
        {
            if (SetProperty(ref _headerSearchQuery, value))
            {
                CollectionVM.SearchQuery = value;
                OnPropertyChanged(nameof(HasHeaderSearchQuery));
                UpdateSearchSuggestions(value);
                if (!string.IsNullOrWhiteSpace(value) && ActivePivot != NavigationPivot.Collection)
                {
                    ActivePivot = NavigationPivot.Collection;
                }
            }
        }
    }

    public bool HasHeaderSearchQuery => !string.IsNullOrWhiteSpace(HeaderSearchQuery);

    public ObservableCollection<string> SearchSuggestions { get; } = new();

    public bool HasSearchSuggestions => SearchSuggestions.Count > 0 && !string.IsNullOrWhiteSpace(HeaderSearchQuery);

    private CancellationTokenSource? _searchCts;

    private void UpdateSearchSuggestions(string query)
    {
        SearchSuggestions.Clear();
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            OnPropertyChanged(nameof(HasSearchSuggestions));
            return;
        }

        var lower = query.Trim().ToLowerInvariant();
        var suggestions = new List<string>();

        suggestions.AddRange(CollectionVM.Artists
            .Where(a => a.Name.ToLowerInvariant().Contains(lower))
            .Take(2)
            .Select(a => a.Name));

        suggestions.AddRange(CollectionVM.Albums
            .Where(a => a.Title.ToLowerInvariant().Contains(lower))
            .Take(2)
            .Select(a => a.Title));

        suggestions.AddRange(CollectionVM.Songs
            .Where(s => s.Title.ToLowerInvariant().Contains(lower))
            .Take(2)
            .Select(s => s.Title));

        foreach (var suggestion in suggestions.Distinct().Take(6))
        {
            SearchSuggestions.Add(suggestion);
        }

        // Async podcast + video matches (debounced via cancellation token)
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        _ = SearchExtendedAsync(query, _searchCts.Token);

        OnPropertyChanged(nameof(HasSearchSuggestions));
    }

    private async Task SearchExtendedAsync(string query, CancellationToken ct)
    {
        var lower = query.Trim().ToLowerInvariant();
        var added = new List<string>();

        if (_podcastService != null)
        {
            try
            {
                var podcasts = await _podcastService.GetAllPodcastsAsync();
                foreach (var series in podcasts)
                {
                    if (ct.IsCancellationRequested) return;
                    if (series.Title?.ToLowerInvariant().Contains(lower) == true)
                    {
                        added.Add(series.Title);
                    }
                }
            }
            catch
            {
                // Search is best-effort; ignore service failures
            }
        }

        if (_videoLibraryService != null)
        {
            try
            {
                var videos = await _videoLibraryService.GetAllVideosAsync();
                foreach (var video in videos)
                {
                    if (ct.IsCancellationRequested) return;
                    if (video.Title?.ToLowerInvariant().Contains(lower) == true)
                    {
                        added.Add(video.Title);
                    }
                }
            }
            catch
            {
                // best-effort
            }
        }

        try
        {
            var playlists = await _libraryService.GetAllPlaylistsAsync();
            foreach (var playlist in playlists)
            {
                if (ct.IsCancellationRequested) return;
                if (playlist.Name?.ToLowerInvariant().Contains(lower) == true)
                {
                    added.Add(playlist.Name);
                }
            }
        }
        catch
        {
            // best-effort
        }

        if (ct.IsCancellationRequested || added.Count == 0) return;

        // Append the additional matches (deduped) without evicting the synchronous ones.
        var existing = new HashSet<string>(SearchSuggestions, StringComparer.OrdinalIgnoreCase);
        foreach (var s in added.Where(s => !existing.Contains(s)).Take(4))
        {
            SearchSuggestions.Add(s);
            if (SearchSuggestions.Count >= 10) break;
        }

        OnPropertyChanged(nameof(HasSearchSuggestions));
    }

    public bool IsQuickDockVisible => !IsNowPlayingActive && !IsCompactMode;

    /// <summary>
    /// Official chrome parity (ShowSearch per page): Quickplay, Playback (Now Playing)
    /// and the settings frame hide the header search box; collection-like lands show it.
    /// </summary>
    public bool IsHeaderSearchVisible
        => !IsQuickplayActive && !IsNowPlayingActive && !IsSettingsActive && !IsCompactMode;

    /// <summary>
    /// The Zune 4.8 cropped-header title: the current view's title on detail pages
    /// (Now Playing, Mixview, wizard overlays). Landing pivots do NOT render this — the
    /// header shows the ZUNE wordmark instead so the active pivot name is not duplicated
    /// in the top-left. Rendered with negative left margin so the text bleeds off the
    /// viewport — the title is the back affordance (Tier A1).
    /// </summary>
    public string CroppedHeaderTitle
    {
        get
        {
            if (IsFirstLaunchWizardOpen && FirstLaunchWizardVM != null) return FirstLaunchWizardVM.StepTitle;
            if (IsFirstConnectWizardOpen && FirstConnectWizardVM != null) return FirstConnectWizardVM.StepTitle;
            if (IsWhatsNewOpen && WhatsNewVM != null) return WhatsNewVM.Title;

            if (IsCollectionActive && CollectionVM.SelectedArtist != null)
            {
                return CollectionVM.SelectedArtist.Name.ToUpperInvariant();
            }

            return ActivePivot switch
            {
                NavigationPivot.Quickplay  => "QUICKPLAY",
                NavigationPivot.Collection => "COLLECTION",
                NavigationPivot.NowPlaying  => "NOW PLAYING",
                NavigationPivot.Device     => "DEVICE",
                NavigationPivot.Settings   => "SETTINGS",
                NavigationPivot.Social     => "SOCIAL",
                NavigationPivot.Disc       => "DISC",
                NavigationPivot.Mixview    => "MIXVIEW",
                _ => string.Empty,
            };
        }
    }

    /// <summary>
    /// True when the cropped-header click should navigate back: true for detail pages and wizard overlays,
    /// also true on landing pages that have history (e.g., Quickplay after leaving Now Playing).
    /// </summary>
    public bool IsCroppedHeaderBack => CanGoBack || IsNowPlayingActive || IsMixviewActive
        || IsFirstLaunchWizardOpen || IsFirstConnectWizardOpen || IsWhatsNewOpen
        || (IsCollectionActive && CollectionVM.SelectedArtist != null);

    /// <summary>
    /// Detail pages (Now Playing / Mixview / wizards / artist drilldown) get the back glyph + the view title;
    /// landing pivots get just the title (since there's no view to back out of).
    /// </summary>
    public bool IsCroppedHeaderDetail => IsNowPlayingActive || IsMixviewActive
        || IsFirstLaunchWizardOpen || IsFirstConnectWizardOpen || IsWhatsNewOpen
        || (IsCollectionActive && CollectionVM.SelectedArtist != null);

    public string QuickDockDeviceName => DeviceVM.HasDevice ? DeviceVM.DeviceName.ToUpperInvariant() : "NO DEVICE";
    public string QuickDockDeviceStatus => DeviceVM.HasDevice ? (DeviceVM.IsSyncing ? "SYNCING..." : "CONNECTED") : "CONNECT USB";
    public double QuickDockDeviceOpacity => DeviceVM.HasDevice ? 1.0 : 0.45;
    public string QuickDockDeviceTooltip => DeviceVM.HasDevice 
        ? $"{DeviceVM.DeviceName} connected • Click to view device storage" 
        : "No Zune device attached • Connect via USB";

    public NavigationPivot ActivePivot
    {
        get => _activePivot;
        set
        {
            var previous = _activePivot;
            if (SetProperty(ref _activePivot, value))
            {
                if (!_isNavigatingBack && previous != value)
                {
                    _navigationHistory.Push(previous);
                    NavigationStack.Push(new PageStackEntry(
                        value,
                        GetPivotTitle(value),
                        CollectionVM.ActiveMediaGroup,
                        CollectionVM.ActiveSubPivot));
                    OnPropertyChanged(nameof(CanGoBack));
                }

                OnPropertyChanged(nameof(IsQuickplayActive));
                OnPropertyChanged(nameof(IsCollectionActive));
                OnPropertyChanged(nameof(IsNowPlayingActive));
                OnPropertyChanged(nameof(IsDeviceActive));
                OnPropertyChanged(nameof(IsSettingsActive));
                OnPropertyChanged(nameof(IsSocialActive));
                OnPropertyChanged(nameof(IsDiscActive));
                OnPropertyChanged(nameof(IsMixviewActive));
                OnPropertyChanged(nameof(HasDisc));
                OnPropertyChanged(nameof(IsQuickDockVisible));
                OnPropertyChanged(nameof(IsHeaderSearchVisible));
                OnPropertyChanged(nameof(CroppedHeaderTitle));
                OnPropertyChanged(nameof(IsCroppedHeaderBack));
                OnPropertyChanged(nameof(IsCroppedHeaderDetail));

                CurrentView = _activePivot switch
                {
                    NavigationPivot.Quickplay => QuickplayVM,
                    NavigationPivot.Collection => CollectionVM,
                    NavigationPivot.NowPlaying => NowPlayingVM,
                    NavigationPivot.Device => DeviceVM,
                    NavigationPivot.Settings => SettingsVM,
                    NavigationPivot.Social => ZuneCardVM,
                    NavigationPivot.Disc => CDVM,
                    NavigationPivot.Mixview => MixviewVM,
                    _ => QuickplayVM
                };

                if (_activePivot == NavigationPivot.Collection)
                {
                    _ = CollectionVM.RefreshDataAsync();
                }
                else if (_activePivot == NavigationPivot.Quickplay)
                {
                    _ = QuickplayVM.LoadInitialDataAsync();
                }
                else if (_activePivot == NavigationPivot.Social)
                {
                    _ = ZuneCardVM.LoadStatsAsync();
                }
            }
        }
    }

    public bool IsQuickplayActive => _activePivot == NavigationPivot.Quickplay;
    public bool IsCollectionActive => _activePivot == NavigationPivot.Collection;
    public bool IsNowPlayingActive => _activePivot == NavigationPivot.NowPlaying;
    public bool IsDeviceActive => _activePivot == NavigationPivot.Device;
    public bool IsSettingsActive => _activePivot == NavigationPivot.Settings;
    public bool IsSocialActive => _activePivot == NavigationPivot.Social;
    public bool IsDiscActive => _activePivot == NavigationPivot.Disc;
    public bool IsMixviewActive => _activePivot == NavigationPivot.Mixview;

    public ViewModelBase CurrentView
    {
        get => _currentView;
        private set => SetProperty(ref _currentView, value);
    }

    // Playback HUD properties
    public Track? CurrentTrack => _playerCoordinator.CurrentTrack;
    public PlaybackState State => _playerCoordinator.State;
    public bool IsPlaying => State == PlaybackState.Playing;
    public string PlayPauseIcon => IsPlaying ? "⏸" : "▶";
    public TimeSpan CurrentPosition => _playerCoordinator.CurrentPosition;
    public TimeSpan Duration => _playerCoordinator.Duration;

    public double ProgressPercentage
    {
        get
        {
            if (Duration.TotalSeconds <= 0) return 0.0;
            return Math.Clamp(CurrentPosition.TotalSeconds / Duration.TotalSeconds, 0.0, 1.0);
        }
    }

    private bool _showTotalTime;
    public bool ShowTotalTime
    {
        get => _showTotalTime;
        set
        {
            if (SetProperty(ref _showTotalTime, value))
            {
                OnPropertyChanged(nameof(FormattedDurationText));
            }
        }
    }

    public string ElapsedTimeText => CurrentPosition.ToString(@"m\:ss");
    public string RemainingTimeText => (Duration - CurrentPosition).ToString(@"\-m\:ss");

    public string FormattedDurationText => ShowTotalTime
        ? Duration.ToString(@"m\:ss")
        : (Duration - CurrentPosition).ToString(@"\-m\:ss");

    public double Volume
    {
        get => _playerCoordinator.Volume * 100.0;
        set
        {
            _playerCoordinator.Volume = value / 100.0;
            OnPropertyChanged();
        }
    }

    public bool IsMuted
    {
        get => _playerCoordinator.IsMuted;
        set
        {
            if (_playerCoordinator.IsMuted != value)
            {
                _playerCoordinator.IsMuted = value;
                OnPropertyChanged();
            }
        }
    }

    public HeartRating CurrentRating => CurrentTrack?.Rating ?? HeartRating.None;
    public bool IsFavorite => CurrentRating == HeartRating.Favorite;
    public bool IsDisliked => CurrentRating == HeartRating.Dislike;

    public bool Shuffle
    {
        get => _playerCoordinator.Shuffle;
        set
        {
            _playerCoordinator.Shuffle = value;
            OnPropertyChanged();
        }
    }

    public bool Repeat
    {
        get => _playerCoordinator.Repeat;
        set
        {
            _playerCoordinator.Repeat = value;
            OnPropertyChanged();
        }
    }

    // Commands
    public ICommand ConfirmDialogCommand { get; } = null!;
    public ICommand CancelDialogCommand { get; } = null!;
    public ICommand SelectPivotCommand { get; } = null!;
    public ICommand PlayPauseCommand { get; } = null!;
    public ICommand NextCommand { get; } = null!;
    public ICommand PreviousCommand { get; } = null!;
    public ICommand ToggleFavoriteCommand { get; } = null!;
    public ICommand ToggleDislikeCommand { get; } = null!;
    public ICommand ToggleNowPlayingCommand { get; } = null!;
    public ICommand ToggleShuffleCommand { get; } = null!;
    public ICommand ToggleRepeatCommand { get; } = null!;
    public ICommand ToggleTimeDisplayCommand { get; } = null!;
    public ICommand ToggleMuteCommand { get; } = null!;
    public ICommand SeekCommand { get; } = null!;
    public ICommand StopCommand { get; } = null!;
    public ICommand RewindCommand { get; } = null!;
    public ICommand FastForwardCommand { get; } = null!;
    public ICommand ToggleCompactModeCommand { get; } = null!;
    public ICommand ToggleZuneCardCommand { get; } = null!;
    public ICommand ClearSearchCommand { get; } = null!;
    public ICommand OpenDeviceCommand { get; } = null!;
    public ICommand OpenPlaylistsCommand { get; } = null!;
    public ICommand NavigateToMixviewCommand { get; } = null!;
    public ICommand OpenCDCommand { get; } = null!;
    public ICommand AcceptSuggestionCommand { get; } = null!;
    public ICommand GoBackCommand { get; } = null!;
    public bool CanGoBack => _navigationHistory.Count > 0
        || (IsCollectionActive && CollectionVM.SelectedArtist != null);

    private FirstLaunchWizardViewModel? _firstLaunchWizardVM;
    public FirstLaunchWizardViewModel? FirstLaunchWizardVM
    {
        get => _firstLaunchWizardVM;
        set
        {
            if (SetProperty(ref _firstLaunchWizardVM, value))
            {
                OnPropertyChanged(nameof(IsFirstLaunchWizardOpen));
                OnPropertyChanged(nameof(CroppedHeaderTitle));
                OnPropertyChanged(nameof(IsCroppedHeaderBack));
                OnPropertyChanged(nameof(IsCroppedHeaderDetail));
            }
        }
    }

    public bool IsFirstLaunchWizardOpen => FirstLaunchWizardVM != null;

    // ==========================================
    // IN-SHELL MODAL DIALOG (Phase 19a)
    // ==========================================
    public bool IsDialogOpen
    {
        get => _isDialogOpen;
        private set => SetProperty(ref _isDialogOpen, value);
    }

    public string DialogTitle
    {
        get => _dialogTitle;
        private set => SetProperty(ref _dialogTitle, value);
    }

    public string DialogMessage
    {
        get => _dialogMessage;
        private set => SetProperty(ref _dialogMessage, value);
    }

    public string DialogConfirmText
    {
        get => _dialogConfirmText;
        private set => SetProperty(ref _dialogConfirmText, value);
    }

    public string DialogCancelText
    {
        get => _dialogCancelText;
        private set => SetProperty(ref _dialogCancelText, value);
    }

    public bool HasDialogCancel
    {
        get => _hasDialogCancel;
        private set => SetProperty(ref _hasDialogCancel, value);
    }

    public bool IsDialogDestructive
    {
        get => _isDialogDestructive;
        private set => SetProperty(ref _isDialogDestructive, value);
    }

    public bool IsDialogPrompt
    {
        get => _isDialogPrompt;
        private set => SetProperty(ref _isDialogPrompt, value);
    }

    public string DialogInput
    {
        get => _dialogInput;
        set => SetProperty(ref _dialogInput, value);
    }

    private async Task<string?> ShowPromptAsync(DialogRequest request, string? initialValue, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            DialogTitle = request.Title;
            DialogMessage = request.Message;
            DialogConfirmText = request.ConfirmText;
            DialogCancelText = request.CancelText ?? "CANCEL";
            HasDialogCancel = request.CancelText is not null;
            IsDialogDestructive = request.IsDestructive;
            DialogInput = initialValue ?? string.Empty;
            IsDialogPrompt = true;
            _promptCompletion = completion;
            IsDialogOpen = true;
        });

        using var registration = cancellationToken.Register(() => completion.TrySetResult(null));
        return await completion.Task;
    }

    private async Task<bool> ShowDialogAsync(DialogRequest request, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            DialogTitle = request.Title;
            DialogMessage = request.Message;
            DialogConfirmText = request.ConfirmText;
            DialogCancelText = request.CancelText ?? "CANCEL";
            HasDialogCancel = request.CancelText is not null;
            IsDialogDestructive = request.IsDestructive;
            IsDialogPrompt = false;
            _dialogCompletion = completion;
            IsDialogOpen = true;
        });

        using var registration = cancellationToken.Register(() => completion.TrySetResult(false));
        return await completion.Task;
    }

    private void ConfirmDialog()
    {
        IsDialogOpen = false;

        if (IsDialogPrompt)
        {
            IsDialogPrompt = false;
            var prompt = _promptCompletion;
            _promptCompletion = null;
            prompt?.TrySetResult(DialogInput);
            return;
        }

        var completion = _dialogCompletion;
        _dialogCompletion = null;
        completion?.TrySetResult(true);
    }

    private void CancelDialog()
    {
        IsDialogOpen = false;

        if (IsDialogPrompt)
        {
            IsDialogPrompt = false;
            var prompt = _promptCompletion;
            _promptCompletion = null;
            prompt?.TrySetResult(null);
        }

        var completion = _dialogCompletion;
        _dialogCompletion = null;
        completion?.TrySetResult(false);
    }


    private FirstConnectWizardViewModel? _firstConnectWizardVM;
    public FirstConnectWizardViewModel? FirstConnectWizardVM
    {
        get => _firstConnectWizardVM;
        set
        {
            if (SetProperty(ref _firstConnectWizardVM, value))
            {
                OnPropertyChanged(nameof(IsFirstConnectWizardOpen));
                OnPropertyChanged(nameof(CroppedHeaderTitle));
                OnPropertyChanged(nameof(IsCroppedHeaderBack));
                OnPropertyChanged(nameof(IsCroppedHeaderDetail));
            }
        }
    }

    public bool IsFirstConnectWizardOpen => FirstConnectWizardVM != null;

    private WhatsNewViewModel? _whatsNewVM;
    public WhatsNewViewModel? WhatsNewVM
    {
        get => _whatsNewVM;
        set
        {
            if (SetProperty(ref _whatsNewVM, value))
            {
                OnPropertyChanged(nameof(IsWhatsNewOpen));
                OnPropertyChanged(nameof(CroppedHeaderTitle));
                OnPropertyChanged(nameof(IsCroppedHeaderBack));
                OnPropertyChanged(nameof(IsCroppedHeaderDetail));
            }
        }
    }

    public bool IsWhatsNewOpen => WhatsNewVM != null;

    public MainShellViewModel(
        IPlayerCoordinator playerCoordinator,
        IMediaLibraryService libraryService,
        IDeviceSyncService deviceSyncService,
        ISmartDJService smartDJService,
        ISoundEffectService? soundEffectService = null,
        IUserStatsService? userStatsService = null,
        IPodcastService? podcastService = null,
        IFolderPickerService? folderPickerService = null,
        ISettingsStore? settingsStore = null,
        IArtistEnrichmentService? enrichmentService = null,
        IArtworkCacheService? artworkCacheService = null,
        IExternalMetadataService? metadataService = null,
        IAudioOutputEngine? audioEngine = null,
        ISmartPlaylistService? smartPlaylistService = null,
        IVideoLibraryService? videoLibraryService = null,
        IVideoPlaybackEngine? videoEngine = null,
        IPhotoLibraryService? photoLibraryService = null,
        ISyncEngine? syncEngine = null,
        ISyncGroupService? syncGroupService = null,
        PluginManager? pluginManager = null,
        IDynamicMixService? dynamicMixService = null,
        ILocalizationService? localization = null,
        IDialogService? dialogService = null,
        IReviewService? reviewService = null,
        ICloudSocialService? cloudSocialService = null,
        ICloudSignInService? cloudSignInService = null,
        IArtistRelationshipService? artistRelationships = null,
        IOpticalDriveService? opticalDriveService = null)
    {
        _playerCoordinator = playerCoordinator;
        _libraryService = libraryService;
        _deviceSyncService = deviceSyncService;
        _soundEffectService = soundEffectService ?? new SoundEffectService();
        _userStatsService = userStatsService ?? new UserStatsService(libraryService);
        _podcastService = podcastService ?? new PodcastService(playerCoordinator);
        _videoLibraryService = videoLibraryService;

        // Child ViewModels
        QuickplayVM = new QuickplayViewModel(playerCoordinator, libraryService, smartDJService, dynamicMixService);
        CollectionVM = new CollectionViewModel(playerCoordinator, libraryService, _podcastService, smartDJService, artworkCacheService, metadataService, smartPlaylistService, videoLibraryService, videoEngine, photoLibraryService, dialogService, reviewService);
        NowPlayingVM = new NowPlayingViewModel(playerCoordinator, libraryService, enrichmentService, audioEngine, videoLibraryService, videoEngine);
        DeviceVM = new DeviceViewModel(deviceSyncService, libraryService, syncEngine, settingsStore, videoLibraryService, photoLibraryService, _podcastService, _soundEffectService, syncGroupService);
        SettingsVM = new SettingsViewModel(_soundEffectService, folderPickerService, _libraryService, playerCoordinator, deviceSyncService, settingsStore, pluginManager, localization, dialogService, cloudSignInService);

        // Onboarding (FIRSTLAUNCH + WHATSNEW parity): wizard on first run, What's New on version change.
        var startupSettings = settingsStore?.Load();
        if (startupSettings != null && !startupSettings.FirstLaunchCompleted)
        {
            FirstLaunchWizardVM = new FirstLaunchWizardViewModel(
                libraryService,
                folderPickerService,
                settingsStore,
                _soundEffectService,
                string.IsNullOrWhiteSpace(startupSettings.MusicFolderPath) ? null : startupSettings.MusicFolderPath);
            FirstLaunchWizardVM.RequestClose += (_, _) =>
            {
                FirstLaunchWizardVM = null;
                MaybeShowWhatsNew(settingsStore);
            };
            FirstLaunchWizardVM.PropertyChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(CroppedHeaderTitle));
            };
        }
        else
        {
            MaybeShowWhatsNew(settingsStore);
        }
        ZuneCardVM = new ZuneCardViewModel(
            _userStatsService,
            cloudSocialService,
            () => settingsStore?.Load().CloudHandle ?? string.Empty,
            settingsStore,
            folderPickerService);

        var mixService = new MixviewCoordinator(libraryService, artistRelationships);
        MixviewVM = new MixviewViewModel(mixService, playerCoordinator, smartDJService, libraryService);

        NowPlayingVM.LaunchMixviewRequested += async (_, artist) =>
        {
            await MixviewVM.InitializeSeedAsync(artist, MixNodeType.Artist);
            ActivePivot = NavigationPivot.Mixview;
        };

        NavigateToMixviewCommand = new AsyncRelayCommand<string>(async artistName =>
        {
            var seed = string.IsNullOrWhiteSpace(artistName) ? (CurrentTrack?.ArtistName ?? "Zune") : artistName;
            await MixviewVM.InitializeSeedAsync(seed, MixNodeType.Artist);
            ActivePivot = NavigationPivot.Mixview;
        });

        CDVM = new CDViewModel(libraryService, playerCoordinator, _soundEffectService, opticalDriveService);
        CDVM.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CDViewModel.HasDisc))
            {
                OnPropertyChanged(nameof(HasDisc));
            }
        };

        OpenCDCommand = new RelayCommand(() =>
        {
            ActivePivot = NavigationPivot.Disc;
        });

        NavigationStack.Push(new PageStackEntry(NavigationPivot.Quickplay, "QUICKPLAY"));

        CollectionVM.DetailStateChanged += (_, detail) =>
        {
            if (!_isNavigatingBack && IsCollectionActive)
            {
                var title = CollectionVM.SelectedArtist != null
                    ? CollectionVM.SelectedArtist.Name.ToUpperInvariant()
                    : "COLLECTION";
                NavigationStack.Push(new PageStackEntry(
                    NavigationPivot.Collection,
                    title,
                    CollectionVM.ActiveMediaGroup,
                    CollectionVM.ActiveSubPivot,
                    detail));
            }
            OnPropertyChanged(nameof(CroppedHeaderTitle));
            OnPropertyChanged(nameof(IsCroppedHeaderBack));
            OnPropertyChanged(nameof(IsCroppedHeaderDetail));
            OnPropertyChanged(nameof(CanGoBack));
        };

        AcceptSuggestionCommand = new RelayCommand<string>(suggestion =>
        {
            if (!string.IsNullOrWhiteSpace(suggestion))
            {
                HeaderSearchQuery = suggestion;
            }
        });

        GoBackCommand = new RelayCommand(GoBack);

        _currentView = QuickplayVM;

        // Background sync
        SettingsVM.BackgroundArtChanged += (_, artUri) => SelectedBackgroundArt = artUri;

        // Wire player events
        _playerCoordinator.TrackChanged += OnPlayerTrackChanged;
        _playerCoordinator.StateChanged += OnPlayerStateChanged;
        _playerCoordinator.RatingChanged += OnPlayerRatingChanged;

        // Wire device sync events
        _deviceSyncService.DeviceConnected += (_, dev) =>
        {
            _soundEffectService?.PlayNotification();
            NotifyQuickDockChanged();

            // Zune FIRSTCONNECT parity: run the device-arrival wizard once per physical serial.
            if (dev != null
                && !string.IsNullOrWhiteSpace(dev.SerialNumber)
                && !SettingsVM.FirstConnectCompletedSerials.Contains(dev.SerialNumber))
            {
                BeginFirstConnect(dev);
            }
        };
        _deviceSyncService.DeviceDisconnected += (_, _) => NotifyQuickDockChanged();
        DeviceVM.PropertyChanged += (_, _) => NotifyQuickDockChanged();

        // Wire library updates
        _libraryService.LibraryUpdated += async (_, _) =>
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await CollectionVM.RefreshDataAsync();
                await QuickplayVM.LoadInitialDataAsync();
                if (_userStatsService != null)
                {
                    await ZuneCardVM.LoadStatsAsync();
                }
            });
        };

        // Setup commands
        _dialogService = dialogService;
        if (dialogService is Dorado.Application.Services.DialogService host)
        {
            host.ConfirmHandler = ShowDialogAsync;
            host.PromptHandler = ShowPromptAsync;
        }

        ConfirmDialogCommand = new RelayCommand(ConfirmDialog);
        CancelDialogCommand = new RelayCommand(CancelDialog);

        SelectPivotCommand = new RelayCommand<NavigationPivot>(pivot => ActivePivot = pivot);
        PlayPauseCommand = new AsyncRelayCommand(() => _playerCoordinator.PlayPauseAsync());
        NextCommand = new AsyncRelayCommand(() => _playerCoordinator.NextAsync());
        PreviousCommand = new AsyncRelayCommand(() => _playerCoordinator.PreviousAsync());
        ToggleFavoriteCommand = new AsyncRelayCommand(OnToggleFavoriteAsync);
        ToggleDislikeCommand = new AsyncRelayCommand(OnToggleDislikeAsync);
        ToggleShuffleCommand = new RelayCommand(() => Shuffle = !Shuffle);
        ToggleRepeatCommand = new RelayCommand(() => Repeat = !Repeat);
        ToggleTimeDisplayCommand = new RelayCommand(() => ShowTotalTime = !ShowTotalTime);
        ToggleMuteCommand = new RelayCommand(() => IsMuted = !IsMuted);
        ToggleCompactModeCommand = new RelayCommand(() => IsCompactMode = !IsCompactMode);
        ClearSearchCommand = new RelayCommand(() => HeaderSearchQuery = string.Empty);
        OpenDeviceCommand = new RelayCommand(() => ActivePivot = NavigationPivot.Device);
        OpenPlaylistsCommand = new RelayCommand(() =>
        {
            ActivePivot = NavigationPivot.Collection;
            CollectionVM.ActiveSubPivot = CollectionSubPivot.Playlists;
        });
        ToggleZuneCardCommand = new RelayCommand(() =>
        {
            ActivePivot = ActivePivot == NavigationPivot.Social
                ? NavigationPivot.Collection
                : NavigationPivot.Social;
        });

        // Synchronous so a drag can seek repeatedly; AsyncRelayCommand would drop
        // concurrent invocations and the scrubber would only move once per drag.
        SeekCommand = new RelayCommand<double>(progress =>
        {
            if (Duration.TotalSeconds > 0)
            {
                var target = TimeSpan.FromSeconds(Math.Clamp(progress, 0.0, 1.0) * Duration.TotalSeconds);
                _ = _playerCoordinator.SeekAsync(target);
            }
        });

        StopCommand = new AsyncRelayCommand(() => _playerCoordinator.StopAsync());
        RewindCommand = new AsyncRelayCommand(async () =>
        {
            var target = CurrentPosition - TimeSpan.FromSeconds(5);
            await _playerCoordinator.SeekAsync(target < TimeSpan.Zero ? TimeSpan.Zero : target);
        });
        FastForwardCommand = new AsyncRelayCommand(async () =>
        {
            var target = CurrentPosition + TimeSpan.FromSeconds(5);
            if (Duration > TimeSpan.Zero && target > Duration)
                target = Duration;
            await _playerCoordinator.SeekAsync(target);
        });

        ToggleNowPlayingCommand = new RelayCommand(() =>
        {
            ActivePivot = ActivePivot == NavigationPivot.NowPlaying
                ? NavigationPivot.Collection
                : NavigationPivot.NowPlaying;
            // Entering Now Playing: reset the idle clock so the screensaver doesn't engage instantly.
            if (ActivePivot == NavigationPivot.NowPlaying)
            {
                ResetNowPlayingIdle();
            }
        });

        // Initialize Now Playing equalizer animation timer
        try
        {
            _equalizerTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };
            _equalizerTimer.Tick += (s, e) =>
            {
                _equalizerFrame = (_equalizerFrame % 10) + 1;
                RefreshNowPlayingIcon();
            };

            // HUD position cadence. Real audio advances inside the output engine, so the bound
            // progress/elapsed/remaining values only refresh on state changes; without this
            // tick the transport stuck at 0:00 while a track played.
            _positionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _positionTimer.Tick += (_, _) =>
            {
                if (!IsPlaying)
                {
                    return;
                }

                OnPropertyChanged(nameof(CurrentPosition));
                OnPropertyChanged(nameof(ProgressPercentage));
                OnPropertyChanged(nameof(ElapsedTimeText));
                OnPropertyChanged(nameof(RemainingTimeText));
                OnPropertyChanged(nameof(FormattedDurationText));
            };
            _positionTimer.Start();

            // Tier A3: idle screensaver for Now Playing. Ticks at 60fps when in Now Playing and
            // advances _nowPlayingIdleProgress (0..1 over IdleFullSeconds-IdleStartSeconds) plus
            // _nowPlayingArtRotation (one revolution every ArtRotationPeriodSeconds). Reset by user input.
            _nowPlayingIdleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _nowPlayingIdleTimer.Tick += (_, _) =>
            {
                if (ActivePivot != NavigationPivot.NowPlaying)
                {
                    if (_nowPlayingIdleProgress > 0)
                    {
                        _nowPlayingIdleProgress = 0;
                        OnPropertyChanged(nameof(NowPlayingIdleProgress));
                        OnPropertyChanged(nameof(IsNowPlayingIdle));
                    }
                    return;
                }

                var idleSeconds = (DateTime.UtcNow - _lastUserInputAt).TotalSeconds;
                if (idleSeconds < IdleStartSeconds)
                {
                    if (_nowPlayingIdleProgress > 0)
                    {
                        _nowPlayingIdleProgress = 0;
                        _nowPlayingArtRotation = 0;
                        OnPropertyChanged(nameof(NowPlayingIdleProgress));
                        OnPropertyChanged(nameof(NowPlayingArtRotation));
                        OnPropertyChanged(nameof(IsNowPlayingIdle));
                    }
                    return;
                }

                var range = Math.Max(IdleFullSeconds - IdleStartSeconds, 0.1);
                var raw = (idleSeconds - IdleStartSeconds) / range;
                var progress = Math.Clamp(raw, 0.0, 1.0);

                var rotationSeconds = idleSeconds - IdleStartSeconds;
                _nowPlayingArtRotation = (rotationSeconds * 360.0 / ArtRotationPeriodSeconds) % 360.0;

                if (Math.Abs(progress - _nowPlayingIdleProgress) > 0.001)
                {
                    _nowPlayingIdleProgress = progress;
                    OnPropertyChanged(nameof(NowPlayingIdleProgress));
                    OnPropertyChanged(nameof(IsNowPlayingIdle));
                }

                OnPropertyChanged(nameof(NowPlayingArtRotation));
            };

            BeginFirstConnectOnAlreadyConnected();
        }
        catch
        {
            // Fallback for headless test environments where Dispatcher is unavailable
        }
    }

    private void BeginFirstConnectOnAlreadyConnected()
    {
        if (SettingsVM == null || _deviceSyncService == null)
        {
            return;
        }

        foreach (var device in _deviceSyncService.ConnectedDevices)
        {
            if (device != null
                && !string.IsNullOrWhiteSpace(device.SerialNumber)
                && !SettingsVM.FirstConnectCompletedSerials.Contains(device.SerialNumber))
            {
                BeginFirstConnect(device);
                return;
            }
        }
    }

    private void BeginFirstConnect(Dorado.Domain.Models.ZuneDevice device)
    {
        if (SettingsVM == null || FirstConnectWizardVM != null)
        {
            return;
        }

        var vm = new FirstConnectWizardViewModel(device);
        vm.RequestClose += (_, result) =>
        {
            FirstConnectWizardVM = null;

            if (result != null && result.Device != null && !string.IsNullOrWhiteSpace(result.Device.SerialNumber))
            {
                if (!SettingsVM.FirstConnectCompletedSerials.Contains(result.Device.SerialNumber))
                {
                    SettingsVM.FirstConnectCompletedSerials.Add(result.Device.SerialNumber);
                }

                if (result.SyncMusic) SettingsVM.MusicSyncRule = "All Music (Automatic Sync)";
                if (result.SyncVideos) SettingsVM.VideoSyncRule = "All Videos & Pictures";
                if (result.SyncPhotos) SettingsVM.PicturesSyncRule = "Newest 25 Items";
                if (result.SyncPodcasts) SettingsVM.PodcastSyncRule = "3 Newest Episodes";

                SettingsVM.FirstConnectDeviceName = result.DeviceName;
                SettingsVM.Persist();
            }
        };
        vm.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CroppedHeaderTitle));
        FirstConnectWizardVM = vm;
    }

    /// <summary>
    /// Pops the most recent pivot or collection drilldown off the navigation history (PAGESTACK parity).
    /// </summary>
    public void GoBack()
    {
        if (FirstLaunchWizardVM != null)
        {
            FirstLaunchWizardVM = null;
            return;
        }
        if (FirstConnectWizardVM != null)
        {
            FirstConnectWizardVM = null;
            return;
        }
        if (WhatsNewVM != null)
        {
            WhatsNewVM = null;
            return;
        }

        if (IsCollectionActive && CollectionVM.SelectedArtist != null)
        {
            CollectionVM.SelectedArtist = null;
            if (NavigationStack.CanNavigateBack)
            {
                NavigationStack.Pop();
            }
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CroppedHeaderTitle));
            OnPropertyChanged(nameof(IsCroppedHeaderBack));
            OnPropertyChanged(nameof(IsCroppedHeaderDetail));
            return;
        }

        if (_navigationHistory.Count == 0)
        {
            return;
        }

        var previous = _navigationHistory.Pop();
        if (NavigationStack.CanNavigateBack)
        {
            NavigationStack.Pop();
        }

        _isNavigatingBack = true;
        try
        {
            ActivePivot = previous;
        }
        finally
        {
            _isNavigatingBack = false;
        }

        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CroppedHeaderTitle));
        OnPropertyChanged(nameof(IsCroppedHeaderBack));
        OnPropertyChanged(nameof(IsCroppedHeaderDetail));
    }

    private static string GetPivotTitle(NavigationPivot pivot) => pivot switch
    {
        NavigationPivot.Quickplay => "QUICKPLAY",
        NavigationPivot.Collection => "COLLECTION",
        NavigationPivot.NowPlaying => "NOW PLAYING",
        NavigationPivot.Device => "DEVICE",
        NavigationPivot.Settings => "SETTINGS",
        NavigationPivot.Social => "SOCIAL",
        NavigationPivot.Disc => "DISC",
        NavigationPivot.Mixview => "MIXVIEW",
        _ => string.Empty
    };

    private void MaybeShowWhatsNew(ISettingsStore? settingsStore)
    {
        if (settingsStore == null)
        {
            return;
        }

        try
        {
            var settings = settingsStore.Load();
            if (settings.WhatsNewSeenVersion != Dorado.Application.AppInfo.Version)
            {
                WhatsNewVM = new WhatsNewViewModel(settingsStore);
                WhatsNewVM.RequestClose += (_, _) => WhatsNewVM = null;
            }
        }
        catch
        {
            // Never block startup on the What's New dialog.
        }
    }

    private async Task OnToggleFavoriteAsync()
    {
        if (CurrentTrack == null) return;
        var newRating = CurrentRating == HeartRating.Favorite ? HeartRating.None : HeartRating.Favorite;
        await _playerCoordinator.SetRatingAsync(CurrentTrack.Id, newRating);
        await _libraryService.SetTrackRatingAsync(CurrentTrack.Id, newRating);
    }

    private async Task OnToggleDislikeAsync()
    {
        if (CurrentTrack == null) return;
        var newRating = CurrentRating == HeartRating.Dislike ? HeartRating.None : HeartRating.Dislike;
        await _playerCoordinator.SetRatingAsync(CurrentTrack.Id, newRating);
        await _libraryService.SetTrackRatingAsync(CurrentTrack.Id, newRating);
        if (newRating == HeartRating.Dislike)
        {
            await _playerCoordinator.NextAsync();
        }
    }

    private void OnPlayerTrackChanged(object? sender, TrackChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CurrentTrack));
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(CurrentPosition));
        OnPropertyChanged(nameof(ProgressPercentage));
        OnPropertyChanged(nameof(ElapsedTimeText));
        OnPropertyChanged(nameof(RemainingTimeText));
        OnPropertyChanged(nameof(FormattedDurationText));
        OnPropertyChanged(nameof(CurrentRating));
        OnPropertyChanged(nameof(IsFavorite));
        OnPropertyChanged(nameof(IsDisliked));

        if (e.CurrentTrack != null)
        {
            _ = _userStatsService?.RecordTrackPlayedAsync(e.CurrentTrack);
        }
    }

    private void OnPlayerStateChanged(object? sender, PlaybackStateChangedEventArgs e)
    {
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(PlayPauseIcon));
        OnPropertyChanged(nameof(CurrentPosition));
        OnPropertyChanged(nameof(ProgressPercentage));
        OnPropertyChanged(nameof(ElapsedTimeText));
        OnPropertyChanged(nameof(RemainingTimeText));
        OnPropertyChanged(nameof(FormattedDurationText));
        OnPropertyChanged(nameof(Shuffle));
        OnPropertyChanged(nameof(Repeat));

        if (IsPlaying)
        {
            _isNowPlayingPlaying = true;
            _equalizerTimer?.Start();
            _nowPlayingIdleTimer?.Start();
        }
        else
        {
            _isNowPlayingPlaying = false;
            _equalizerTimer?.Stop();
            _nowPlayingIdleTimer?.Stop();
            if (_nowPlayingIdleProgress > 0)
            {
                _nowPlayingIdleProgress = 0;
                OnPropertyChanged(nameof(NowPlayingIdleProgress));
                OnPropertyChanged(nameof(IsNowPlayingIdle));
            }
            RefreshNowPlayingIcon();
        }
    }

    private void OnPlayerRatingChanged(object? sender, HeartRatingChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CurrentRating));
        OnPropertyChanged(nameof(IsFavorite));
        OnPropertyChanged(nameof(IsDisliked));
    }

    private void NotifyQuickDockChanged()
    {
        OnPropertyChanged(nameof(QuickDockDeviceName));
        OnPropertyChanged(nameof(QuickDockDeviceStatus));
        OnPropertyChanged(nameof(QuickDockDeviceOpacity));
        OnPropertyChanged(nameof(QuickDockDeviceTooltip));
    }
}
