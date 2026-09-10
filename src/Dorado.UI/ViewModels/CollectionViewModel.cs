using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Interfaces;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;

namespace Dorado.UI.ViewModels;

public enum CollectionSubPivot
{
    Artists,
    Albums,
    Songs,
    Genres,
    Podcasts,
    Playlists,
    Videos,
    Pictures
}

public class CollectionViewModel : ViewModelBase
{
    private readonly IPlayerCoordinator _playerCoordinator;
    private readonly IMediaLibraryService _libraryService;
    private readonly IDialogService? _dialogService;
    private readonly IReviewService? _reviewService;
    private readonly ISmartDJService _smartDJService;
    private readonly IArtworkCacheService? _artworkCache;
    private readonly IExternalMetadataService? _metadataService;
    private readonly HashSet<Guid> _artworkLookupsInFlight = new();

    private CollectionSubPivot _activeSubPivot = CollectionSubPivot.Artists;
    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                FilterQuery(value);
            }
        }
    }
    private Artist? _selectedArtist;
    private string? _selectedGenre;

    private List<Artist> _allArtists = new();
    private List<Album> _allAlbums = new();
    private List<Track> _allSongs = new();

    public PodcastsViewModel PodcastsVM { get; }
    public PlaylistsViewModel PlaylistsVM { get; }
    public VideoLibraryViewModel VideoVM { get; }
    public PhotoLibraryViewModel PhotoVM { get; }

    public ObservableCollection<Artist> Artists { get; } = new();
    public ObservableCollection<Album> Albums { get; } = new();
    public ObservableCollection<Track> Songs { get; } = new();
    public ObservableCollection<Album> SelectedArtistAlbums { get; } = new();
    public ObservableCollection<string> Genres { get; } = new();
    public ObservableCollection<Track> SelectedGenreSongs { get; } = new();

    public CollectionSubPivot ActiveSubPivot
    {
        get => _activeSubPivot;
        set
        {
            if (SetProperty(ref _activeSubPivot, value))
            {
                OnPropertyChanged(nameof(IsArtistsActive));
                OnPropertyChanged(nameof(IsAlbumsActive));
                OnPropertyChanged(nameof(IsSongsActive));
                OnPropertyChanged(nameof(IsGenresActive));
                OnPropertyChanged(nameof(IsPodcastsActive));
                OnPropertyChanged(nameof(IsPlaylistsActive));
                OnPropertyChanged(nameof(IsVideosActive));
                OnPropertyChanged(nameof(IsPicturesActive));
            }
        }
    }

    public bool IsArtistsActive => _activeSubPivot == CollectionSubPivot.Artists;
    public bool IsAlbumsActive => _activeSubPivot == CollectionSubPivot.Albums;
    public bool IsSongsActive => _activeSubPivot == CollectionSubPivot.Songs;
    public bool IsGenresActive => _activeSubPivot == CollectionSubPivot.Genres;
    public bool IsPodcastsActive => _activeSubPivot == CollectionSubPivot.Podcasts;
    public bool IsPlaylistsActive => _activeSubPivot == CollectionSubPivot.Playlists;
    public bool IsVideosActive => _activeSubPivot == CollectionSubPivot.Videos;
    public bool IsPicturesActive => _activeSubPivot == CollectionSubPivot.Pictures;

    public Artist? SelectedArtist
    {
        get => _selectedArtist;
        set
        {
            if (SetProperty(ref _selectedArtist, value))
            {
                UpdateSelectedArtistAlbums();
            }
        }
    }

    public string? SelectedGenre
    {
        get => _selectedGenre;
        set
        {
            if (SetProperty(ref _selectedGenre, value))
            {
                UpdateSelectedGenreSongs();
            }
        }
    }

    public ICommand SelectSubPivotCommand { get; }
    public ICommand SelectArtistCommand { get; }
    public ICommand SelectGenreCommand { get; }
    public ICommand PlaySongCommand { get; }
    public ICommand PlayAlbumCommand { get; }
    public ICommand PlayArtistCommand { get; }
    public ICommand PlayGenreCommand { get; }
    public ICommand PlayNextCommand { get; }
    public ICommand EnqueueTrackCommand { get; }
    public ICommand ToggleFavoriteCommand { get; }
    public ICommand ToggleDislikeCommand { get; }
    public ICommand OpenEditMetadataCommand { get; }
    public ICommand CloseEditMetadataCommand { get; }
    public ICommand PinAlbumCommand { get; }
    public ICommand UnpinAlbumCommand { get; }
    public ICommand WriteReviewCommand { get; }
    public ICommand StartSmartDjFromTrackCommand { get; }
    public ICommand StartSmartDjFromAlbumCommand { get; }
    public ICommand StartSmartDjFromArtistCommand { get; }

    private MetadataEditViewModel? _activeEditMetadataVM;
    public MetadataEditViewModel? ActiveEditMetadataVM
    {
        get => _activeEditMetadataVM;
        set
        {
            if (SetProperty(ref _activeEditMetadataVM, value))
            {
                OnPropertyChanged(nameof(IsEditMetadataOpen));
            }
        }
    }

    public bool IsEditMetadataOpen => ActiveEditMetadataVM != null;

    private PhotoSlideshowViewModel? _activeSlideshowVM;
    public PhotoSlideshowViewModel? ActiveSlideshowVM
    {
        get => _activeSlideshowVM;
        set
        {
            if (SetProperty(ref _activeSlideshowVM, value))
            {
                OnPropertyChanged(nameof(IsSlideshowOpen));
            }
        }
    }

    public bool IsSlideshowOpen => ActiveSlideshowVM != null;

    private string? _findAlbumInfoStatusText;
    public string? FindAlbumInfoStatusText
    {
        get => _findAlbumInfoStatusText;
        set
        {
            if (SetProperty(ref _findAlbumInfoStatusText, value))
            {
                OnPropertyChanged(nameof(HasFindAlbumInfoStatus));
            }
        }
    }

    public bool HasFindAlbumInfoStatus => !string.IsNullOrEmpty(FindAlbumInfoStatusText);

    public ICommand FindAlbumInfoCommand { get; }

    public CollectionViewModel(
        IPlayerCoordinator playerCoordinator,
        IMediaLibraryService libraryService,
        IPodcastService? podcastService = null,
        ISmartDJService? smartDJService = null,
        IArtworkCacheService? artworkCache = null,
        IExternalMetadataService? metadataService = null,
        ISmartPlaylistService? smartPlaylistService = null,
        IVideoLibraryService? videoLibraryService = null,
        IVideoPlaybackEngine? videoEngine = null,
        IPhotoLibraryService? photoLibraryService = null,
        IDialogService? dialogService = null,
        IReviewService? reviewService = null)
    {
        _playerCoordinator = playerCoordinator;
        _libraryService = libraryService;
        _dialogService = dialogService;
        _reviewService = reviewService;
        _smartDJService = smartDJService ?? new Dorado.Application.Services.SmartDJEngine();
        _artworkCache = artworkCache;
        _metadataService = metadataService;
        var podService = podcastService ?? new Dorado.Application.Services.PodcastService(playerCoordinator);
        PodcastsVM = new PodcastsViewModel(podService);
        VideoVM = new VideoLibraryViewModel(
            videoLibraryService ?? new Dorado.Application.Services.EmptyVideoLibraryService(),
            videoEngine ?? new Dorado.Application.Services.UnavailableVideoPlaybackEngine());
        PhotoVM = new PhotoLibraryViewModel(
            photoLibraryService ?? new Dorado.Application.Services.EmptyPhotoLibraryService());
        PhotoVM.SlideshowRequested += (_, photo) =>
        {
            var startIndex = photo != null ? PhotoVM.GalleryPhotos.IndexOf(photo) : 0;
            var slideshow = new PhotoSlideshowViewModel(PhotoVM.GalleryPhotos, Math.Max(0, startIndex));
            slideshow.RequestClose += (_, _) => ActiveSlideshowVM = null;
            ActiveSlideshowVM = slideshow;
        };
        PlaylistsVM = new PlaylistsViewModel(libraryService, playerCoordinator, smartPlaylistService, dialogService);

        OpenEditMetadataCommand = new RelayCommand<Track>(track =>
        {
            if (track != null)
            {
                var vm = new MetadataEditViewModel(track, _libraryService);
                vm.RequestClose += (_, _) => ActiveEditMetadataVM = null;
                ActiveEditMetadataVM = vm;
            }
        });
        CloseEditMetadataCommand = new RelayCommand(() => ActiveEditMetadataVM = null);

        SelectSubPivotCommand = new RelayCommand<CollectionSubPivot>(pivot => ActiveSubPivot = pivot);
        SelectArtistCommand = new RelayCommand<Artist>(artist => SelectedArtist = artist);
        SelectGenreCommand = new RelayCommand<string>(genre => SelectedGenre = genre);
        PlaySongCommand = new AsyncRelayCommand<Track>(OnPlaySongAsync);
        PlayAlbumCommand = new AsyncRelayCommand<Album>(OnPlayAlbumAsync);
        PlayArtistCommand = new AsyncRelayCommand<Artist>(OnPlayArtistAsync);
        PlayGenreCommand = new AsyncRelayCommand<string>(OnPlayGenreAsync);
        PlayNextCommand = new RelayCommand<Track>(track =>
        {
            if (track != null) _playerCoordinator.PlayNext(new[] { track });
        });
        EnqueueTrackCommand = new RelayCommand<Track>(track =>
        {
            if (track != null) _playerCoordinator.Enqueue(new[] { track });
        });
        ToggleFavoriteCommand = new AsyncRelayCommand<Track>(OnToggleFavoriteAsync);
        ToggleDislikeCommand = new AsyncRelayCommand<Track>(OnToggleDislikeAsync);

        PinAlbumCommand = new AsyncRelayCommand<Album>(async album =>
        {
            if (album != null) await _libraryService.PinAlbumAsync(album.Id);
        });
        UnpinAlbumCommand = new AsyncRelayCommand<Album>(async album =>
        {
            if (album != null) await _libraryService.UnpinAlbumAsync(album.Id);
        });
        WriteReviewCommand = new AsyncRelayCommand<Album>(OnWriteReviewAsync);

        StartSmartDjFromTrackCommand = new AsyncRelayCommand<Track>(OnStartSmartDjFromTrackAsync);
        StartSmartDjFromAlbumCommand = new AsyncRelayCommand<Album>(OnStartSmartDjFromAlbumAsync);
        StartSmartDjFromArtistCommand = new AsyncRelayCommand<Artist>(OnStartSmartDjFromArtistAsync);

        FindAlbumInfoCommand = new AsyncRelayCommand<Album>(OnFindAlbumInfoAsync);

        _ = RefreshDataAsync();
    }

    private async Task OnWriteReviewAsync(Album? album)
    {
        if (album is null || _dialogService is null || _reviewService is null)
        {
            return;
        }

        var body = await _dialogService.PromptAsync(new DialogRequest(
            "Write a review",
            $"Your review of \u201c{album.Title}\u201d by {album.ArtistName}",
            "SAVE",
            "CANCEL"));
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        await _reviewService.AddReviewAsync(new Review
        {
            AlbumId = album.Id,
            AlbumTitle = album.Title,
            ArtistName = album.ArtistName,
            Body = body.Trim()
        });
    }

    private TrackMatchReviewViewModel? _activeTrackMatchReviewVM;
    public TrackMatchReviewViewModel? ActiveTrackMatchReviewVM
    {
        get => _activeTrackMatchReviewVM;
        set
        {
            if (SetProperty(ref _activeTrackMatchReviewVM, value))
            {
                OnPropertyChanged(nameof(IsTrackMatchReviewOpen));
            }
        }
    }

    public bool IsTrackMatchReviewOpen => ActiveTrackMatchReviewVM != null;

    private async System.Threading.Tasks.Task OnFindAlbumInfoAsync(Album? album)
    {
        if (album == null || _metadataService == null || _artworkCache == null)
        {
            FindAlbumInfoStatusText = "Online metadata services are unavailable.";
            return;
        }

        lock (_artworkLookupsInFlight)
        {
            if (!_artworkLookupsInFlight.Add(album.Id))
            {
                return;
            }
        }

        FindAlbumInfoStatusText = $"Searching MusicBrainz for \"{album.Title}\"...";
        try
        {
            var match = await _metadataService.FindAlbumArtworkAsync(album.ArtistName, album.Title, album.Year);
            if (match?.ArtworkUrl == null)
            {
                FindAlbumInfoStatusText = $"No matching release found for \"{album.Title}\".";
            }
            else
            {
                var localPath = await _artworkCache.GetOrDownloadAsync(match.ArtworkUrl);
                if (string.IsNullOrEmpty(localPath))
                {
                    FindAlbumInfoStatusText = $"Cover art not yet available on the Cover Art Archive for \"{album.Title}\".";
                }
                else
                {
                    await _libraryService.SetAlbumArtworkAsync(album.Id, localPath);

                    var updated = new Album
                    {
                        Id = album.Id,
                        Title = album.Title,
                        ArtistId = album.ArtistId,
                        ArtistName = album.ArtistName,
                        Year = album.Year,
                        Genre = album.Genre,
                        ArtworkUri = localPath,
                        TrackCount = album.TrackCount,
                        IsPinned = album.IsPinned,
                        PinnedAtUtc = album.PinnedAtUtc,
                        Tracks = album.Tracks
                    };
                    ReplaceAlbumEverywhere(album, updated);
                    FindAlbumInfoStatusText = $"Cover art applied to \"{updated.Title}\".";
                }
            }

            // Track matching review (Zune's per-song "Find Album Info" flow)
            if (album.Tracks.Count > 0)
            {
                FindAlbumInfoStatusText = $"Matching {album.Tracks.Count} tracks on MusicBrainz...";
                var candidates = await _metadataService.FindTrackMatchesAsync(album.ArtistName, album.Title, album.Tracks);
                if (candidates.Count > 0)
                {
                    var review = new TrackMatchReviewViewModel(album.Title, album.ArtistName, candidates, album.Tracks, _libraryService);
                    review.RequestClose += async (_, _) =>
                    {
                        ActiveTrackMatchReviewVM = null;
                        var changed = review.Rows.Count(r => r.Accepted && !r.Candidate.MatchedTitle.Equals(r.OriginalTrack.Title, StringComparison.OrdinalIgnoreCase));
                        if (changed > 0)
                        {
                            await RefreshDataAsync();
                            FindAlbumInfoStatusText = $"Updated {changed} track title{(changed == 1 ? string.Empty : "s")} from MusicBrainz.";
                        }
                    };
                    ActiveTrackMatchReviewVM = review;
                }
                else
                {
                    FindAlbumInfoStatusText += " All track titles already match.";
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            FindAlbumInfoStatusText = $"Album lookup failed: {ex.Message}";
        }
        finally
        {
            lock (_artworkLookupsInFlight)
            {
                _artworkLookupsInFlight.Remove(album.Id);
            }
        }
    }

    private void ReplaceAlbumEverywhere(Album oldAlbum, Album updatedAlbum)
    {
        ReplaceAlbumInCollection(Albums, oldAlbum, updatedAlbum);
        ReplaceAlbumInCollection(SelectedArtistAlbums, oldAlbum, updatedAlbum);
        var index = _allAlbums.FindIndex(a => a.Id == oldAlbum.Id);
        if (index >= 0)
        {
            _allAlbums[index] = updatedAlbum;
        }
    }

    private static void ReplaceAlbumInCollection(ObservableCollection<Album> collection, Album oldAlbum, Album updatedAlbum)
    {
        for (int i = 0; i < collection.Count; i++)
        {
            if (collection[i].Id == oldAlbum.Id)
            {
                collection[i] = updatedAlbum;
                return;
            }
        }
    }

    public void FilterQuery(string query)
    {
        _searchQuery = query;
        if (string.IsNullOrWhiteSpace(query))
        {
            Albums.Clear();
            foreach (var a in _allAlbums) Albums.Add(a);

            Artists.Clear();
            foreach (var a in _allArtists) Artists.Add(a);

            Songs.Clear();
            foreach (var s in _allSongs) Songs.Add(s);
            return;
        }

        var lower = query.Trim().ToLowerInvariant();

        Songs.Clear();
        foreach (var s in _allSongs.Where(s => 
            s.Title.ToLowerInvariant().Contains(lower) || 
            s.ArtistName.ToLowerInvariant().Contains(lower) || 
            s.AlbumTitle.ToLowerInvariant().Contains(lower) || 
            s.Genre.ToLowerInvariant().Contains(lower)))
        {
            Songs.Add(s);
        }

        Albums.Clear();
        foreach (var a in _allAlbums.Where(a => 
            a.Title.ToLowerInvariant().Contains(lower) || 
            a.ArtistName.ToLowerInvariant().Contains(lower) || 
            a.Genre.ToLowerInvariant().Contains(lower)))
        {
            Albums.Add(a);
        }

        Artists.Clear();
        foreach (var a in _allArtists.Where(a => 
            a.Name.ToLowerInvariant().Contains(lower)))
        {
            Artists.Add(a);
        }
    }

    public async Task RefreshDataAsync()
    {
        var albums = await _libraryService.GetAllAlbumsAsync();
        _allAlbums = albums.ToList();
        Albums.Clear();
        foreach (var album in albums)
        {
            Albums.Add(album);
        }

        var artists = await _libraryService.GetAllArtistsAsync();
        _allArtists = artists.ToList();
        Artists.Clear();
        foreach (var artist in artists)
        {
            Artists.Add(artist);
        }

        var songs = await _libraryService.GetAllTracksAsync();
        _allSongs = songs.ToList();
        Songs.Clear();
        var uniqueGenres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var song in songs)
        {
            Songs.Add(song);
            if (!string.IsNullOrWhiteSpace(song.Genre))
            {
                uniqueGenres.Add(song.Genre);
            }
        }

        Genres.Clear();
        foreach (var g in uniqueGenres.OrderBy(x => x))
        {
            Genres.Add(g);
        }

        await PlaylistsVM.LoadPlaylistsAsync();

        if (Artists.Count > 0 && SelectedArtist == null)
        {
            SelectedArtist = Artists[0];
        }

        if (Genres.Count > 0 && SelectedGenre == null)
        {
            SelectedGenre = Genres[0];
        }
    }

    private void UpdateSelectedArtistAlbums()
    {
        SelectedArtistAlbums.Clear();
        if (_selectedArtist == null) return;

        var artistAlbums = Albums.Where(a => a.ArtistId == _selectedArtist.Id || 
                                             string.Equals(a.ArtistName, _selectedArtist.Name, StringComparison.OrdinalIgnoreCase))
                                 .OrderByDescending(a => a.Year);
        foreach (var album in artistAlbums)
        {
            SelectedArtistAlbums.Add(album);
        }
    }

    private void UpdateSelectedGenreSongs()
    {
        SelectedGenreSongs.Clear();
        if (string.IsNullOrEmpty(_selectedGenre)) return;

        var genreTracks = Songs.Where(t => string.Equals(t.Genre, _selectedGenre, StringComparison.OrdinalIgnoreCase));
        foreach (var track in genreTracks)
        {
            SelectedGenreSongs.Add(track);
        }
    }

    private async Task PerformSearchAsync()
    {
        var results = await _libraryService.SearchAsync(_searchQuery);
        Songs.Clear();
        foreach (var s in results)
        {
            Songs.Add(s);
        }
    }

    private async Task OnPlaySongAsync(Track? track)
    {
        if (track == null) return;
        await _playerCoordinator.PlayTrackAsync(track, Songs);
    }

    private async Task OnPlayAlbumAsync(Album? album)
    {
        if (album == null || album.Tracks.Count == 0) return;
        await _playerCoordinator.PlayTrackAsync(album.Tracks[0], album.Tracks);
    }

    private async Task OnPlayArtistAsync(Artist? artist)
    {
        if (artist == null) return;
        var artistTracks = Songs.Where(t => t.ArtistId == artist.Id || 
                                           string.Equals(t.ArtistName, artist.Name, StringComparison.OrdinalIgnoreCase))
                                .ToList();
        if (artistTracks.Count > 0)
        {
            await _playerCoordinator.PlayTrackAsync(artistTracks[0], artistTracks);
        }
    }

    private async Task OnPlayGenreAsync(string? genre)
    {
        if (string.IsNullOrEmpty(genre)) return;
        var genreTracks = Songs.Where(t => string.Equals(t.Genre, genre, StringComparison.OrdinalIgnoreCase)).ToList();
        if (genreTracks.Count > 0)
        {
            await _playerCoordinator.PlayTrackAsync(genreTracks[0], genreTracks);
        }
    }

    private async Task OnToggleFavoriteAsync(Track? track)
    {
        if (track == null) return;
        var newRating = track.Rating == HeartRating.Favorite ? HeartRating.None : HeartRating.Favorite;
        track.Rating = newRating;
        await _playerCoordinator.SetRatingAsync(track.Id, newRating);
        await _libraryService.SetTrackRatingAsync(track.Id, newRating);
    }

    private async Task OnToggleDislikeAsync(Track? track)
    {
        if (track == null) return;
        var newRating = track.Rating == HeartRating.Dislike ? HeartRating.None : HeartRating.Dislike;
        track.Rating = newRating;
        await _playerCoordinator.SetRatingAsync(track.Id, newRating);
        await _libraryService.SetTrackRatingAsync(track.Id, newRating);
    }

    private async Task OnStartSmartDjFromTrackAsync(Track? track)
    {
        if (track == null) return;
        var allTracks = await _libraryService.GetAllTracksAsync();
        var seed = new SmartDJSeed
        {
            SeedTrackId = track.Id,
            SeedArtistId = track.ArtistId,
            SeedGenre = track.Genre,
            TargetTrackCount = 25,
            ExcludeDisliked = true
        };
        var mix = await _smartDJService.GenerateMixAsync(seed, allTracks);
        if (mix.Count > 0)
        {
            await _playerCoordinator.PlayTrackAsync(mix[0], mix);
        }
    }

    private async Task OnStartSmartDjFromAlbumAsync(Album? album)
    {
        if (album == null) return;
        var allTracks = await _libraryService.GetAllTracksAsync();
        var seed = new SmartDJSeed
        {
            SeedAlbumId = album.Id,
            SeedArtistId = album.ArtistId,
            SeedGenre = album.Genre,
            TargetTrackCount = 25,
            ExcludeDisliked = true
        };
        var mix = await _smartDJService.GenerateMixAsync(seed, allTracks);
        if (mix.Count > 0)
        {
            await _playerCoordinator.PlayTrackAsync(mix[0], mix);
        }
    }

    private async Task OnStartSmartDjFromArtistAsync(Artist? artist)
    {
        if (artist == null) return;
        var allTracks = await _libraryService.GetAllTracksAsync();
        var seed = new SmartDJSeed
        {
            SeedArtistId = artist.Id,
            TargetTrackCount = 25,
            ExcludeDisliked = true
        };
        var mix = await _smartDJService.GenerateMixAsync(seed, allTracks);
        if (mix.Count > 0)
        {
            await _playerCoordinator.PlayTrackAsync(mix[0], mix);
        }
    }
}
