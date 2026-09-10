using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Interfaces;
using Dorado.Domain.Models;

namespace Dorado.UI.ViewModels;

public class ZuneCardViewModel : ViewModelBase
{
    private readonly IUserStatsService _statsService;

    private ZuneProfile _profile = new();
    public ObservableCollection<TopArtistStat> TopArtists { get; } = new();
    public ObservableCollection<ZuneBadge> Badges { get; } = new();

    public ZuneProfile Profile
    {
        get => _profile;
        private set => SetProperty(ref _profile, value);
    }

    public string ZuneTag => Profile.ZuneTag;
    public string StatusMessage => Profile.StatusMessage;
    public string MemberSinceText => $"ZUNE MEMBER SINCE {Profile.MemberSinceUtc:MMMM yyyy}".ToUpperInvariant();
    public int TotalTracksPlayed => Profile.TotalTracksPlayed;
    public string TotalHoursText => $"{Profile.TotalListeningTime.TotalHours:0.1} HRS";

    public ICommand RefreshCommand { get; }

    public ZuneCardViewModel(IUserStatsService statsService)
    {
        _statsService = statsService;
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
    }
}
