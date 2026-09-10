using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application;
using Dorado.Application.Interfaces;

namespace Dorado.UI.ViewModels;

/// <summary>
/// What's New dialog (WHATSNEW parity): shown once per application version change.
/// </summary>
public class WhatsNewViewModel : ViewModelBase
{
    private readonly ISettingsStore? _settingsStore;

    public event EventHandler? RequestClose;

    public WhatsNewViewModel(ISettingsStore? settingsStore)
    {
        _settingsStore = settingsStore;
        Highlights = new ObservableCollection<string>(AppInfo.WhatsNewHighlights);

        ContinueCommand = new RelayCommand(OnContinue);
    }

    public string Title => "WHAT'S NEW";

    public string VersionDisplay => AppInfo.VersionDisplay;

    public ObservableCollection<string> Highlights { get; }

    public ICommand ContinueCommand { get; }

    private void OnContinue()
    {
        if (_settingsStore != null)
        {
            try
            {
                var settings = _settingsStore.Load();
                settings.WhatsNewSeenVersion = AppInfo.Version;
                _settingsStore.Save(settings);
            }
            catch
            {
                // Best-effort persistence.
            }
        }

        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
