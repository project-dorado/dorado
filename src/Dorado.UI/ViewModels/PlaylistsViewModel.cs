using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Interfaces;
using Dorado.Domain.Models;

namespace Dorado.UI.ViewModels;

public class PlaylistsViewModel : ViewModelBase
{
    private readonly IMediaLibraryService _libraryService;
    private readonly IPlayerCoordinator _playerCoordinator;
    private readonly ISmartPlaylistService? _smartPlaylistService;

    public ObservableCollection<Playlist> Playlists { get; } = new();

    private readonly List<Playlist> _allPlaylists = new();

    /// <summary>Filters the visible playlist list by name/description (collection search parity).</summary>
    public void Filter(string query)
    {
        Playlists.Clear();
        if (string.IsNullOrWhiteSpace(query))
        {
            foreach (var p in _allPlaylists) Playlists.Add(p);
            return;
        }

        var lower = query.Trim().ToLowerInvariant();
        foreach (var p in _allPlaylists.Where(p =>
            (p.Name?.ToLowerInvariant().Contains(lower) ?? false) ||
            (p.Description?.ToLowerInvariant().Contains(lower) ?? false)))
        {
            Playlists.Add(p);
        }
    }
    public ObservableCollection<Track> SelectedPlaylistTracks { get; } = new();
    public ObservableCollection<SmartPlaylist> SmartPlaylists { get; } = new();
    public ObservableCollection<Track> SelectedSmartPlaylistTracks { get; } = new();

    private Playlist? _selectedPlaylist;
    public Playlist? SelectedPlaylist
    {
        get => _selectedPlaylist;
        set
        {
            if (SetProperty(ref _selectedPlaylist, value))
            {
                OnPropertyChanged(nameof(HasSelectedPlaylist));
                OnPropertyChanged(nameof(ShowStaticPlaylistPane));
                OnPropertyChanged(nameof(ShowEmptyPlaylistPane));
                OnPropertyChanged(nameof(PlaylistTitle));
                OnPropertyChanged(nameof(PlaylistStatsText));
                _ = LoadSelectedPlaylistTracksAsync(value);
            }
        }
    }

    private SmartPlaylist? _selectedSmartPlaylist;
    public SmartPlaylist? SelectedSmartPlaylist
    {
        get => _selectedSmartPlaylist;
        set
        {
            if (SetProperty(ref _selectedSmartPlaylist, value))
            {
                OnPropertyChanged(nameof(HasSelectedSmartPlaylist));
                OnPropertyChanged(nameof(ShowStaticPlaylistPane));
                OnPropertyChanged(nameof(ShowEmptyPlaylistPane));
                OnPropertyChanged(nameof(SmartPlaylistTitle));
                OnPropertyChanged(nameof(SmartPlaylistStatsText));
                _ = EvaluateSelectedSmartPlaylistAsync();
            }
        }
    }

    public bool HasSelectedPlaylist => SelectedPlaylist != null;
    public bool HasSelectedSmartPlaylist => SelectedSmartPlaylist != null;
    public bool ShowStaticPlaylistPane => HasSelectedPlaylist && !HasSelectedSmartPlaylist;
    public bool ShowEmptyPlaylistPane => !HasSelectedPlaylist && !HasSelectedSmartPlaylist;
    public string PlaylistTitle => SelectedPlaylist?.Name ?? "Select a Playlist";
    public string SmartPlaylistTitle => SelectedSmartPlaylist?.Name ?? "Select a Smart Playlist";

    public string PlaylistStatsText
    {
        get
        {
            if (SelectedPlaylist == null) return string.Empty;
            int count = SelectedPlaylistTracks.Count;
            var totalDuration = TimeSpan.FromSeconds(SelectedPlaylistTracks.Sum(t => t.Duration.TotalSeconds));
            return $"{count} {(count == 1 ? "song" : "songs")} • {totalDuration.Hours * 60 + totalDuration.Minutes} mins";
        }
    }

    public string SmartPlaylistStatsText
    {
        get
        {
            if (SelectedSmartPlaylist == null) return string.Empty;
            int count = SelectedSmartPlaylistTracks.Count;
            var totalDuration = TimeSpan.FromSeconds(SelectedSmartPlaylistTracks.Sum(t => t.Duration.TotalSeconds));
            var ruleText = SelectedSmartPlaylist.Match == Domain.Models.SmartPlaylistMatch.All ? "all" : "any";
            return $"{count} {(count == 1 ? "song" : "songs")} • matches {ruleText} of {SelectedSmartPlaylist.Rules.Count} rule{((SelectedSmartPlaylist.Rules.Count == 1) ? string.Empty : "s")}";
        }
    }

    private string _newPlaylistName = string.Empty;
    public string NewPlaylistName
    {
        get => _newPlaylistName;
        set => SetProperty(ref _newPlaylistName, value);
    }

    private string? _statusMessage;
    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private SmartPlaylistEditorViewModel? _activeSmartEditorVM;
    public SmartPlaylistEditorViewModel? ActiveSmartEditorVM
    {
        get => _activeSmartEditorVM;
        set
        {
            if (SetProperty(ref _activeSmartEditorVM, value))
            {
                OnPropertyChanged(nameof(IsSmartEditorOpen));
            }
        }
    }

    public bool IsSmartEditorOpen => ActiveSmartEditorVM != null;

    public ICommand CreatePlaylistCommand { get; }
    public ICommand DeletePlaylistCommand { get; }
    public ICommand PlayPlaylistCommand { get; }
    public ICommand PlayTrackCommand { get; }
    public ICommand RemoveTrackCommand { get; }
    public ICommand ExportZplCommand { get; }
    public ICommand NewSmartPlaylistCommand { get; }
    public ICommand EditSmartPlaylistCommand { get; }
    public ICommand DeleteSmartPlaylistCommand { get; }
    public ICommand PlaySmartPlaylistCommand { get; }
    public ICommand RefreshSmartPlaylistCommand { get; }
    public ICommand AddTracksToPlaylistCommand { get; }
    public ICommand SwapHoverTargetCommand { get; }

    // Tier B2 (Zune 4.8 playlist drag-drop): the playlist currently highlighted as the drop target.
    // While a track is being dragged, the playlist under the cursor sets this; releasing the
    // drag on HoveredPlaylistId triggers AddTracksToPlaylistCommand.
    private Playlist? _hoveredPlaylist;
    public Playlist? HoveredPlaylist
    {
        get => _hoveredPlaylist;
        private set
        {
            if (SetProperty(ref _hoveredPlaylist, value))
            {
                OnPropertyChanged(nameof(IsPlaylistHovered));
                OnPropertyChanged(nameof(HoverSwapPlaylistName));
            }
        }
    }

    public bool IsPlaylistHovered => _hoveredPlaylist != null;

    /// <summary>True after ~300ms dwell on a playlist — shows the hover-swap popup of alternative drop targets.</summary>
    private bool _isHoverSwapExpanded;
    public bool IsHoverSwapExpanded
    {
        get => _isHoverSwapExpanded;
        private set
        {
            if (SetProperty(ref _isHoverSwapExpanded, value))
            {
                OnPropertyChanged(nameof(HoverSwapAlternatives));
            }
        }
    }

    /// <summary>Other playlists the user can land on while still hovering (Tier B2 hover-swap parity).</summary>
    public System.Collections.Generic.IReadOnlyList<Playlist> HoverSwapAlternatives =>
        Playlists.Where(p => p.Id != _hoveredPlaylist?.Id).ToList();

    public string HoverSwapPlaylistName => _hoveredPlaylist?.Name ?? string.Empty;

    private readonly System.Collections.Generic.Dictionary<Guid, Avalonia.Threading.DispatcherTimer> _hoverSwapTimers = new();

    private readonly IDialogService? _dialogService;

    public PlaylistsViewModel(
        IMediaLibraryService libraryService,
        IPlayerCoordinator playerCoordinator,
        ISmartPlaylistService? smartPlaylistService = null,
        IDialogService? dialogService = null)
    {
        _libraryService = libraryService;
        _playerCoordinator = playerCoordinator;
        _smartPlaylistService = smartPlaylistService;
        _dialogService = dialogService;

        CreatePlaylistCommand = new AsyncRelayCommand(OnCreatePlaylistAsync);
        DeletePlaylistCommand = new AsyncRelayCommand<Playlist>(OnDeletePlaylistAsync);
        PlayPlaylistCommand = new AsyncRelayCommand(OnPlayPlaylistAsync);
        PlayTrackCommand = new AsyncRelayCommand<Track>(OnPlayTrackAsync);
        RemoveTrackCommand = new AsyncRelayCommand<Track>(OnRemoveTrackAsync);
        ExportZplCommand = new AsyncRelayCommand(OnExportZplAsync);
        NewSmartPlaylistCommand = new RelayCommand(OnNewSmartPlaylist);
        EditSmartPlaylistCommand = new RelayCommand<SmartPlaylist>(OnEditSmartPlaylist);
        DeleteSmartPlaylistCommand = new AsyncRelayCommand<SmartPlaylist>(OnDeleteSmartPlaylistAsync);
        PlaySmartPlaylistCommand = new AsyncRelayCommand(OnPlaySmartPlaylistAsync);
        RefreshSmartPlaylistCommand = new AsyncRelayCommand(OnRefreshSmartPlaylistAsync);

        // Tier B2: drag-drop + hover-swap commands. The parameters are tuples of
        // (Playlist playlist, IReadOnlyList<Track> tracks) — Avalonia's drag-drop payload
        // is deserialized on the drop site.
        AddTracksToPlaylistCommand = new AsyncRelayCommand<(Playlist Playlist, IReadOnlyList<Track> Tracks)>(
            async payload => await OnAddTracksAsync(payload.Playlist, payload.Tracks));
        SwapHoverTargetCommand = new RelayCommand<Playlist>(p => HoveredPlaylist = p);

        _ = LoadPlaylistsAsync();
    }

    public async Task LoadPlaylistsAsync()
    {
        var list = await _libraryService.GetAllPlaylistsAsync();
        _allPlaylists.Clear();
        _allPlaylists.AddRange(list);
        Playlists.Clear();
        foreach (var p in _allPlaylists)
        {
            Playlists.Add(p);
        }

        if (_smartPlaylistService != null)
        {
            var smart = await _smartPlaylistService.GetAllAsync();
            SmartPlaylists.Clear();
            foreach (var sp in smart)
            {
                SmartPlaylists.Add(sp);
            }

            if (SelectedSmartPlaylist != null && SmartPlaylists.Any(sp => sp.Id == SelectedSmartPlaylist.Id))
            {
                await EvaluateSelectedSmartPlaylistAsync();
            }
        }

        if (SelectedPlaylist == null || !Playlists.Any(p => p.Id == SelectedPlaylist.Id))
        {
            SelectedPlaylist = Playlists.FirstOrDefault();
        }
        else
        {
            await LoadSelectedPlaylistTracksAsync(SelectedPlaylist);
        }
    }

    private async Task LoadSelectedPlaylistTracksAsync(Playlist? playlist)
    {
        SelectedPlaylistTracks.Clear();
        if (playlist == null)
        {
            OnPropertyChanged(nameof(PlaylistStatsText));
            return;
        }

        var tracks = await _libraryService.GetPlaylistTracksAsync(playlist.Id);
        foreach (var t in tracks)
        {
            SelectedPlaylistTracks.Add(t);
        }
        OnPropertyChanged(nameof(PlaylistStatsText));
    }

    private async Task OnCreatePlaylistAsync()
    {
        var name = string.IsNullOrWhiteSpace(NewPlaylistName) ? "New Playlist" : NewPlaylistName.Trim();
        var playlist = await _libraryService.CreatePlaylistAsync(name);
        NewPlaylistName = string.Empty;
        await LoadPlaylistsAsync();
        SelectedPlaylist = Playlists.FirstOrDefault(p => p.Id == playlist.Id);
        StatusMessage = $"Created playlist '{playlist.Name}'";
    }

    private async Task OnDeletePlaylistAsync(Playlist? playlist)
    {
        var target = playlist ?? SelectedPlaylist;
        if (target == null) return;

        if (_dialogService is not null)
        {
            var confirmed = await _dialogService.ConfirmAsync(new DialogRequest(
                "Delete playlist",
                $"Delete \u201c{target.Name}\u201d? This cannot be undone.",
                "DELETE",
                "CANCEL",
                IsDestructive: true));
            if (!confirmed)
            {
                return;
            }
        }

        await _libraryService.DeletePlaylistAsync(target.Id);
        await LoadPlaylistsAsync();
        StatusMessage = $"Deleted playlist '{target.Name}'";
    }

    private Task OnPlayPlaylistAsync()
    {
        if (SelectedPlaylistTracks.Count > 0)
        {
            _playerCoordinator.Enqueue(SelectedPlaylistTracks.ToList());
            _ = _playerCoordinator.NextAsync();
        }
        return Task.CompletedTask;
    }

    private Task OnPlayTrackAsync(Track? track)
    {
        if (track != null)
        {
            _playerCoordinator.Enqueue(new[] { track });
            _ = _playerCoordinator.NextAsync();
        }
        return Task.CompletedTask;
    }

    private async Task OnRemoveTrackAsync(Track? track)
    {
        if (SelectedPlaylist == null || track == null) return;
        await _libraryService.RemoveTrackFromPlaylistAsync(SelectedPlaylist.Id, track.Id);
        await LoadSelectedPlaylistTracksAsync(SelectedPlaylist);
    }

    private async Task OnExportZplAsync()
    {
        if (SelectedPlaylist == null) return;
        try
        {
            var musicDir = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            var safeName = string.Join("_", SelectedPlaylist.Name.Split(Path.GetInvalidFileNameChars()));
            var outPath = Path.Combine(musicDir, "Playlists", $"{safeName}.zpl");
            await _libraryService.ExportPlaylistToZplAsync(SelectedPlaylist.Id, outPath);
            StatusMessage = $"Exported to {outPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    // ==========================================
    // SMART PLAYLISTS (AUTO PLAYLISTS)
    // ==========================================
    private void OnNewSmartPlaylist()
    {
        if (_smartPlaylistService == null)
        {
            StatusMessage = "Smart playlists are unavailable in this configuration.";
            return;
        }

        OpenSmartEditor(new SmartPlaylist { Name = string.Empty });
    }

    private void OnEditSmartPlaylist(SmartPlaylist? playlist)
    {
        if (_smartPlaylistService == null || playlist == null)
        {
            return;
        }

        OpenSmartEditor(playlist);
    }

    private void OpenSmartEditor(SmartPlaylist playlist)
    {
        var editor = new SmartPlaylistEditorViewModel(
            playlist,
            _smartPlaylistService!,
            _libraryService,
            async saved =>
            {
                await LoadPlaylistsAsync();
                SelectedSmartPlaylist = SmartPlaylists.FirstOrDefault(sp => sp.Id == saved.Id);
                StatusMessage = $"Saved smart playlist '{saved.Name}'";
            });
        editor.RequestClose += (_, _) => ActiveSmartEditorVM = null;
        ActiveSmartEditorVM = editor;
    }

    private async Task OnDeleteSmartPlaylistAsync(SmartPlaylist? playlist)
    {
        if (_smartPlaylistService == null || playlist == null)
        {
            return;
        }

        await _smartPlaylistService.DeleteAsync(playlist.Id);
        await LoadPlaylistsAsync();
        SelectedSmartPlaylist = SmartPlaylists.FirstOrDefault();
        StatusMessage = $"Deleted smart playlist '{playlist.Name}'";
    }

    private async Task EvaluateSelectedSmartPlaylistAsync()
    {
        SelectedSmartPlaylistTracks.Clear();
        OnPropertyChanged(nameof(SmartPlaylistStatsText));
        if (_smartPlaylistService == null || SelectedSmartPlaylist == null)
        {
            return;
        }

        try
        {
            var libraryTracks = await _libraryService.GetAllTracksAsync();
            var matches = _smartPlaylistService.Evaluate(SelectedSmartPlaylist, libraryTracks);
            foreach (var t in matches)
            {
                SelectedSmartPlaylistTracks.Add(t);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Smart playlist evaluation failed: {ex.Message}";
        }

        OnPropertyChanged(nameof(SmartPlaylistStatsText));
    }

    private async Task OnRefreshSmartPlaylistAsync()
    {
        await EvaluateSelectedSmartPlaylistAsync();
        StatusMessage = SelectedSmartPlaylist != null ? $"Refreshed '{SelectedSmartPlaylist.Name}'" : string.Empty;
    }

    private async Task OnPlaySmartPlaylistAsync()
    {
        if (SelectedSmartPlaylistTracks.Count == 0)
        {
            return;
        }

        var tracks = SelectedSmartPlaylistTracks.ToList();
        await _playerCoordinator.PlayTrackAsync(tracks[0], tracks);
    }

    // ----------------------------------------------------------------------
    // Tier B2: drag-drop + hover-swap for playlists (Zune 4.8 parity)
    // ----------------------------------------------------------------------

    /// <summary>Called when a track enters a playlist's drop zone. Sets hover state and arms the swap timer.</summary>
    public void EnterPlaylistDropZone(Playlist playlist)
    {
        HoveredPlaylist = playlist;
        ArmHoverSwapTimer(playlist);
    }

    /// <summary>Called when the cursor moves between drop targets inside the same playlist (re-arms the timer).</summary>
    public void RefreshPlaylistDropZone(Playlist playlist)
    {
        if (_hoveredPlaylist?.Id != playlist.Id)
        {
            HoveredPlaylist = playlist;
        }
        ArmHoverSwapTimer(playlist);
    }

    /// <summary>Called when the cursor leaves a playlist's drop zone — collapses the swap popup and clears highlight.</summary>
    public void LeavePlaylistDropZone(Playlist playlist)
    {
        if (_hoveredPlaylist?.Id == playlist.Id)
        {
            HoveredPlaylist = null;
            IsHoverSwapExpanded = false;
        }
        CancelHoverSwapTimer(playlist);
    }

    /// <summary>Called when the drop is released over a playlist — clears transient state.</summary>
    public void ClearPlaylistHover()
    {
        HoveredPlaylist = null;
        IsHoverSwapExpanded = false;
    }

    private void ArmHoverSwapTimer(Playlist playlist)
    {
        CancelHoverSwapTimer(playlist);
        var timer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (_hoveredPlaylist?.Id == playlist.Id)
            {
                IsHoverSwapExpanded = true;
            }
        };
        _hoverSwapTimers[playlist.Id] = timer;
        timer.Start();
    }

    private void CancelHoverSwapTimer(Playlist playlist)
    {
        if (_hoverSwapTimers.TryGetValue(playlist.Id, out var timer))
        {
            timer.Stop();
            _hoverSwapTimers.Remove(playlist.Id);
        }
    }

    private async Task OnAddTracksAsync(Playlist playlist, IReadOnlyList<Track> tracks)
    {
        foreach (var t in tracks)
        {
            await _libraryService.AddTrackToPlaylistAsync(playlist.Id, t.Id);
        }

        StatusMessage = $"Added {tracks.Count} track{(tracks.Count == 1 ? string.Empty : "s")} to {playlist.Name}.";
        if (SelectedPlaylist?.Id == playlist.Id)
        {
            await LoadSelectedPlaylistTracksAsync(playlist);
        }
    }
}
