using Avalonia.Headless.XUnit;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

public class SettingsPagesParityTests
{
    [AvaloniaFact]
    public void IsFileTypesSubPivotActive_ComposesWithTopLevelPivot()
    {
        var vm = new SettingsViewModel();
        Assert.False(vm.IsFileTypesSubPivotActive);

        vm.SoftwarePivot = SoftwareSubPivot.FileTypes;
        Assert.True(vm.IsFileTypesSubPivotActive);

        vm.TopLevelPivot = SettingsTopLevelPivot.Device;
        Assert.False(vm.IsFileTypesSubPivotActive);
    }

    [AvaloniaFact]
    public void IsPrivacySubPivotActive_ComposesWithTopLevelPivot()
    {
        var vm = new SettingsViewModel();
        vm.SoftwarePivot = SoftwareSubPivot.Privacy;
        Assert.True(vm.IsPrivacySubPivotActive);

        vm.TopLevelPivot = SettingsTopLevelPivot.Device;
        Assert.False(vm.IsPrivacySubPivotActive);
    }

    [AvaloniaFact]
    public void IsPhotosSubPivotActive_ComposesWithTopLevelPivot()
    {
        var vm = new SettingsViewModel();
        vm.SoftwarePivot = SoftwareSubPivot.Photos;
        Assert.True(vm.IsPhotosSubPivotActive);

        vm.TopLevelPivot = SettingsTopLevelPivot.Device;
        Assert.False(vm.IsPhotosSubPivotActive);
    }

    [AvaloniaFact]
    public void IsGeneralSubPivotActive_ComposesWithTopLevelPivot()
    {
        var vm = new SettingsViewModel();
        vm.SoftwarePivot = SoftwareSubPivot.General;
        Assert.True(vm.IsGeneralSubPivotActive);

        vm.TopLevelPivot = SettingsTopLevelPivot.Device;
        Assert.False(vm.IsGeneralSubPivotActive);
    }

    [AvaloniaFact]
    public void IngestExtensionsList_ParsesCommaSeparatedAndTrims()
    {
        var vm = new SettingsViewModel { IngestExtensions = " mp3 , m4a , wma ,, flac " };
        Assert.Equal(new[] { "mp3", "m4a", "wma", "flac" }, vm.IngestExtensionsList);
    }

    [AvaloniaFact]
    public void FileTypesPresets_HasZuneDefaults()
    {
        var vm = new SettingsViewModel();
        Assert.True(vm.FileTypesPresets.Count >= 3);
        Assert.Equal("mp3 only", vm.FileTypesPresets[^1]);
    }

    [AvaloniaFact]
    public void StartupViewOptions_QuickplayAndCollection()
    {
        var vm = new SettingsViewModel();
        Assert.Equal("Quickplay", vm.StartupViewOptions[0]);
        Assert.Equal("Collection", vm.StartupViewOptions[1]);
    }
}
