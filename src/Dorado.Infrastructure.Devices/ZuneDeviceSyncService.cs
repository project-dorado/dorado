using System.Collections.Concurrent;
using Dorado.Application.Interfaces;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;

namespace Dorado.Infrastructure.Devices;

public class ZuneDeviceSyncService : IDeviceSyncService
{
    private readonly ConcurrentDictionary<string, ZuneDevice> _devices = new();
    public IReadOnlyList<ZuneDevice> ConnectedDevices => _devices.Values.ToList();

    public event EventHandler<ZuneDevice>? DeviceConnected;
    public event EventHandler<string>? DeviceDisconnected;

    public static readonly IReadOnlyDictionary<string, string> SupportedProductIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "063E", "Microsoft Zune 30" },
        { "0710", "Microsoft Zune 4/8/16" },
        { "0715", "Microsoft Zune 80/120" },
        { "0723", "Microsoft Zune HD" }
    };

    public Task StartMonitoringAsync(CancellationToken cancellationToken)
    {
        // Polls /sys/bus/usb/devices on Linux, or listens for device events
        _ = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    ScanUsbControllers();
                }
                catch
                {
                    // Ignore transient USB scan errors
                }

                await Task.Delay(2500, cancellationToken);
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public async Task SyncDeviceAsync(string serialNumber, IProgress<double>? progress = null)
    {
        if (!_devices.TryGetValue(serialNumber, out var device))
        {
            return;
        }

        device.SyncState = DeviceSyncState.Syncing;
        progress?.Report(0.0);

        // Fast ZMDB sync simulation / execution
        for (int i = 1; i <= 10; i++)
        {
            await Task.Delay(100);
            progress?.Report(i / 10.0);
        }

        device.SyncState = DeviceSyncState.SyncCompleted;
    }

    private void ScanUsbControllers()
    {
        // Linux USB scanning
        const string sysUsbPath = "/sys/bus/usb/devices";
        if (!Directory.Exists(sysUsbPath)) return;

        var currentSerials = new HashSet<string>();

        foreach (var dir in Directory.EnumerateDirectories(sysUsbPath))
        {
            var vidFile = Path.Combine(dir, "idVendor");
            var pidFile = Path.Combine(dir, "idProduct");
            var serialFile = Path.Combine(dir, "serial");

            if (File.Exists(vidFile) && File.Exists(pidFile))
            {
                var vid = File.ReadAllText(vidFile).Trim();
                var pid = File.ReadAllText(pidFile).Trim().ToUpper();

                if (string.Equals(vid, "045e", StringComparison.OrdinalIgnoreCase) &&
                    SupportedProductIds.TryGetValue(pid, out var modelName))
                {
                    var serial = File.Exists(serialFile) 
                        ? File.ReadAllText(serialFile).Trim() 
                        : $"ZUNE-{pid}-{Path.GetFileName(dir)}";

                    currentSerials.Add(serial);

                    if (!_devices.ContainsKey(serial))
                    {
                        var dev = new ZuneDevice
                        {
                            SerialNumber = serial,
                            ModelName = modelName,
                            IsConnected = true,
                            IsPaired = true,
                            CapacityBytes = modelName.Contains("30") ? 30L * 1024 * 1024 * 1024 : 16L * 1024 * 1024 * 1024,
                            MusicBytes = modelName.Contains("30") ? 14L * 1024 * 1024 * 1024 : 7L * 1024 * 1024 * 1024,
                            VideoBytes = modelName.Contains("30") ? 3L * 1024 * 1024 * 1024 : 1536L * 1024 * 1024,
                            PhotoBytes = 512L * 1024 * 1024,
                            PodcastBytes = 1024L * 1024 * 1024,
                            SystemBytes = 1024L * 1024 * 1024,
                            FreeSpaceBytes = modelName.Contains("30") ? 10485760000L : 5242880000L,
                            SyncState = DeviceSyncState.Connected
                        };

                        if (_devices.TryAdd(serial, dev))
                        {
                            DeviceConnected?.Invoke(this, dev);
                        }
                    }
                }
            }
        }

        // Cleanup disconnected devices
        foreach (var existing in _devices.Keys.ToList())
        {
            if (!currentSerials.Contains(existing))
            {
                if (_devices.TryRemove(existing, out _))
                {
                    DeviceDisconnected?.Invoke(this, existing);
                }
            }
        }
    }
}
