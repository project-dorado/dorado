using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Interfaces;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;

namespace Dorado.UI.ViewModels;

public class MixviewViewModel : ViewModelBase
{
    private readonly IMixviewService _mixviewService;
    private readonly IPlayerCoordinator _playerCoordinator;
    private readonly ISmartDJService _smartDJService;
    private readonly IMediaLibraryService _libraryService;

    private readonly Stack<MixStackEntry> _mixStack = new();

    private MixNode _centerSeed = new() { Title = "Zune Mixview", Subtitle = "CONSTELLATION" };
    public MixNode CenterSeed
    {
        get => _centerSeed;
        set => SetProperty(ref _centerSeed, value);
    }

    public ObservableCollection<MixNode> Satellites { get; } = new();

    private MixNode? _hoveredNode;
    public MixNode? HoveredNode
    {
        get => _hoveredNode;
        set
        {
            if (SetProperty(ref _hoveredNode, value))
            {
                OnPropertyChanged(nameof(HasHoveredNode));
                OnPropertyChanged(nameof(HoveredTitle));
                OnPropertyChanged(nameof(HoveredSubtitle));
            }
        }
    }

    public bool HasHoveredNode => HoveredNode != null;
    public string HoveredTitle => HoveredNode?.Title ?? CenterSeed.Title;
    public string HoveredSubtitle => HoveredNode?.Subtitle ?? CenterSeed.Subtitle;

    public bool CanGoBack => _mixStack.Count > 0;
    public string HistoryBreadcrumbText => _mixStack.Count > 0
        ? $"← BACK TO {_mixStack.Peek().SeedNode.Title.ToUpperInvariant()}"
        : string.Empty;

    private string? _infoCardText;
    public string? InfoCardText
    {
        get => _infoCardText;
        set
        {
            if (SetProperty(ref _infoCardText, value))
            {
                OnPropertyChanged(nameof(HasInfoCard));
            }
        }
    }

    public bool HasInfoCard => !string.IsNullOrEmpty(InfoCardText);

    public ICommand SelectNodeCommand { get; }
    public ICommand GoBackCommand { get; }
    public ICommand PlayNodeCommand { get; }
    public ICommand SmartDJNodeCommand { get; }
    public ICommand SetHoveredNodeCommand { get; }
    public ICommand LikeNodeCommand { get; }
    public ICommand HateNodeCommand { get; }
    public ICommand InfoNodeCommand { get; }
    public ICommand AddNodeCommand { get; }
    public ICommand CloseInfoCardCommand { get; }

    public MixviewViewModel(
        IMixviewService mixviewService,
        IPlayerCoordinator playerCoordinator,
        ISmartDJService smartDJService,
        IMediaLibraryService libraryService)
    {
        _mixviewService = mixviewService;
        _playerCoordinator = playerCoordinator;
        _smartDJService = smartDJService;
        _libraryService = libraryService;

        SelectNodeCommand = new AsyncRelayCommand<MixNode>(OnSelectNodeAsync);
        GoBackCommand = new AsyncRelayCommand(OnGoBackAsync);
        PlayNodeCommand = new AsyncRelayCommand<MixNode>(OnPlayNodeAsync);
        SmartDJNodeCommand = new AsyncRelayCommand<MixNode>(OnSmartDJNodeAsync);
        SetHoveredNodeCommand = new RelayCommand<MixNode>(node => HoveredNode = node);
        LikeNodeCommand = new AsyncRelayCommand<MixNode>(node => RateNodeAsync(node, HeartRating.Favorite));
        HateNodeCommand = new AsyncRelayCommand<MixNode>(node => RateNodeAsync(node, HeartRating.Dislike));
        InfoNodeCommand = new AsyncRelayCommand<MixNode>(OnInfoNodeAsync);
        AddNodeCommand = new AsyncRelayCommand<MixNode>(OnAddNodeAsync);
        CloseInfoCardCommand = new RelayCommand(() => InfoCardText = null);
    }

    public async Task InitializeSeedAsync(string seedName, MixNodeType seedType = MixNodeType.Artist, Guid? seedId = null)
    {
        _mixStack.Clear();
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(HistoryBreadcrumbText));

        await LoadConstellationAsync(seedName, seedType, seedId);
    }

    private async Task OnSelectNodeAsync(MixNode? node)
    {
        if (node == null || node.IsCenterSeed) return;

        // Push current view onto MixStack
        _mixStack.Push(new MixStackEntry(CenterSeed, Satellites));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(HistoryBreadcrumbText));

        await LoadConstellationAsync(node.Title, node.NodeType, node.EntityId);
    }

    private Task OnGoBackAsync()
    {
        if (_mixStack.Count == 0) return Task.CompletedTask;

        var previous = _mixStack.Pop();
        CenterSeed = previous.SeedNode;
        Satellites.Clear();
        foreach (var s in previous.Satellites)
        {
            Satellites.Add(s);
        }

        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(HistoryBreadcrumbText));
        return Task.CompletedTask;
    }

    private async Task LoadConstellationAsync(string seedName, MixNodeType seedType, Guid? seedId)
    {
        var constellation = await _mixviewService.GenerateConstellationAsync(seedName, seedType, seedId);
        CenterSeed = constellation.CenterSeed;

        Satellites.Clear();
        foreach (var sat in constellation.Satellites)
        {
            Satellites.Add(sat);
        }

        HoveredNode = null;
    }

    private async Task OnPlayNodeAsync(MixNode? node)
    {
        var target = node ?? CenterSeed;
        var allTracks = await _libraryService.GetAllTracksAsync();

        if (target.NodeType == MixNodeType.Track && target.EntityId.HasValue)
        {
            var trk = allTracks.FirstOrDefault(t => t.Id == target.EntityId.Value);
            if (trk != null)
            {
                await _playerCoordinator.PlayTrackAsync(trk, allTracks);
                return;
            }
        }

        if (target.NodeType == MixNodeType.Album && target.EntityId.HasValue)
        {
            var albumTracks = allTracks.Where(t => t.AlbumId == target.EntityId.Value).ToList();
            if (albumTracks.Count > 0)
            {
                await _playerCoordinator.PlayTrackAsync(albumTracks[0], albumTracks);
                return;
            }
        }

        // Default or Artist: generate Smart DJ mix
        await OnSmartDJNodeAsync(target);
    }

    private async Task OnSmartDJNodeAsync(MixNode? node)
    {
        var target = node ?? CenterSeed;
        var allTracks = await _libraryService.GetAllTracksAsync();

        var seed = new SmartDJSeed
        {
            SeedArtistId = target.NodeType == MixNodeType.Artist ? target.EntityId : null,
            SeedAlbumId = target.NodeType == MixNodeType.Album ? target.EntityId : null,
            SeedGenre = target.Title
        };

        var mix = await _smartDJService.GenerateMixAsync(seed, allTracks);
        if (mix.Count > 0)
        {
            await _playerCoordinator.PlayTrackAsync(mix[0], mix);
        }
    }

    // ==========================================
    // HOVER ACTION TILES (MIX.LIKEIT / HATEIT / INFO / ADD parity)
    // ==========================================
    private async Task<Track?> ResolveRepresentativeTrackAsync(MixNode node)
    {
        var allTracks = await _libraryService.GetAllTracksAsync();

        if (node.NodeType == MixNodeType.Track && node.EntityId.HasValue)
        {
            return allTracks.FirstOrDefault(t => t.Id == node.EntityId.Value);
        }

        if (node.NodeType == MixNodeType.Album && node.EntityId.HasValue)
        {
            return allTracks.FirstOrDefault(t => t.AlbumId == node.EntityId.Value);
        }

        if (node.NodeType == MixNodeType.Artist)
        {
            return allTracks.FirstOrDefault(t => t.ArtistName.Equals(node.Title, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private async Task RateNodeAsync(MixNode? node, HeartRating rating)
    {
        var target = node ?? HoveredNode ?? CenterSeed;
        var track = await ResolveRepresentativeTrackAsync(target);
        if (track == null)
        {
            return;
        }

        await _playerCoordinator.SetRatingAsync(track.Id, rating);
        await _libraryService.SetTrackRatingAsync(track.Id, rating);
    }

    private async Task OnInfoNodeAsync(MixNode? node)
    {
        var target = node ?? HoveredNode ?? CenterSeed;
        var allTracks = await _libraryService.GetAllTracksAsync();

        if (target.NodeType == MixNodeType.Artist)
        {
            var artists = await _libraryService.GetAllArtistsAsync();
            var artist = artists.FirstOrDefault(a => a.Name.Equals(target.Title, StringComparison.OrdinalIgnoreCase));
            InfoCardText = artist?.Biography
                ?? $"{target.Title} is cataloged in your Dorado collection. Enable online metadata services in Settings to display the full artist biography.";
            return;
        }

        if (target.NodeType == MixNodeType.Album && target.EntityId.HasValue)
        {
            var albums = await _libraryService.GetAllAlbumsAsync();
            var album = albums.FirstOrDefault(a => a.Id == target.EntityId.Value);
            if (album != null)
            {
                var albumTracks = allTracks.Where(t => t.AlbumId == album.Id).ToList();
                var totalDuration = TimeSpan.FromSeconds(albumTracks.Sum(t => t.Duration.TotalSeconds));
                InfoCardText = $"{album.Title} ({album.Year}) — {album.ArtistName}. {albumTracks.Count} tracks, {totalDuration.Minutes} minutes. Genre: {(string.IsNullOrWhiteSpace(album.Genre) ? "unclassified" : album.Genre)}.";
                return;
            }
        }

        if (target.NodeType == MixNodeType.Track && target.EntityId.HasValue)
        {
            var track = allTracks.FirstOrDefault(t => t.Id == target.EntityId.Value);
            if (track != null)
            {
                InfoCardText = $"{track.Title} — {track.ArtistName}, from {track.AlbumTitle} ({track.Year}). {track.Duration:m\\:ss}. Play count: {track.PlayCount}.";
                return;
            }
        }

        InfoCardText = $"{target.Title} — {target.Subtitle}.";
    }

    private async Task OnAddNodeAsync(MixNode? node)
    {
        var target = node ?? HoveredNode ?? CenterSeed;
        var allTracks = await _libraryService.GetAllTracksAsync();

        if (target.NodeType == MixNodeType.Track && target.EntityId.HasValue)
        {
            var track = allTracks.FirstOrDefault(t => t.Id == target.EntityId.Value);
            if (track != null)
            {
                _playerCoordinator.Enqueue(new[] { track });
                return;
            }
        }

        if (target.NodeType == MixNodeType.Album && target.EntityId.HasValue)
        {
            var albumTracks = allTracks.Where(t => t.AlbumId == target.EntityId.Value).ToList();
            if (albumTracks.Count > 0)
            {
                _playerCoordinator.Enqueue(albumTracks);
                return;
            }
        }

        // Artist (or fallback): queue a Smart DJ mix built from this node.
        var seed = new SmartDJSeed
        {
            SeedArtistId = target.NodeType == MixNodeType.Artist ? target.EntityId : null,
            SeedAlbumId = target.NodeType == MixNodeType.Album ? target.EntityId : null,
            SeedGenre = target.Title
        };
        var mix = await _smartDJService.GenerateMixAsync(seed, allTracks);
        if (mix.Count > 0)
        {
            _playerCoordinator.Enqueue(mix);
        }
    }
}
