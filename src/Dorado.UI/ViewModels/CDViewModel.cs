using System;
using System.Collections.ObjectModel;
using System.Linq;
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

    /// <summary>
    /// True when the loaded tracks came from the built-in simulated session rather
    /// than a real optical disc. Rip/burn remain simulated in that case and must say so.
    /// </summary>
    public bool IsSimulatedDisc { get; private set; }

    /// <summary>
    /// True when a disc session is loaded. There is no optical-drive detection in this
    /// build, so this is false until <see cref="LoadSimulatedDisc"/> (or a future real
    /// TOC loader) populates the track list — which keeps the DISC pivot ephemeral.
    /// </summary>
    public bool HasDisc => DiscTracks.Count > 0;
    public bool HasNoDisc => !HasDisc;

    public string DiscTitle { get; set; } = string.Empty;
    public string DiscArtist { get; set; } = string.Empty;

    public string TotalDurationText
    {
        get
        {
            var total = TimeSpan.FromSeconds(DiscTracks.Sum(t => t.Duration.TotalSeconds));
            return total.TotalHours >= 1
                ? $"{(int)total.TotalHours}:{total.Minutes:00}:{total.Seconds:00}"
                : $"{total.Minutes}:{total.Seconds:00}";
        }
    }

    public int TrackCount => DiscTracks.Count;

    public ObservableCollection<Track> DiscTracks { get; } = new();
    public ObservableCollection<Track> BurnQueue { get; } = new();

    private bool _isRipping;
    public bool IsRipping
    {
        get => _isRipping;
        set
        {
            if (SetProperty(ref _isRipping, value))
            {
                OnPropertyChanged(nameof(CanRip));
            }
        }
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
        set
        {
            if (SetProperty(ref _ripStatusText, value))
            {
                OnPropertyChanged(nameof(HasRipMessage));
            }
        }
    }

    public bool HasRipMessage => !string.IsNullOrEmpty(RipStatusText);

    private bool _isBurning;
    public bool IsBurning
    {
        get => _isBurning;
        set
        {
            if (SetProperty(ref _isBurning, value))
            {
                OnPropertyChanged(nameof(CanBurn));
            }
        }
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
        set
        {
            if (SetProperty(ref _burnStatusText, value))
            {
                OnPropertyChanged(nameof(HasBurnMessage));
            }
        }
    }

    public bool HasBurnMessage => !string.IsNullOrEmpty(BurnStatusText);

    public bool CanRip => HasDisc && !IsRipping;
    public bool CanBurn => HasDisc && !IsBurning;

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

        DiscTracks.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasDisc));
            OnPropertyChanged(nameof(HasNoDisc));
            OnPropertyChanged(nameof(TrackCount));
            OnPropertyChanged(nameof(TotalDurationText));
            OnPropertyChanged(nameof(CanRip));
            OnPropertyChanged(nameof(CanBurn));
        };

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

    /// <summary>
    /// Loads the built-in simulated Audio CD session. Only ever invoked explicitly
    /// (tests / the DEBUG staging path) — the constructor no longer seeds a disc, so
    /// the DISC pivot stays hidden until a session is actually present.
    /// </summary>
    public void LoadSimulatedDisc()
    {
        IsSimulatedDisc = true;
        DiscTitle = "Audio CD (Simulated Session)";
        DiscArtist = "Compact Disc Digital Audio";
        DiscTracks.Clear();

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

        OnPropertyChanged(nameof(DiscTitle));
        OnPropertyChanged(nameof(DiscArtist));
        OnPropertyChanged(nameof(IsSimulatedDisc));
    }

    /// <summary>Clears the loaded session (disc ejected).</summary>
    public void EjectDisc()
    {
        DiscTracks.Clear();
        BurnQueue.Clear();
        IsSimulatedDisc = false;
        DiscTitle = string.Empty;
        DiscArtist = string.Empty;
        RipStatusText = null;
        BurnStatusText = null;
        RipProgress = 0;
        BurnProgress = 0;
        OnPropertyChanged(nameof(DiscTitle));
        OnPropertyChanged(nameof(DiscArtist));
        OnPropertyChanged(nameof(IsSimulatedDisc));
    }

    private bool TryBeginSession(out string? blockedReason)
    {
        if (!HasDisc)
        {
            blockedReason = "No disc detected.";
            return false;
        }
        if (!IsSimulatedDisc)
        {
            blockedReason = "Optical-drive access is not implemented in this build.";
            return false;
        }
        blockedReason = null;
        return true;
    }

    private async Task OnRipCdAsync()
    {
        if (IsRipping) return;

        if (!TryBeginSession(out var blocked))
        {
            RipStatusText = blocked;
            RipProgress = 0;
            return;
        }

        IsRipping = true;
        RipProgress = 0.0;
        RipStatusText = "Reading Audio CD table of contents (simulated)...";

        try
        {
            for (int i = 0; i < DiscTracks.Count; i++)
            {
                var track = DiscTracks[i];
                RipStatusText = $"Simulating rip — Track {i + 1} of {DiscTracks.Count}: {track.Title} (no optical drive; no files will be written)...";
                await Task.Delay(350);
                RipProgress = (i + 1.0) / DiscTracks.Count;
            }

            RipStatusText = "Simulation complete. No audio files were written (no optical drive available).";
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

        if (!TryBeginSession(out var blocked))
        {
            BurnStatusText = blocked;
            BurnProgress = 0;
            return;
        }

        IsBurning = true;
        BurnProgress = 0.0;
        BurnStatusText = "Preparing audio buffer (simulated)...";

        try
        {
            int steps = 10;
            for (int i = 1; i <= steps; i++)
            {
                await Task.Delay(300);
                BurnProgress = (double)i / steps;
                BurnStatusText = $"Simulating burn: {i * 10}% (no optical drive; no disc will be written)";
            }

            BurnStatusText = "Simulation complete. No disc was written (no optical drive available).";
            _soundService?.PlayBurnComplete();
        }
        finally
        {
            IsBurning = false;
        }
    }
}
