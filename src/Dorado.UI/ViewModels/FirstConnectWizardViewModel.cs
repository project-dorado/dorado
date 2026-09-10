using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Dorado.Domain.Models;

namespace Dorado.UI.ViewModels;

public enum FirstConnectStep
{
    Welcome,
    Name,
    SyncOptions,
    Privacy,
    Done
}

/// <summary>
/// Zune-style FirstConnect onboarding (FIRSTCONNECT.UIX parity):
/// welcome → device name → media-type sync toggles → privacy → done.
/// Triggered once per physical device serial the first time it connects.
/// </summary>
public class FirstConnectWizardViewModel : ViewModelBase
{
    private FirstConnectStep _currentStep = FirstConnectStep.Welcome;
    private string _deviceName = string.Empty;
    private bool _syncMusic = true;
    private bool _syncVideos = true;
    private bool _syncPhotos = true;
    private bool _syncPodcasts = true;
    private bool _shareAnonymousData;

    public event EventHandler<FirstConnectResult>? RequestClose;

    public ZuneDevice? Device { get; }

    public FirstConnectWizardViewModel(ZuneDevice device)
    {
        Device = device;
        _deviceName = string.IsNullOrWhiteSpace(device.ModelName) ? "Zune Device" : device.ModelName;

        NextCommand = new RelayCommand(OnNext);
        BackCommand = new RelayCommand(OnBack);
        SkipCommand = new RelayCommand(OnSkip);
        FinishCommand = new RelayCommand(OnFinish);
    }

    public FirstConnectStep CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (SetProperty(ref _currentStep, value))
            {
                OnPropertyChanged(nameof(IsWelcomeStep));
                OnPropertyChanged(nameof(IsNameStep));
                OnPropertyChanged(nameof(IsSyncOptionsStep));
                OnPropertyChanged(nameof(IsPrivacyStep));
                OnPropertyChanged(nameof(IsDoneStep));
                OnPropertyChanged(nameof(StepTitle));
                OnPropertyChanged(nameof(StepSubtitle));
            }
        }
    }

    public bool IsWelcomeStep => CurrentStep == FirstConnectStep.Welcome;
    public bool IsNameStep => CurrentStep == FirstConnectStep.Name;
    public bool IsSyncOptionsStep => CurrentStep == FirstConnectStep.SyncOptions;
    public bool IsPrivacyStep => CurrentStep == FirstConnectStep.Privacy;
    public bool IsDoneStep => CurrentStep == FirstConnectStep.Done;

    public string StepTitle => CurrentStep switch
    {
        FirstConnectStep.Welcome => "WELCOME TO DORADO",
        FirstConnectStep.Name => "NAME YOUR DEVICE",
        FirstConnectStep.SyncOptions => "CHOOSE WHAT TO SYNC",
        FirstConnectStep.Privacy => "PRIVACY OPTIONS",
        _ => "DEVICE READY"
    };

    public string StepSubtitle => CurrentStep switch
    {
        FirstConnectStep.Welcome => $"A new {(string.IsNullOrWhiteSpace(Device?.ModelName) ? "Zune" : Device!.ModelName)} device was just connected. Let's set it up.",
        FirstConnectStep.Name => "Pick a name for this device. It shows up on the device dock and in sync plans.",
        FirstConnectStep.SyncOptions => "Select which media types should be synced to this device by default. You can change any of these later in Settings.",
        FirstConnectStep.Privacy => "Choose what you'd like to share. You can change these later in Settings → Privacy.",
        _ => "Your device is configured. Ready to sync."
    };

    public string DeviceName
    {
        get => _deviceName;
        set => SetProperty(ref _deviceName, value);
    }

    public bool SyncMusic
    {
        get => _syncMusic;
        set => SetProperty(ref _syncMusic, value);
    }

    public bool SyncVideos
    {
        get => _syncVideos;
        set => SetProperty(ref _syncVideos, value);
    }

    public bool SyncPhotos
    {
        get => _syncPhotos;
        set => SetProperty(ref _syncPhotos, value);
    }

    public bool SyncPodcasts
    {
        get => _syncPodcasts;
        set => SetProperty(ref _syncPodcasts, value);
    }

    public bool ShareAnonymousData
    {
        get => _shareAnonymousData;
        set => SetProperty(ref _shareAnonymousData, value);
    }

    public bool CanGoBack => CurrentStep is FirstConnectStep.Name or FirstConnectStep.SyncOptions or FirstConnectStep.Privacy;

    public ICommand NextCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand SkipCommand { get; }
    public ICommand FinishCommand { get; }

    private void OnNext()
    {
        switch (CurrentStep)
        {
            case FirstConnectStep.Welcome:
                CurrentStep = FirstConnectStep.Name;
                break;
            case FirstConnectStep.Name:
                CurrentStep = FirstConnectStep.SyncOptions;
                break;
            case FirstConnectStep.SyncOptions:
                CurrentStep = FirstConnectStep.Privacy;
                break;
            case FirstConnectStep.Privacy:
                CurrentStep = FirstConnectStep.Done;
                break;
        }
        OnPropertyChanged(nameof(CanGoBack));
    }

    private void OnBack()
    {
        switch (CurrentStep)
        {
            case FirstConnectStep.Name:
                CurrentStep = FirstConnectStep.Welcome;
                break;
            case FirstConnectStep.SyncOptions:
                CurrentStep = FirstConnectStep.Name;
                break;
            case FirstConnectStep.Privacy:
                CurrentStep = FirstConnectStep.SyncOptions;
                break;
        }
        OnPropertyChanged(nameof(CanGoBack));
    }

    private void OnSkip()
    {
        OnFinish();
    }

    private void OnFinish()
    {
        var result = new FirstConnectResult(
            string.IsNullOrWhiteSpace(DeviceName) ? "Zune Device" : DeviceName,
            SyncMusic,
            SyncVideos,
            SyncPhotos,
            SyncPodcasts,
            ShareAnonymousData,
            Device);

        RequestClose?.Invoke(this, result);
    }
}

public record FirstConnectResult(
    string DeviceName,
    bool SyncMusic,
    bool SyncVideos,
    bool SyncPhotos,
    bool SyncPodcasts,
    bool ShareAnonymousData,
    ZuneDevice? Device);
