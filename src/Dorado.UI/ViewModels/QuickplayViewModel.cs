using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;

namespace Dorado.UI.ViewModels;

public enum QuickplayDeck
{
    Pins,
    History,
    New
}

public class QuickplayViewModel : ViewModelBase
{
    private readonly IPlayerCoordinator _playerCoordinator;
    private readonly IMediaLibraryService _libraryService;
    private readonly ISmartDJService _smartDJService;
    private readonly IDynamicMixService? _dynamicMixService;

    private QuickplayDeck _activeDeck = QuickplayDeck.Pins;
    private string _smartDjSeedText = "Seed: Entire Collection";

    public ObservableCollection<Album> Pins { get; } = new();
    public ObservableCollection<PlayHistoryEntry> History { get; } = new();
    public ObservableCollection<Album> NewAlbums { get; } = new();
    public ObservableCollection<DynamicMix> DynamicMixes { get; } = new();
    public bool HasDynamicMixes => DynamicMixes.Count > 0;

    public QuickplayDeck ActiveDeck
    {
        get => _activeDeck;
        set
        {
            if (SetProperty(ref _activeDeck, value))
            {
                OnPropertyChanged(nameof(IsPinsActive));
                OnPropertyChanged(nameof(IsHistoryActive));
                OnPropertyChanged(nameof(IsNewActive));
            }
        }
    }

    public bool IsPinsActive => _activeDeck == QuickplayDeck.Pins;
    public bool IsHistoryActive => _activeDeck == QuickplayDeck.History;
    public bool IsNewActive => _activeDeck == QuickplayDeck.New;

    public string SmartDjSeedText
    {
        get => _smartDjSeedText;
        set => SetProperty(ref _smartDjSeedText, value);
    }

    /// <summary>Quick Mix generation wall-clock limit (overridable for tests).</summary>
    public static TimeSpan QuickMixTimeout { get; set; } = TimeSpan.FromSeconds(5);

    private CancellationTokenSource? _quickMixCts;
    private string? _quickMixStatusText;
    public string? QuickMixStatusText
    {
        get => _quickMixStatusText;
        private set
        {
            if (SetProperty(ref _quickMixStatusText, value))
            {
                OnPropertyChanged(nameof(HasQuickMixStatus));
            }
        }
    }

    public bool HasQuickMixStatus => !string.IsNullOrEmpty(QuickMixStatusText);

    private double _quickMixProgress;
    public double QuickMixProgress
    {
        get => _quickMixProgress;
        private set => SetProperty(ref _quickMixProgress, value);
    }

    private bool _isQuickMixBusy;
    public bool IsQuickMixBusy
    {
        get => _isQuickMixBusy;
        private set => SetProperty(ref _isQuickMixBusy, value);
    }

    public ICommand SelectDeckCommand { get; }
    public ICommand LaunchSmartDjCommand { get; }
    public ICommand PlayFavoritesMixCommand { get; }
    public ICommand PlayDiscoveryMixCommand { get; }
    public ICommand PlayHistoryItemCommand { get; }
    public ICommand PlayAlbumCommand { get; }
    public ICommand UnpinAlbumCommand { get; }
    public ICommand PlayDynamicMixCommand { get; }

    public QuickplayViewModel(
        IPlayerCoordinator playerCoordinator,
        IMediaLibraryService libraryService,
        ISmartDJService smartDJService,
        IDynamicMixService? dynamicMixService = null)
    {
        _playerCoordinator = playerCoordinator;
        _libraryService = libraryService;
        _smartDJService = smartDJService;
        _dynamicMixService = dynamicMixService;

        SelectDeckCommand = new RelayCommand<QuickplayDeck>(deck => ActiveDeck = deck);
        LaunchSmartDjCommand = new AsyncRelayCommand(OnLaunchSmartDjAsync);
        PlayFavoritesMixCommand = new AsyncRelayCommand(OnPlayFavoritesMixAsync);
        PlayDiscoveryMixCommand = new AsyncRelayCommand(OnPlayDiscoveryMixAsync);
        PlayDynamicMixCommand = new AsyncRelayCommand<DynamicMix>(OnPlayDynamicMixAsync);
        PlayHistoryItemCommand = new AsyncRelayCommand<PlayHistoryEntry>(OnPlayHistoryItemAsync);
        PlayAlbumCommand = new AsyncRelayCommand<Album>(OnPlayAlbumAsync);
        UnpinAlbumCommand = new AsyncRelayCommand<Album>(async album =>
        {
            if (album != null)
            {
                Pins.Remove(album);
                await _libraryService.UnpinAlbumAsync(album.Id);
            }
        });

        _libraryService.LibraryUpdated += async (_, _) =>
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(LoadInitialDataAsync);
        };

        _ = LoadInitialDataAsync();
    }

    public async Task LoadInitialDataAsync()
    {
        var pinnedAlbums = await _libraryService.GetPinnedAlbumsAsync();
        Pins.Clear();
        foreach (var album in pinnedAlbums)
        {
            Pins.Add(album);
        }

        var historyItems = await _libraryService.GetRecentHistoryAsync(10);
        History.Clear();
        foreach (var item in historyItems)
        {
            History.Add(item);
        }

        var newItems = await _libraryService.GetRecentlyAddedAlbumsAsync(8);
        NewAlbums.Clear();
        foreach (var album in newItems)
        {
            NewAlbums.Add(album);
        }

        if (_dynamicMixService is not null)
        {
            DynamicMixes.Clear();
            foreach (var mix in _dynamicMixService.BuildDefaultMixes())
            {
                DynamicMixes.Add(mix);
            }

            OnPropertyChanged(nameof(HasDynamicMixes));
        }
    }

    private async Task OnPlayDynamicMixAsync(DynamicMix? mix)
    {
        if (mix is null || _dynamicMixService is null)
        {
            return;
        }

        var allTracks = await _libraryService.GetAllTracksAsync();
        var tracks = await _dynamicMixService.MaterializeAsync(mix, allTracks);
        if (tracks.Count > 0)
        {
            await _playerCoordinator.PlayTrackAsync(tracks[0], tracks);
        }
    }

    /// <summary>
    /// Runs Smart DJ generation with progress reporting and a hard wall-clock
    /// timeout (Quick Mix notification parity). Returns an empty list on timeout.
    /// </summary>
    private async Task<IReadOnlyList<Track>> GenerateMixWithProgressAsync(SmartDJSeed seed, IReadOnlyList<Track> pool)
    {
        _quickMixCts?.Cancel();
        _quickMixCts?.Dispose();
        _quickMixCts = new CancellationTokenSource();

        IsQuickMixBusy = true;
        QuickMixProgress = 0;
        QuickMixStatusText = "Preparing Quick Mix...";

        try
        {
            var progress = new InlineProgress(p =>
            {
                QuickMixStatusText = p.Stage;
                QuickMixProgress = p.Fraction;
            });

            var generation = _smartDJService.GenerateMixAsync(seed, pool, progress, _quickMixCts.Token);
            var timeout = Task.Delay(QuickMixTimeout);

            if (await Task.WhenAny(generation, timeout) != generation)
            {
                _quickMixCts.Cancel();
                QuickMixStatusText = "Quick Mix timed out.";
                QuickMixProgress = 0;
                return Array.Empty<Track>();
            }

            var mix = await generation;
            QuickMixProgress = 1;
            QuickMixStatusText = mix.Count > 0 ? $"Quick Mix ready — {mix.Count} tracks" : "No tracks matched this mix.";
            return mix;
        }
        catch (OperationCanceledException)
        {
            QuickMixStatusText = "Quick Mix timed out.";
            QuickMixProgress = 0;
            return Array.Empty<Track>();
        }
        finally
        {
            IsQuickMixBusy = false;
        }
    }

    private sealed class InlineProgress : IProgress<QuickMixProgress>
    {
        private readonly Action<QuickMixProgress> _onReport;
        public InlineProgress(Action<QuickMixProgress> onReport) => _onReport = onReport;
        public void Report(QuickMixProgress value) => _onReport(value);
    }

    private async Task OnLaunchSmartDjAsync()
    {
        var allTracks = await _libraryService.GetAllTracksAsync();
        if (allTracks.Count == 0) return;

        var seed = new SmartDJSeed
        {
            TargetTrackCount = 25,
            ExcludeDisliked = true
        };

        var mix = await GenerateMixWithProgressAsync(seed, allTracks);
        if (mix.Count > 0)
        {
            await _playerCoordinator.PlayTrackAsync(mix[0], mix);
        }
    }

    private async Task OnPlayFavoritesMixAsync()
    {
        var allTracks = await _libraryService.GetAllTracksAsync();
        var favTracks = allTracks.Where(t => t.Rating == HeartRating.Favorite).ToList();
        var pool = favTracks.Count > 0 ? favTracks : allTracks.ToList();

        var seed = new SmartDJSeed
        {
            TargetTrackCount = 20,
            ExcludeDisliked = true
        };

        var mix = await GenerateMixWithProgressAsync(seed, pool);
        if (mix.Count > 0)
        {
            await _playerCoordinator.PlayTrackAsync(mix[0], mix);
        }
    }

    private async Task OnPlayDiscoveryMixAsync()
    {
        var allTracks = await _libraryService.GetAllTracksAsync();
        var unplayed = allTracks.Where(t => t.PlayCount == 0 && t.Rating != HeartRating.Dislike).ToList();
        var pool = unplayed.Count > 0 ? unplayed : allTracks.ToList();

        var seed = new SmartDJSeed
        {
            TargetTrackCount = 20,
            ExcludeDisliked = true
        };

        var mix = await GenerateMixWithProgressAsync(seed, pool);
        if (mix.Count > 0)
        {
            await _playerCoordinator.PlayTrackAsync(mix[0], mix);
        }
    }

    private async Task OnPlayAlbumAsync(Album? album)
    {
        if (album == null || album.Tracks.Count == 0) return;
        await _playerCoordinator.PlayTrackAsync(album.Tracks[0], album.Tracks);
    }

    private async Task OnPlayHistoryItemAsync(PlayHistoryEntry? entry)
    {
        if (entry == null) return;
        var allTracks = await _libraryService.GetAllTracksAsync();
        var target = allTracks.FirstOrDefault(t => t.Id == entry.TrackId);
        if (target != null)
        {
            await _playerCoordinator.PlayTrackAsync(target);
        }
    }
}
