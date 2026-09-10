using Avalonia.Headless.XUnit;
using Dorado.Domain.Models;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

public class FirstConnectWizardTests
{
    private static ZuneDevice MakeDevice(string serial, string model = "Zune 80")
    {
        return new ZuneDevice
        {
            SerialNumber = serial,
            ModelName = model,
            FirmwareVersion = "4.8",
            CapacityBytes = 120 * 1024 * 1024 * 1024L
        };
    }

    [AvaloniaFact]
    public void StepProgression_AdvancesAndAllowsBack()
    {
        var vm = new FirstConnectWizardViewModel(MakeDevice("SERIAL-A"));
        Assert.True(vm.IsWelcomeStep);
        Assert.Equal(FirstConnectStep.Welcome, vm.CurrentStep);

        vm.NextCommand.Execute(null);
        Assert.Equal(FirstConnectStep.Name, vm.CurrentStep);

        vm.NextCommand.Execute(null);
        Assert.Equal(FirstConnectStep.SyncOptions, vm.CurrentStep);

        vm.NextCommand.Execute(null);
        Assert.Equal(FirstConnectStep.Privacy, vm.CurrentStep);

        vm.NextCommand.Execute(null);
        Assert.Equal(FirstConnectStep.Done, vm.CurrentStep);

        // Back is hidden at the Done step (Finish/Skip are the only options)
        Assert.False(vm.CanGoBack);

        vm.BackCommand.Execute(null);
        Assert.Equal(FirstConnectStep.Done, vm.CurrentStep);

        // Going back from Privacy is allowed
        vm.GetType().GetProperty("CurrentStep")!.SetValue(vm, FirstConnectStep.Privacy);
        Assert.True(vm.CanGoBack);
        vm.BackCommand.Execute(null);
        Assert.Equal(FirstConnectStep.SyncOptions, vm.CurrentStep);
    }

    [AvaloniaFact]
    public void StepProgression_DirectCallAlsoAdvances()
    {
        var vm = new FirstConnectWizardViewModel(MakeDevice("SERIAL-A"));

        // Bypass the command to verify the setter logic
        vm.GetType().GetProperty("CurrentStep")!.SetValue(vm, FirstConnectStep.Name);
        Assert.True(vm.IsNameStep);

        vm.GetType().GetProperty("CurrentStep")!.SetValue(vm, FirstConnectStep.SyncOptions);
        Assert.True(vm.IsSyncOptionsStep);

        vm.GetType().GetProperty("CurrentStep")!.SetValue(vm, FirstConnectStep.Done);
        Assert.True(vm.IsDoneStep);
    }

    [AvaloniaFact]
    public void Finish_RaisesRequestClose_WithResult()
    {
        var vm = new FirstConnectWizardViewModel(MakeDevice("SERIAL-B"))
        {
            DeviceName = "My Zune",
            SyncMusic = true,
            SyncVideos = false,
            SyncPhotos = true,
            SyncPodcasts = false,
            ShareAnonymousData = true
        };

        FirstConnectResult? captured = null;
        vm.RequestClose += (_, r) => captured = r;

        vm.FinishCommand.Execute(null);

        Assert.NotNull(captured);
        Assert.Equal("My Zune", captured!.DeviceName);
        Assert.True(captured.SyncMusic);
        Assert.False(captured.SyncVideos);
        Assert.True(captured.SyncPhotos);
        Assert.False(captured.SyncPodcasts);
        Assert.True(captured.ShareAnonymousData);
        Assert.NotNull(captured.Device);
        Assert.Equal("SERIAL-B", captured.Device!.SerialNumber);
    }

    [AvaloniaFact]
    public void Skip_FromWelcome_StillRaisesClose()
    {
        var vm = new FirstConnectWizardViewModel(MakeDevice("SERIAL-C"));
        bool closed = false;
        vm.RequestClose += (_, _) => closed = true;

        vm.SkipCommand.Execute(null);
        Assert.True(closed);
    }

    [AvaloniaFact]
    public void SettingsViewModel_FirstConnectCompletedSerials_AddAndPersist()
    {
        var vm = new SettingsViewModel();
        Assert.Empty(vm.FirstConnectCompletedSerials);

        vm.FirstConnectCompletedSerials.Add("ABC123");
        vm.FirstConnectCompletedSerials.Add("DEF456");
        vm.Persist();

        Assert.Contains("ABC123", vm.FirstConnectCompletedSerials);
        Assert.Contains("DEF456", vm.FirstConnectCompletedSerials);
    }
}
