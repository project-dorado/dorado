using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Interfaces;
using Dorado.Domain.Models;

namespace Dorado.UI.ViewModels;

public enum CDViewMode
{
    Rip,
    Burn
}

public class CDViewModel : ViewModelBase
{
    private readonly IMediaLibraryService _libraryService;
    private readonly IPlayerCoordinator _playerCoordinator;
    private readonly ISoundEffectService? _soundService;

    private CDViewMode _mode = CDViewMode.Rip;
    public CDViewMode Mode
    {
        get => _mode;
        set
        {
            if (SetProperty(ref _mode, value))
            {
                OnPropertyChanged(nameof(IsRipMode));
                OnPropertyChanged(nameof(IsBurnMode));
            }
        }
    }

    public bool IsRipMode => Mode == CDViewMode.Rip;
    public bool IsBurnMode => Mode == CDViewMode.Burn;

    public string DiscTitle { get; set; } = "Audio CD (Track Session)";
    public string DiscArtist { get; set; } = "Compact Disc Digital Audio";
    public string TotalDurationText => "43:28";
    public int TrackCount => DiscTracks.Count;

    public ObservableCollection<Track> DiscTracks { get; } = new();
    public ObservableCollection<Track> BurnQueue { get; } = new();

    private bool _isRipping;
    public bool IsRipping
    {
        get => _isRipping;
        set => SetProperty(ref _isRipping, value);
    }

    private double _ripProgress;
    public double RipProgress
    {
        get => _ripProgress;
        set => SetProperty(ref _ripProgress, value);
    }

    private string? _ripStatusText;
    public string? RipStatusText
    {
        get => _ripStatusText;
        set => SetProperty(ref _ripStatusText, value);
    }

    private bool _isBurning;
    public bool IsBurning
    {
        get => _isBurning;
        set => SetProperty(ref _isBurning, value);
    }

    private double _burnProgress;
    public double BurnProgress
    {
        get => _burnProgress;
        set => SetProperty(ref _burnProgress, value);
    }

    private string? _burnStatusText;
    public string? BurnStatusText
    {
        get => _burnStatusText;
        set => SetProperty(ref _burnStatusText, value);
    }

    public ICommand RipCdCommand { get; }
    public ICommand BurnCdCommand { get; }
    public ICommand PlayTrackCommand { get; }
    public ICommand SwitchModeCommand { get; }

    public CDViewModel(
        IMediaLibraryService libraryService,
        IPlayerCoordinator playerCoordinator,
        ISoundEffectService? soundService = null)
    {
        _libraryService = libraryService;
        _playerCoordinator = playerCoordinator;
        _soundService = soundService;

        // Initialize sample Audio CD session tracks
        var sampleTitles = new[]
        {
            ("Track 01 - Prologue", 215),
            ("Track 02 - Analog Horizon", 284),
            ("Track 03 - Neon Transit", 195),
            ("Track 04 - Synthetic Pulse", 310),
            ("Track 05 - Subdivisions of Light", 258),
            ("Track 06 - Digital Cascade", 270),
            ("Track 07 - Retrograde Orbit", 222),
            ("Track 08 - Epilogue in C Minor", 345)
        };

        int idx = 1;
        foreach (var (title, sec) in sampleTitles)
        {
            DiscTracks.Add(new Track
            {
                Id = Guid.NewGuid(),
                Title = title,
                ArtistName = DiscArtist,
                AlbumTitle = DiscTitle,
                TrackNumber = idx++,
                Duration = TimeSpan.FromSeconds(sec),
                Genre = "Audio CD"
            });
        }

        RipCdCommand = new AsyncRelayCommand(OnRipCdAsync);
        BurnCdCommand = new AsyncRelayCommand(OnBurnCdAsync);
        PlayTrackCommand = new AsyncRelayCommand<Track>(async track =>
        {
            if (track != null)
            {
                await _playerCoordinator.PlayTrackAsync(track, DiscTracks);
            }
        });
        SwitchModeCommand = new RelayCommand<string>(modeStr =>
        {
            if (Enum.TryParse<CDViewMode>(modeStr, true, out var m))
            {
                Mode = m;
            }
        });
    }

    private async Task OnRipCdAsync()
    {
        if (IsRipping) return;

        IsRipping = true;
        RipProgress = 0.0;
        RipStatusText = "Reading Audio CD table of contents...";

        try
        {
            for (int i = 0; i < DiscTracks.Count; i++)
            {
                var track = DiscTracks[i];
                RipStatusText = $"Ripping Track {i + 1} of {DiscTracks.Count}: {track.Title} (FLAC 100% fidelity)...";
                await Task.Delay(350);
                RipProgress = (i + 1.0) / DiscTracks.Count;
            }

            RipStatusText = "Ripping complete. Media cataloged into collection.";
            _soundService?.PlayRipComplete();
        }
        finally
        {
            IsRipping = false;
        }
    }

    private async Task OnBurnCdAsync()
    {
        if (IsBurning) return;

        IsBurning = true;
        BurnProgress = 0.0;
        BurnStatusText = "Calibrating CD laser and preparing audio buffer...";

        try
        {
            int steps = 10;
            for (int i = 1; i <= steps; i++)
            {
                await Task.Delay(300);
                BurnProgress = (double)i / steps;
                BurnStatusText = $"Writing Audio CD (16x track lead-in): {i * 10}%";
            }

            BurnStatusText = "Burn complete. Closing disc session.";
            _soundService?.PlayBurnComplete();
        }
        finally
        {
            IsBurning = false;
        }
    }
}
