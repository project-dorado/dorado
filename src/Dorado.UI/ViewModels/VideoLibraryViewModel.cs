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

/// <summary>
/// Video library browser (VIDEOLIBRARY parity).
/// </summary>
public class VideoLibraryViewModel : ViewModelBase
{
    private readonly IVideoLibraryService _videoLibraryService;
    private readonly IVideoPlaybackEngine _videoEngine;

    public ObservableCollection<Video> Videos { get; } = new();

    private Video? _selectedVideo;
    public Video? SelectedVideo
    {
        get => _selectedVideo;
        set
        {
            if (SetProperty(ref _selectedVideo, value))
            {
                OnPropertyChanged(nameof(HasVideos));
            }
        }
    }

    public bool HasVideos => Videos.Count > 0;

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set => SetProperty(ref _isScanning, value);
    }

    private string? _statusText;
    public string? StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private VideoPlaybackViewModel? _activePlayerVM;
    public VideoPlaybackViewModel? ActivePlayerVM
    {
        get => _activePlayerVM;
        set
        {
            if (SetProperty(ref _activePlayerVM, value))
            {
                OnPropertyChanged(nameof(IsPlayerOpen));
            }
        }
    }

    public bool IsPlayerOpen => ActivePlayerVM != null;

    public ICommand PlayVideoCommand { get; }
    public ICommand ClosePlayerCommand { get; }
    public ICommand ScanFolderCommand { get; }

    public VideoLibraryViewModel(IVideoLibraryService videoLibraryService, IVideoPlaybackEngine videoEngine)
    {
        _videoLibraryService = videoLibraryService;
        _videoEngine = videoEngine;

        PlayVideoCommand = new RelayCommand<Video>(OnPlayVideoAsync);
        ClosePlayerCommand = new RelayCommand(OnClosePlayer);
        ScanFolderCommand = new AsyncRelayCommand<string>(OnScanFolderAsync);

        _ = LoadVideosAsync();
    }

    public async Task LoadVideosAsync()
    {
        var videos = await _videoLibraryService.GetAllVideosAsync();
        Videos.Clear();
        foreach (var v in videos)
        {
            Videos.Add(v);
        }

        OnPropertyChanged(nameof(HasVideos));
    }

    private void OnPlayVideoAsync(Video? video)
    {
        if (video == null)
        {
            return;
        }

        SelectedVideo = video;
        ActivePlayerVM = new VideoPlaybackViewModel(_videoEngine, video);
        ActivePlayerVM.RequestClose += async (_, _) =>
        {
            ActivePlayerVM = null;
            await _videoLibraryService.MarkPlayedAsync(video.Id);
        };
        ActivePlayerVM.Start();
    }

    private void OnClosePlayer()
    {
        ActivePlayerVM?.CloseCommand.Execute(null);
        ActivePlayerVM = null;
    }

    private async Task OnScanFolderAsync(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            StatusText = "Enter or select a valid folder to scan for videos.";
            return;
        }

        IsScanning = true;
        StatusText = $"Scanning {folderPath} for videos...";
        try
        {
            await _videoLibraryService.ScanDirectoryAsync(folderPath);
            await LoadVideosAsync();
            StatusText = $"Video library scan complete — {Videos.Count} videos.";
        }
        catch (Exception ex)
        {
            StatusText = $"Video scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }
}
