using System;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Dorado.Application.Services;
using Dorado.Domain.Models;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

public class TransportControlsParityTests
{
    [Fact]
    public async Task MainShellViewModel_ToggleTimeDisplayCommand_SwitchesTotalAndRemainingTime()
    {
        var coordinator = new PlaybackQueueCoordinator();
        var dummyLibrary = new DummyMediaLibraryService();
        var dummyDevice = new DummyDeviceSyncService();
        var smartDj = new SmartDJEngine();

        var shellVm = new MainShellViewModel(coordinator, dummyLibrary, dummyDevice, smartDj);

        var track = new Track
        {
            Title = "Limelight",
            ArtistName = "Rush",
            Duration = TimeSpan.FromSeconds(260) // 4:20
        };
        await coordinator.PlayTrackAsync(track);

        // By default, ShowTotalTime is false, showing remaining time "-4:20"
        Assert.False(shellVm.ShowTotalTime);
        Assert.Equal("-4:20", shellVm.FormattedDurationText);

        // Click toggle time display
        Assert.True(shellVm.ToggleTimeDisplayCommand.CanExecute(null));
        shellVm.ToggleTimeDisplayCommand.Execute(null);

        // Now ShowTotalTime is true, showing total duration "4:20"
        Assert.True(shellVm.ShowTotalTime);
        Assert.Equal("4:20", shellVm.FormattedDurationText);

        // Toggle back to remaining time
        shellVm.ToggleTimeDisplayCommand.Execute(null);
        Assert.False(shellVm.ShowTotalTime);
        Assert.Equal("-4:20", shellVm.FormattedDurationText);
    }

    [Fact]
    public void MainShellViewModel_ToggleMuteCommand_TogglesCoordinatorMute()
    {
        var coordinator = new PlaybackQueueCoordinator();
        var dummyLibrary = new DummyMediaLibraryService();
        var dummyDevice = new DummyDeviceSyncService();
        var smartDj = new SmartDJEngine();

        var shellVm = new MainShellViewModel(coordinator, dummyLibrary, dummyDevice, smartDj);

        Assert.False(shellVm.IsMuted);
        Assert.False(coordinator.IsMuted);

        shellVm.ToggleMuteCommand.Execute(null);
        Assert.True(shellVm.IsMuted);
        Assert.True(coordinator.IsMuted);

        shellVm.ToggleMuteCommand.Execute(null);
        Assert.False(shellVm.IsMuted);
        Assert.False(coordinator.IsMuted);
    }

    [Fact]
    public async Task NowPlayingViewModel_ToggleTimeDisplayCommand_SwitchesDurationFormat()
    {
        var coordinator = new PlaybackQueueCoordinator();
        var dummyLibrary = new DummyMediaLibraryService();

        var npVm = new NowPlayingViewModel(coordinator, dummyLibrary);

        var track = new Track
        {
            Title = "Red Barchetta",
            ArtistName = "Rush",
            Duration = TimeSpan.FromSeconds(370) // 6:10
        };
        await coordinator.PlayTrackAsync(track);

        Assert.False(npVm.ShowTotalTime);
        Assert.Equal("-6:10", npVm.FormattedDurationText);

        npVm.ToggleTimeDisplayCommand.Execute(null);
        Assert.True(npVm.ShowTotalTime);
        Assert.Equal("6:10", npVm.FormattedDurationText);

        npVm.ToggleTimeDisplayCommand.Execute(null);
        Assert.False(npVm.ShowTotalTime);
        Assert.Equal("-6:10", npVm.FormattedDurationText);
    }

    [Fact]
    public void NowPlayingViewModel_VisualizerBands_Devkanro24BandsAndPeakCaps()
    {
        var coordinator = new PlaybackQueueCoordinator();
        var dummyLibrary = new DummyMediaLibraryService();

        var npVm = new NowPlayingViewModel(coordinator, dummyLibrary);

        Assert.Equal(24, npVm.VisualizerBands.Count);
        Assert.Equal(24, npVm.VisualizerBars.Count);

        for (int i = 0; i < 24; i++)
        {
            var band = npVm.VisualizerBands[i];
            Assert.Equal(4.0, band.Value);
            Assert.Equal(4.0, band.Peak);
            Assert.Equal(3.0, band.PeakMargin.Bottom);
        }

        // Test peak property update updates PeakMargin
        var testBand = npVm.VisualizerBands[0];
        testBand.Peak = 25.0;
        Assert.Equal(24.0, testBand.PeakMargin.Bottom);
    }
}
