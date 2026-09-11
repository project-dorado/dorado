using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dorado.Application.Interfaces;
using Dorado.Application.Services;
using Dorado.Domain.Models;
using Dorado.Tests.Application.TestFakes;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Regression coverage for the 2026-09-11 audit honesty fixes:
/// CD rip/burn must never claim success without a disc, and Device Info must
/// project the real connected device instead of fabricated standby constants.
/// </summary>
public class DeviceAndDiscHonestyTests
{
    // ---- CD / DISC ----------------------------------------------------------

    [Fact]
    public void CDViewModel_StartsWithNoDisc()
    {
        var vm = new CDViewModel(new FakeMediaLibraryService(), new PlaybackQueueCoordinator());

        Assert.False(vm.HasDisc);
        Assert.True(vm.HasNoDisc);
        Assert.False(vm.CanRip);
        Assert.False(vm.CanBurn);
        Assert.Equal(0, vm.TrackCount);
        Assert.Equal("0:00", vm.TotalDurationText);
    }

    [Fact]
    public void CDViewModel_SimulatedDisc_ReportsDerivedDuration()
    {
        var vm = new CDViewModel(new FakeMediaLibraryService(), new PlaybackQueueCoordinator());
        vm.LoadSimulatedDisc();

        Assert.True(vm.HasDisc);
        Assert.True(vm.IsSimulatedDisc);
        Assert.Equal(8, vm.TrackCount);
        // 215+284+195+310+258+270+222+345 = 2099s = 34:59
        Assert.Equal("34:59", vm.TotalDurationText);
        Assert.True(vm.CanRip);
    }

    [Fact]
    public async Task CDViewModel_NoDisc_RipAndBurnDoNotClaimSuccess()
    {
        var vm = new CDViewModel(new FakeMediaLibraryService(), new PlaybackQueueCoordinator());

        vm.RipCdCommand.Execute(null);
        await Task.Delay(50);
        Assert.Equal("No disc detected.", vm.RipStatusText);
        Assert.Equal(0.0, vm.RipProgress);
        Assert.False(vm.IsRipping);

        vm.SwitchModeCommand.Execute("Burn");
        vm.BurnCdCommand.Execute(null);
        await Task.Delay(50);
        Assert.Equal("No disc detected.", vm.BurnStatusText);
        Assert.Equal(0.0, vm.BurnProgress);
        Assert.False(vm.IsBurning);
    }

    [Fact]
    public async Task CDViewModel_SimulatedRip_MessagingIsHonest()
    {
        var vm = new CDViewModel(new FakeMediaLibraryService(), new PlaybackQueueCoordinator());
        vm.LoadSimulatedDisc();

        vm.RipCdCommand.Execute(null);
        for (int i = 0; i < 200 && vm.RipProgress < 1.0; i++) await Task.Delay(25);

        Assert.Equal(1.0, vm.RipProgress);
        Assert.NotNull(vm.RipStatusText);
        Assert.Contains("No audio files were written", vm.RipStatusText);
    }

    [Fact]
    public void CDViewModel_EjectDisc_ReturnsToNoDiscState()
    {
        var vm = new CDViewModel(new FakeMediaLibraryService(), new PlaybackQueueCoordinator());
        vm.LoadSimulatedDisc();
        Assert.True(vm.HasDisc);

        vm.EjectDisc();
        Assert.False(vm.HasDisc);
        Assert.False(vm.IsSimulatedDisc);
        Assert.Equal(0, vm.TrackCount);
    }

    // ---- Device Info --------------------------------------------------------

    [Fact]
    public void SettingsViewModel_DeviceInfo_NoDevice_ShowsHonestDisconnectedState()
    {
        var devices = new MutableDeviceSyncService();
        var vm = new SettingsViewModel(deviceSyncService: devices);

        Assert.False(vm.IsDeviceConnected);
        Assert.Equal("No device connected", vm.DeviceModelName);
        Assert.Equal("—", vm.DeviceSerialNumber);
        Assert.Equal("—", vm.FirmwareVersion);
        Assert.Equal("—", vm.DeviceBatteryText);
        Assert.Equal(0.0, vm.TotalCapacityGb);
    }

    [Fact]
    public void SettingsViewModel_DeviceInfo_ProjectsConnectedDevice()
    {
        var devices = new MutableDeviceSyncService
        {
            Devices =
            {
                new ZuneDevice
                {
                    ModelName = "Zune HD",
                    SerialNumber = "ABC123",
                    FirmwareVersion = "4.8",
                    CapacityBytes = 32L * 1024 * 1024 * 1024
                }
            }
        };
        var vm = new SettingsViewModel(deviceSyncService: devices);

        Assert.True(vm.IsDeviceConnected);
        Assert.Equal("Zune HD", vm.DeviceModelName);
        Assert.Equal("ABC123", vm.DeviceSerialNumber);
        Assert.Equal("4.8", vm.FirmwareVersion);
        Assert.Equal(32.0, vm.TotalCapacityGb, 1);
        Assert.Contains("reserved for device cache", vm.SpaceReservationSummaryText);

        // Space reservation math now derives from the real device capacity.
        vm.SpaceReservationPercent = 20;
        Assert.Contains("6.4 GB", vm.ReservedGbText);
        Assert.Contains("25.6 GB", vm.SyncSpaceGbText);
    }

    [Fact]
    public void SettingsViewModel_DeviceInfo_RefreshesOnConnectAndDisconnect()
    {
        var devices = new MutableDeviceSyncService();
        var vm = new SettingsViewModel(deviceSyncService: devices);
        Assert.False(vm.IsDeviceConnected);

        var device = new ZuneDevice { ModelName = "Zune HD", SerialNumber = "ZZZ", FirmwareVersion = "4.8" };
        devices.Connect(device);
        Assert.True(vm.IsDeviceConnected);
        Assert.Equal("Zune HD", vm.DeviceModelName);

        devices.Disconnect(device.SerialNumber);
        Assert.False(vm.IsDeviceConnected);
        Assert.Equal("No device connected", vm.DeviceModelName);
    }

    private sealed class MutableDeviceSyncService : IDeviceSyncService
    {
        public List<ZuneDevice> Devices { get; } = new();

        public IReadOnlyList<ZuneDevice> ConnectedDevices => Devices;
        public Task StartMonitoringAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SyncDeviceAsync(string serialNumber, IProgress<double>? progress = null) => Task.CompletedTask;

        public event EventHandler<ZuneDevice>? DeviceConnected;
        public event EventHandler<string>? DeviceDisconnected;

        public void Connect(ZuneDevice device)
        {
            Devices.Add(device);
            DeviceConnected?.Invoke(this, device);
        }

        public void Disconnect(string serialNumber)
        {
            var d = Devices.Find(x => x.SerialNumber == serialNumber);
            if (d is not null) Devices.Remove(d);
            DeviceDisconnected?.Invoke(this, serialNumber);
        }
    }
}
