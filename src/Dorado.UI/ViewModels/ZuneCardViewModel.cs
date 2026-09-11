using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Interfaces;
using Dorado.Domain.Models;

namespace Dorado.UI.ViewModels;

public class ZuneCardViewModel : ViewModelBase
{
    private readonly IUserStatsService _statsService;
    private readonly ICloudSocialService? _cloud;
    private readonly Func<string>? _handleProvider;
    private readonly ISettingsStore? _settingsStore;
    private readonly IFolderPickerService? _folderPicker;

    private ZuneProfile _profile = new();
    private ZuneCardSnapshot? _liveCard;
    public ObservableCollection<TopArtistStat> TopArtists { get; } = new();
    public ObservableCollection<ZuneBadge> Badges { get; } = new();
    public ObservableCollection<ZuneCardBadge> LiveBadges { get; } = new();
    public ObservableCollection<ZuneCardActivity> LiveRecent { get; } = new();

    public ZuneProfile Profile
    {
        get => _profile;
        private set => SetProperty(ref _profile, value);
    }

    /// <summary>The live cloud Zune Card, when the cloud is enabled and a handle is set.</summary>
    public ZuneCardSnapshot? LiveCard
    {
        get => _liveCard;
        private set
        {
            if (SetProperty(ref _liveCard, value))
            {
                OnPropertyChanged(nameof(HasLiveCard));
                OnPropertyChanged(nameof(LiveHandle));
                OnPropertyChanged(nameof(LiveFollowersText));
                OnPropertyChanged(nameof(LiveFollowingText));
                OnPropertyChanged(nameof(LiveActivitiesText));
            }
        }
    }

    public bool HasLiveCard => LiveCard is not null;
    public string LiveHandle => LiveCard?.Handle ?? string.Empty;
    public string LiveFollowersText => LiveCard is null ? string.Empty : $"{LiveCard.Followers} FOLLOWERS";
    public string LiveFollowingText => LiveCard is null ? string.Empty : $"{LiveCard.Following} FOLLOWING";
    public string LiveActivitiesText => LiveCard is null ? string.Empty : $"{LiveCard.Activities} ACTIVITIES";

    public string ZuneTag => Profile.ZuneTag;
    public string StatusMessage => Profile.StatusMessage;
    public string MemberSinceText => $"ZUNE MEMBER SINCE {Profile.MemberSinceUtc:MMMM yyyy}".ToUpperInvariant();
    public int TotalTracksPlayed => Profile.TotalTracksPlayed;
    public string TotalHoursText => $"{Profile.TotalListeningTime.TotalHours:0.1} HRS";

    // ---- Editable profile (audit M-6 / Top-15 #12) -------------------------

    private bool _isEditingProfile;
    public bool IsEditingProfile
    {
        get => _isEditingProfile;
        private set => SetProperty(ref _isEditingProfile, value);
    }

    private string _editZuneTag = string.Empty;
    public string EditZuneTag
    {
        get => _editZuneTag;
        set => SetProperty(ref _editZuneTag, value);
    }

    private string _editStatusMessage = string.Empty;
    public string EditStatusMessage
    {
        get => _editStatusMessage;
        set => SetProperty(ref _editStatusMessage, value);
    }

    private string _editAvatarUri = string.Empty;
    public string EditAvatarUri
    {
        get => _editAvatarUri;
        set
        {
            if (SetProperty(ref _editAvatarUri, value))
            {
                OnPropertyChanged(nameof(AvatarUri));
                OnPropertyChanged(nameof(HasAvatar));
            }
        }
    }

    /// <summary>Display avatar (edit buffer wins while editing; falls back to the profile value).</summary>
    public string AvatarUri => string.IsNullOrWhiteSpace(_editAvatarUri) ? Profile.AvatarUri : _editAvatarUri;
    public bool HasAvatar => !string.IsNullOrWhiteSpace(AvatarUri);

    public ICommand RefreshCommand { get; }
    public ICommand BeginEditProfileCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand CancelEditProfileCommand { get; }
    public ICommand PickAvatarCommand { get; }

    public ZuneCardViewModel(
        IUserStatsService statsService,
        ICloudSocialService? cloud = null,
        Func<string>? handleProvider = null,
        ISettingsStore? settingsStore = null,
        IFolderPickerService? folderPicker = null)
    {
        _statsService = statsService;
        _cloud = cloud;
        _handleProvider = handleProvider;
        _settingsStore = settingsStore;
        _folderPicker = folderPicker;
        RefreshCommand = new AsyncRelayCommand(LoadStatsAsync);
        BeginEditProfileCommand = new RelayCommand(BeginEditProfile);
        SaveProfileCommand = new RelayCommand(SaveProfile);
        CancelEditProfileCommand = new RelayCommand(() => IsEditingProfile = false);
        PickAvatarCommand = new AsyncRelayCommand(PickAvatarAsync);
        _ = LoadStatsAsync();
    }

    private void BeginEditProfile()
    {
        EditZuneTag = Profile.ZuneTag;
        EditStatusMessage = Profile.StatusMessage;
        EditAvatarUri = Profile.AvatarUri;
        IsEditingProfile = true;
    }

    private void SaveProfile()
    {
        Profile.ZuneTag = string.IsNullOrWhiteSpace(EditZuneTag) ? "ZuneUser" : EditZuneTag.Trim();
        Profile.StatusMessage = EditStatusMessage?.Trim() ?? string.Empty;
        Profile.AvatarUri = EditAvatarUri?.Trim() ?? string.Empty;

        if (_settingsStore is not null)
        {
            var settings = _settingsStore.Load();
            settings.ZuneTag = Profile.ZuneTag;
            settings.ZuneStatusMessage = Profile.StatusMessage;
            settings.ZuneAvatarUri = Profile.AvatarUri;
            _settingsStore.Save(settings);
        }

        OnPropertyChanged(nameof(ZuneTag));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(AvatarUri));
        OnPropertyChanged(nameof(HasAvatar));
        IsEditingProfile = false;
    }

    private async Task PickAvatarAsync()
    {
        if (_folderPicker is null) return;
        var path = await _folderPicker.PickFileAsync("Select Avatar Image", "*.png;*.jpg;*.jpeg");
        if (!string.IsNullOrWhiteSpace(path))
        {
            EditAvatarUri = path;
        }
    }

    public async Task LoadStatsAsync()
    {
        Profile = await _statsService.GetProfileAsync();

        // User-authored profile values (persisted) win over the computed defaults.
        var persisted = _settingsStore?.Load();
        if (persisted is not null)
        {
            if (!string.IsNullOrWhiteSpace(persisted.ZuneTag)) Profile.ZuneTag = persisted.ZuneTag;
            if (!string.IsNullOrWhiteSpace(persisted.ZuneStatusMessage)) Profile.StatusMessage = persisted.ZuneStatusMessage;
            if (!string.IsNullOrWhiteSpace(persisted.ZuneAvatarUri)) Profile.AvatarUri = persisted.ZuneAvatarUri;
        }
        _editAvatarUri = Profile.AvatarUri;
        OnPropertyChanged(nameof(AvatarUri));
        OnPropertyChanged(nameof(HasAvatar));
        OnPropertyChanged(nameof(ZuneTag));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(MemberSinceText));
        OnPropertyChanged(nameof(TotalTracksPlayed));
        OnPropertyChanged(nameof(TotalHoursText));

        var artists = await _statsService.GetTopArtistsAsync(5);
        TopArtists.Clear();
        foreach (var a in artists)
        {
            TopArtists.Add(a);
        }

        var badges = await _statsService.GetBadgesAsync();
        Badges.Clear();
        foreach (var b in badges)
        {
            Badges.Add(b);
        }

        await LoadLiveCardAsync();
    }

    /// <summary>
    /// Fetches the live cross-device Zune Card from the cloud. Failures degrade
    /// silently — the local projection above remains authoritative offline.
    /// </summary>
    public async Task LoadLiveCardAsync()
    {
        if (_cloud is null || !_cloud.IsEnabled)
        {
            LiveCard = null;
            return;
        }

        var handle = _handleProvider?.Invoke()?.Trim() ?? string.Empty;
        if (handle.Length == 0)
        {
            LiveCard = null;
            return;
        }

        var card = await _cloud.GetZuneCardAsync(handle);
        LiveCard = card;

        LiveBadges.Clear();
        LiveRecent.Clear();
        if (card is null)
        {
            return;
        }

        foreach (var badge in card.Badges)
        {
            LiveBadges.Add(badge);
        }

        foreach (var activity in card.Recent)
        {
            LiveRecent.Add(activity);
        }
    }
}
