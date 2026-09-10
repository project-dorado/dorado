using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Interfaces;
using Dorado.Domain.Models;

namespace Dorado.UI.ViewModels;

public class MetadataEditViewModel : ViewModelBase
{
    private readonly IMediaLibraryService _libraryService;

    public Guid TrackId { get; }
    public string? FilePath { get; }

    private string _title = string.Empty;
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    private string _artistName = string.Empty;
    public string ArtistName
    {
        get => _artistName;
        set => SetProperty(ref _artistName, value);
    }

    private string _albumTitle = string.Empty;
    public string AlbumTitle
    {
        get => _albumTitle;
        set => SetProperty(ref _albumTitle, value);
    }

    private int? _year;
    public int? Year
    {
        get => _year;
        set => SetProperty(ref _year, value);
    }

    private string _genre = string.Empty;
    public string Genre
    {
        get => _genre;
        set => SetProperty(ref _genre, value);
    }

    private int _trackNumber;
    public int TrackNumber
    {
        get => _trackNumber;
        set => SetProperty(ref _trackNumber, value);
    }

    private int _discNumber;
    public int DiscNumber
    {
        get => _discNumber;
        set => SetProperty(ref _discNumber, value);
    }

    private string? _statusMessage;
    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public event EventHandler? RequestClose;

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public MetadataEditViewModel(Track track, IMediaLibraryService libraryService)
    {
        _libraryService = libraryService;

        TrackId = track.Id;
        FilePath = track.FilePath;
        Title = track.Title;
        ArtistName = track.ArtistName;
        AlbumTitle = track.AlbumTitle;
        Year = track.Year;
        Genre = track.Genre;
        TrackNumber = track.TrackNumber;
        DiscNumber = track.DiscNumber > 0 ? track.DiscNumber : 1;

        SaveCommand = new AsyncRelayCommand(OnSaveAsync);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, EventArgs.Empty));
    }

    private async Task OnSaveAsync()
    {
        try
        {
            await _libraryService.UpdateTrackMetadataAsync(
                TrackId,
                Title,
                ArtistName,
                AlbumTitle,
                Year,
                Genre,
                TrackNumber,
                DiscNumber);

            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save: {ex.Message}";
        }
    }
}
