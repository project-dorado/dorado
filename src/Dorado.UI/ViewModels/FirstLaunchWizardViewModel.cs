using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Interfaces;

namespace Dorado.UI.ViewModels;

public enum FirstLaunchStep
{
    Welcome,
    ChooseFolder,
    Scanning,
    Done
}

/// <summary>
/// Zune-style first-launch onboarding (FIRSTLAUNCH parity):
/// welcome → monitored folder selection → library scan → done.
/// </summary>
public class FirstLaunchWizardViewModel : ViewModelBase
{
    private readonly IMediaLibraryService _libraryService;
    private readonly IFolderPickerService? _folderPicker;
    private readonly ISettingsStore? _settingsStore;
    private readonly ISoundEffectService? _soundService;

    private FirstLaunchStep _currentStep = FirstLaunchStep.Welcome;
    private string _folderPath = string.Empty;
    private double _scanProgress;
    private string _scanStatusText = string.Empty;
    private int _discoveredCount;

    public event EventHandler? RequestClose;

    public FirstLaunchWizardViewModel(
        IMediaLibraryService libraryService,
        IFolderPickerService? folderPicker,
        ISettingsStore? settingsStore,
        ISoundEffectService? soundService = null,
        string? initialFolderPath = null)
    {
        _libraryService = libraryService;
        _folderPicker = folderPicker;
        _settingsStore = settingsStore;
        _soundService = soundService;
        _folderPath = initialFolderPath ?? string.Empty;

        NextCommand = new AsyncRelayCommand(OnNextAsync);
        SkipCommand = new RelayCommand(OnSkip);
        BrowseCommand = new AsyncRelayCommand(OnBrowseAsync);
        FinishCommand = new RelayCommand(OnFinish);
    }

    public FirstLaunchStep CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (SetProperty(ref _currentStep, value))
            {
                OnPropertyChanged(nameof(IsWelcomeStep));
                OnPropertyChanged(nameof(IsChooseFolderStep));
                OnPropertyChanged(nameof(IsScanningStep));
                OnPropertyChanged(nameof(IsDoneStep));
                OnPropertyChanged(nameof(StepTitle));
                OnPropertyChanged(nameof(StepSubtitle));
            }
        }
    }

    public bool IsWelcomeStep => CurrentStep == FirstLaunchStep.Welcome;
    public bool IsChooseFolderStep => CurrentStep == FirstLaunchStep.ChooseFolder;
    public bool IsScanningStep => CurrentStep == FirstLaunchStep.Scanning;
    public bool IsDoneStep => CurrentStep == FirstLaunchStep.Done;

    public string StepTitle => CurrentStep switch
    {
        FirstLaunchStep.Welcome => "WELCOME TO DORADO",
        FirstLaunchStep.ChooseFolder => "CHOOSE YOUR MUSIC",
        FirstLaunchStep.Scanning => "BUILDING YOUR COLLECTION",
        _ => "YOU'RE ALL SET"
    };

    public string StepSubtitle => CurrentStep switch
    {
        FirstLaunchStep.Welcome => "The legendary Zune desktop experience, rebuilt for modern platforms.",
        FirstLaunchStep.ChooseFolder => "Point Dorado at your music folder. New files are picked up automatically as your collection grows.",
        FirstLaunchStep.Scanning => "Reading tags, artwork and ratings from your audio files...",
        _ => "Your collection is ready. Enjoy the mix."
    };

    public string FolderPath
    {
        get => _folderPath;
        set => SetProperty(ref _folderPath, value);
    }

    public double ScanProgress
    {
        get => _scanProgress;
        set => SetProperty(ref _scanProgress, value);
    }

    public string ScanStatusText
    {
        get => _scanStatusText;
        set => SetProperty(ref _scanStatusText, value);
    }

    public int DiscoveredCount
    {
        get => _discoveredCount;
        set => SetProperty(ref _discoveredCount, value);
    }

    public ICommand NextCommand { get; }
    public ICommand SkipCommand { get; }
    public ICommand BrowseCommand { get; }
    public ICommand FinishCommand { get; }

    private async Task OnNextAsync()
    {
        if (CurrentStep == FirstLaunchStep.Welcome)
        {
            CurrentStep = FirstLaunchStep.ChooseFolder;
            return;
        }

        if (CurrentStep == FirstLaunchStep.ChooseFolder)
        {
            var normalized = NormalizePath(FolderPath);
            if (string.IsNullOrWhiteSpace(normalized) || !Directory.Exists(normalized))
            {
                ScanStatusText = "Enter or select a valid music folder first.";
                return;
            }

            CurrentStep = FirstLaunchStep.Scanning;
            ScanProgress = 0.0;
            ScanStatusText = $"Scanning {normalized}...";

            try
            {
                var progress = new Progress<double>(p =>
                {
                    ScanProgress = p;
                    ScanStatusText = $"Scanning audio files: {(int)(p * 100)}%";
                });

                await _libraryService.ScanDirectoryAsync(normalized, progress);
                var tracks = await _libraryService.GetAllTracksAsync();
                DiscoveredCount = tracks.Count;
                ScanProgress = 1.0;
                ScanStatusText = "Scan complete.";
                _soundService?.PlaySyncComplete();

                PersistFolderPath(normalized);
                MarkCompleted();
                CurrentStep = FirstLaunchStep.Done;
            }
            catch (Exception ex)
            {
                ScanStatusText = $"Scan failed: {ex.Message}";
                CurrentStep = FirstLaunchStep.ChooseFolder;
            }
        }
    }

    private void OnSkip()
    {
        MarkCompleted();
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private void OnFinish()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private async Task OnBrowseAsync()
    {
        if (_folderPicker == null)
        {
            return;
        }

        var selected = await _folderPicker.PickFolderAsync("Select Music Collection Folder");
        if (!string.IsNullOrWhiteSpace(selected))
        {
            FolderPath = selected;
        }
    }

    private void PersistFolderPath(string normalizedPath)
    {
        if (_settingsStore == null)
        {
            return;
        }

        try
        {
            var settings = _settingsStore.Load();
            settings.MusicFolderPath = normalizedPath;
            settings.FirstLaunchCompleted = true;
            _settingsStore.Save(settings);
        }
        catch
        {
            // Best-effort persistence; the wizard outcome still stands.
        }
    }

    private void MarkCompleted()
    {
        if (_settingsStore == null)
        {
            return;
        }

        try
        {
            var settings = _settingsStore.Load();
            settings.FirstLaunchCompleted = true;
            _settingsStore.Save(settings);
        }
        catch
        {
        }
    }

    private static string NormalizePath(string path)
    {
        if (path.StartsWith("~/") || path.StartsWith("~\\"))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path.Substring(2));
        }

        return path;
    }
}
