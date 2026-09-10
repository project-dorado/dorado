using Avalonia.Controls;
using Dorado.UI.ViewModels;

namespace Dorado.UI.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void OnFileTypesPresetChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.SelectedItem is string preset && DataContext is SettingsViewModel vm)
        {
            vm.IngestExtensions = preset;
        }
    }
}
