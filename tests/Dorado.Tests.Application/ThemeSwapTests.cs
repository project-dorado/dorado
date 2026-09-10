using Avalonia.Headless.XUnit;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

public class ThemeSwapTests
{
    [AvaloniaFact]
    public void SettingsViewModel_HasBothThemeOptions()
    {
        var vm = new SettingsViewModel();
        Assert.Equal(2, vm.ThemeOptions.Count);
        Assert.True(vm.ThemeOptions[0].IsDark);
        Assert.False(vm.ThemeOptions[1].IsDark);
        Assert.Equal("Soft White (Light)", vm.ThemeOptions[1].Name);
    }

    [AvaloniaFact]
    public void SelectedTheme_DefaultsToDark()
    {
        var vm = new SettingsViewModel();
        Assert.True(vm.SelectedTheme.IsDark);
    }

    [AvaloniaFact]
    public void SelectedTheme_ChangingToLight_SetsIsDarkFalse()
    {
        var vm = new SettingsViewModel();
        vm.SelectedTheme = vm.ThemeOptions[1];
        Assert.False(vm.SelectedTheme.IsDark);
        Assert.Equal("Soft White (Light)", vm.SelectedTheme.Name);
    }
}
