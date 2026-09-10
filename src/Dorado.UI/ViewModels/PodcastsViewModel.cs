using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Interfaces;
using Dorado.Domain.Models;

namespace Dorado.UI.ViewModels;

public class PodcastsViewModel : ViewModelBase
{
    private readonly IPodcastService _podcastService;
    private PodcastSeries? _selectedPodcast;
    private string _newFeedUrl = string.Empty;

    public ObservableCollection<PodcastSeries> Podcasts { get; } = new();
    public ObservableCollection<PodcastEpisode> Episodes { get; } = new();

    public PodcastSeries? SelectedPodcast
    {
        get => _selectedPodcast;
        set
        {
            if (SetProperty(ref _selectedPodcast, value))
            {
                Episodes.Clear();
                if (_selectedPodcast != null)
                {
                    foreach (var ep in _selectedPodcast.Episodes)
                    {
                        Episodes.Add(ep);
                    }
                }
            }
        }
    }

    public string NewFeedUrl
    {
        get => _newFeedUrl;
        set => SetProperty(ref _newFeedUrl, value);
    }

    public ICommand SelectPodcastCommand { get; }
    public ICommand PlayEpisodeCommand { get; }
    public ICommand SubscribeCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand MarkAllPlayedCommand { get; }
    public ICommand MarkAllUnplayedCommand { get; }

    public PodcastsViewModel(IPodcastService podcastService)
    {
        _podcastService = podcastService;

        SelectPodcastCommand = new RelayCommand<PodcastSeries>(p => SelectedPodcast = p);
        PlayEpisodeCommand = new AsyncRelayCommand<PodcastEpisode>(async ep =>
        {
            if (ep != null)
            {
                await _podcastService.PlayEpisodeAsync(ep);
            }
        });
        SubscribeCommand = new AsyncRelayCommand(async () =>
        {
            if (!string.IsNullOrWhiteSpace(NewFeedUrl))
            {
                var created = await _podcastService.SubscribeAsync(NewFeedUrl);
                Podcasts.Add(created);
                SelectedPodcast = created;
                NewFeedUrl = string.Empty;
            }
        });
        RefreshCommand = new AsyncRelayCommand(LoadPodcastsAsync);

        // PODCASTSERIESPANEL parity: "mark all as played / unplayed" on the selected series
        MarkAllPlayedCommand = new AsyncRelayCommand(() => MarkAllEpisodesAsync(true));
        MarkAllUnplayedCommand = new AsyncRelayCommand(() => MarkAllEpisodesAsync(false));

        _ = LoadPodcastsAsync();
    }

    private async Task MarkAllEpisodesAsync(bool played)
    {
        if (_selectedPodcast == null)
        {
            return;
        }

        foreach (var episode in _selectedPodcast.Episodes)
        {
            episode.IsPlayed = played;
            await _podcastService.MarkEpisodePlayedAsync(episode.Id, played);
        }
    }

    public async Task LoadPodcastsAsync()
    {
        var list = await _podcastService.GetAllPodcastsAsync();
        Podcasts.Clear();
        foreach (var p in list)
        {
            Podcasts.Add(p);
        }
        if (SelectedPodcast == null && Podcasts.Count > 0)
        {
            SelectedPodcast = Podcasts[0];
        }
    }
}
