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
/// Photo library browser with folder tree + gallery grid (PHOTOLIBRARY / GALLERYVIEW parity).
/// </summary>
public class PhotoLibraryViewModel : ViewModelBase
{
    private readonly IPhotoLibraryService _photoLibraryService;

    public ObservableCollection<Photo> AllPhotos { get; } = new();
    public ObservableCollection<Photo> GalleryPhotos { get; } = new();
    public ObservableCollection<string> Folders { get; } = new();

    private string? _selectedFolder;
    public string? SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            if (SetProperty(ref _selectedFolder, value))
            {
                ApplyFolderFilter();
                OnPropertyChanged(nameof(SelectedFolderName));
                OnPropertyChanged(nameof(GalleryStatsText));
            }
        }
    }

    public string SelectedFolderName => SelectedFolder == AllFoldersEntry ? "All Photos" : Path.GetFileName(SelectedFolder ?? string.Empty);

    private Photo? _zoomedPhoto;
    public Photo? ZoomedPhoto
    {
        get => _zoomedPhoto;
        set
        {
            if (SetProperty(ref _zoomedPhoto, value))
            {
                OnPropertyChanged(nameof(IsZoomOpen));
            }
        }
    }

    public bool IsZoomOpen => ZoomedPhoto != null;

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

    public const string AllFoldersEntry = "<<all>>";

    public event EventHandler<Photo>? SlideshowRequested;

    public ICommand ZoomPhotoCommand { get; }
    public ICommand CloseZoomCommand { get; }
    public ICommand StartSlideshowCommand { get; }
    public ICommand ScanFolderCommand { get; }

    public PhotoLibraryViewModel(IPhotoLibraryService photoLibraryService)
    {
        _photoLibraryService = photoLibraryService;

        ZoomPhotoCommand = new RelayCommand<Photo>(p => ZoomedPhoto = p);
        CloseZoomCommand = new RelayCommand(() => ZoomedPhoto = null);
        StartSlideshowCommand = new RelayCommand(OnStartSlideshow);
        ScanFolderCommand = new AsyncRelayCommand<string>(OnScanFolderAsync);

        _ = LoadPhotosAsync();
    }

    public async Task LoadPhotosAsync()
    {
        var photos = await _photoLibraryService.GetAllPhotosAsync();
        AllPhotos.Clear();
        foreach (var p in photos)
        {
            AllPhotos.Add(p);
        }

        var folders = await _photoLibraryService.GetFoldersAsync();
        Folders.Clear();
        Folders.Add(AllFoldersEntry);
        foreach (var f in folders)
        {
            Folders.Add(f);
        }

        if (SelectedFolder == null || Folders.All(f => f != SelectedFolder))
        {
            SelectedFolder = AllFoldersEntry;
        }
        else
        {
            ApplyFolderFilter();
        }
    }

    private void ApplyFolderFilter()
    {
        GalleryPhotos.Clear();
        foreach (var p in SelectedFolder == null || SelectedFolder == AllFoldersEntry
            ? AllPhotos
            : AllPhotos.Where(p => p.FolderPath == SelectedFolder))
        {
            GalleryPhotos.Add(p);
        }

        OnPropertyChanged(nameof(GalleryStatsText));
        OnPropertyChanged(nameof(HasNoPhotos));
        OnPropertyChanged(nameof(CanStartSlideshow));
    }

    public string GalleryStatsText => $"{GalleryPhotos.Count} photo{((GalleryPhotos.Count == 1) ? string.Empty : "s")}";

    public bool HasNoPhotos => GalleryPhotos.Count == 0;

    public bool CanStartSlideshow => GalleryPhotos.Count > 0;

    private void OnStartSlideshow()
    {
        if (GalleryPhotos.Count > 0)
        {
            SlideshowRequested?.Invoke(this, GalleryPhotos[0]);
        }
    }

    private async Task OnScanFolderAsync(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            StatusText = "Enter or select a valid folder to scan for pictures.";
            return;
        }

        IsScanning = true;
        StatusText = $"Scanning {folderPath} for pictures...";
        try
        {
            await _photoLibraryService.ScanDirectoryAsync(folderPath);
            await LoadPhotosAsync();
            StatusText = $"Picture library scan complete — {AllPhotos.Count} photos.";
        }
        catch (Exception ex)
        {
            StatusText = $"Picture scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }
}
