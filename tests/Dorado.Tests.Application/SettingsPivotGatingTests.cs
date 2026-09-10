using Avalonia.Headless.XUnit;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Regression tests for the Settings pivot-gating bug:
/// the SoftwarePivot setter must raise OnPropertyChanged for every
/// composed IsXSubPivotActive flag, including the Phase 3 pages.
/// </summary>
public class SettingsPivotGatingTests
{
    [AvaloniaFact]
    public void SoftwarePivot_ChangeBetweenPhase3Pages_RaisesAllComposedFlags()
    {
        var vm = new SettingsViewModel();

        var fired = 0;
        void OnProp(object? _, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(SettingsViewModel.IsPodcastsSubPivotActive)
                or nameof(SettingsViewModel.IsFileTypesSubPivotActive)
                or nameof(SettingsViewModel.IsPrivacySubPivotActive)
                or nameof(SettingsViewModel.IsPhotosSubPivotActive)
                or nameof(SettingsViewModel.IsGeneralSubPivotActive))
            {
                fired++;
            }
        }
        vm.PropertyChanged += OnProp;

        vm.SoftwarePivot = SoftwareSubPivot.Podcasts;
        vm.SoftwarePivot = SoftwareSubPivot.FileTypes;
        vm.SoftwarePivot = SoftwareSubPivot.Privacy;
        vm.SoftwarePivot = SoftwareSubPivot.Photos;
        vm.SoftwarePivot = SoftwareSubPivot.General;

        vm.PropertyChanged -= OnProp;

        // 5 transitions × 5 distinct sub-pivot flags = 25 fired events.
        // (Each transition raises all 5 flags because the flag depends on
        // SoftwarePivot; the bug was that notifications weren't fired.)
        Assert.Equal(25, fired);

        // Final state: General is active.
        Assert.True(vm.IsGeneralSubPivotActive);
        Assert.False(vm.IsPodcastsSubPivotActive);
    }
}
