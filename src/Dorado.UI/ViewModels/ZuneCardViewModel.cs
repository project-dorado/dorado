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

    public ICommand RefreshCommand { get; }

    public ZuneCardViewModel(
        IUserStatsService statsService,
        ICloudSocialService? cloud = null,
        Func<string>? handleProvider = null)
    {
        _statsService = statsService;
        _cloud = cloud;
        _handleProvider = handleProvider;
        RefreshCommand = new AsyncRelayCommand(LoadStatsAsync);
        _ = LoadStatsAsync();
    }

    public async Task LoadStatsAsync()
    {
        Profile = await _statsService.GetProfileAsync();
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
