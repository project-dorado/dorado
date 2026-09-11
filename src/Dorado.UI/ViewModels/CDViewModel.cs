using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
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
    private readonly IOpticalDriveService? _driveService;
    private readonly List<OpticalTrack> _realTracks = new();

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

    /// <summary>True when a real optical drive + toolchain is available (capability-gated).</summary>
    public bool IsRealDriveAvailable => _driveService?.IsAvailable == true;

    public ICommand RipCdCommand { get; }
    public ICommand BurnCdCommand { get; }
    public ICommand PlayTrackCommand { get; }
    public ICommand SwitchModeCommand { get; }
    public ICommand LoadDiscCommand { get; }

    public CDViewModel(
        IMediaLibraryService libraryService,
        IPlayerCoordinator playerCoordinator,
        ISoundEffectService? soundService = null,
        IOpticalDriveService? driveService = null)
    {
        _libraryService = libraryService;
        _playerCoordinator = playerCoordinator;
        _soundService = soundService;
        _driveService = driveService;

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
        LoadDiscCommand = new AsyncRelayCommand(OnLoadDiscAsync);
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

    /// <summary>
    /// Loads the disc: reads a real TOC when an optical drive + toolchain is
    /// available, otherwise stages the simulated session (clearly labelled).
    /// </summary>
    private async Task OnLoadDiscAsync()
    {
        if (!IsRealDriveAvailable)
        {
            LoadSimulatedDisc();
            RipStatusText = "No optical drive available — loaded a simulated session.";
            return;
        }

        try
        {
            var toc = await _driveService!.ReadTocAsync();
            _realTracks.Clear();
            _realTracks.AddRange(toc);

            DiscTracks.Clear();
            BurnQueue.Clear();
            IsSimulatedDisc = false;
            DiscTitle = "Audio CD";
            DiscArtist = "Compact Disc Digital Audio";
            foreach (var t in toc)
            {
                DiscTracks.Add(new Track
                {
                    Id = Guid.NewGuid(),
                    Title = t.Title,
                    ArtistName = DiscArtist,
                    AlbumTitle = DiscTitle,
                    TrackNumber = t.Number,
                    Duration = t.Duration,
                    Genre = "Audio CD"
                });
            }

            OnPropertyChanged(nameof(DiscTitle));
            OnPropertyChanged(nameof(DiscArtist));
            OnPropertyChanged(nameof(IsSimulatedDisc));
            RipStatusText = $"Read {DiscTracks.Count} tracks from the optical drive.";
        }
        catch (Exception ex)
        {
            RipStatusText = $"Could not read disc: {ex.Message}";
        }
    }

    private bool TryBeginSession(out string? blockedReason)
    {
        if (!HasDisc)
        {
            blockedReason = "No disc detected.";
            return false;
        }
        if (!IsSimulatedDisc && !IsRealDriveAvailable)
        {
            blockedReason = "Optical-drive access is not implemented in this build.";
            return false;
        }
        blockedReason = null;
        return true;
    }

    private sealed class InlineProgress : IProgress<double>
    {
        private readonly Action<double> _onReport;
        public InlineProgress(Action<double> onReport) => _onReport = onReport;
        public void Report(double value) => _onReport(value);
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

        try
        {
            if (IsRealDriveAvailable && !IsSimulatedDisc)
            {
                var destination = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Dorado Rips");
                Directory.CreateDirectory(destination);

                for (int i = 0; i < DiscTracks.Count; i++)
                {
                    var optical = i < _realTracks.Count
                        ? _realTracks[i]
                        : new OpticalTrack(i + 1, 0, 0, DiscTracks[i].Duration, DiscTracks[i].Title);
                    RipStatusText = $"Ripping track {i + 1} of {DiscTracks.Count}: {DiscTracks[i].Title}...";
                    var slice = 1.0 / DiscTracks.Count;
                    var offset = i * slice;
                    var progress = new InlineProgress(v => RipProgress = offset + (v * slice));

                    await _driveService!.RipTrackAsync(optical, destination, "FLAC (Lossless Free Audio)", progress);
                }

                RipProgress = 1.0;
                RipStatusText = $"Rip complete. {DiscTracks.Count} tracks written to {destination}.";
                _soundService?.PlayRipComplete();
                return;
            }

            RipStatusText = "Reading Audio CD table of contents (simulated)...";
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
        catch (Exception ex)
        {
            RipStatusText = $"Rip failed: {ex.Message}";
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

        try
        {
            if (IsRealDriveAvailable && !IsSimulatedDisc)
            {
                var files = BurnQueue.Select(t => t.FilePath).Where(File.Exists).ToList();
                if (files.Count == 0)
                {
                    BurnStatusText = "Add tracks to the burn queue first (only on-disk files can be burned).";
                    return;
                }

                BurnStatusText = $"Burning {files.Count} tracks to Audio CD...";
                var progress = new InlineProgress(v => BurnProgress = v);
                await _driveService!.BurnAsync(files, progress);
                BurnProgress = 1.0;
                BurnStatusText = $"Burn complete. Wrote {files.Count} tracks.";
                _soundService?.PlayBurnComplete();
                return;
            }

            BurnStatusText = "Preparing audio buffer (simulated)...";
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
        catch (Exception ex)
        {
            BurnStatusText = $"Burn failed: {ex.Message}";
        }
        finally
        {
            IsBurning = false;
        }
    }
}
