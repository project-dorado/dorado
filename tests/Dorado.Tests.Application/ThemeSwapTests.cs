using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Dorado.UI.ViewModels;
using Dorado.UI.Views;
using System.Linq;
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

    [AvaloniaFact]
    public void SelectedTheme_MarksOnlyActiveOptionSelected()
    {
        var vm = new SettingsViewModel();
        Assert.True(vm.ThemeOptions[0].IsSelected);
        Assert.False(vm.ThemeOptions[1].IsSelected);

        vm.SelectedTheme = vm.ThemeOptions[1];
        Assert.False(vm.ThemeOptions[0].IsSelected);
        Assert.True(vm.ThemeOptions[1].IsSelected);
    }

    /// <summary>
    /// Regression: building the theme swatch DataTemplate must not throw a binding
    /// ExpressionParseException (previously an invalid $parent cast on the theme border).
    /// </summary>
    [AvaloniaFact]
    public void SettingsView_ThemeSwatchTemplate_BuildsWithoutBindingErrors()
    {
        var vm = new SettingsViewModel { SoftwarePivot = SoftwareSubPivot.Display };
        var view = new SettingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 1100, Height = 700 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var themeItems = view.GetVisualDescendants()
            .OfType<ItemsControl>()
            .FirstOrDefault(c => c.Name == "ThemeOptionsItems");
        Assert.NotNull(themeItems);
        Assert.NotNull(themeItems!.ItemTemplate);

        // Building the templates is what parses/attaches the swatch border bindings.
        foreach (var option in vm.ThemeOptions)
        {
            Assert.NotNull(themeItems.ItemTemplate!.Build(option));
        }

        var accentItems = view.GetVisualDescendants()
            .OfType<ItemsControl>()
            .FirstOrDefault(c => c.ItemsSource == vm.AccentColors);
        Assert.NotNull(accentItems);
        Assert.NotNull(accentItems!.ItemTemplate);
        foreach (var option in vm.AccentColors)
        {
            Assert.NotNull(accentItems.ItemTemplate!.Build(option));
        }

        var backgroundItems = view.GetVisualDescendants()
            .OfType<ItemsControl>()
            .FirstOrDefault(c => c.ItemsSource == vm.BackgroundThemes);
        Assert.NotNull(backgroundItems);
        Assert.NotNull(backgroundItems!.ItemTemplate);
        foreach (var option in vm.BackgroundThemes)
        {
            Assert.NotNull(backgroundItems.ItemTemplate!.Build(option));
        }

        window.Close();
    }
}
