using System.Runtime.InteropServices;

namespace Dorado.Infrastructure.Devices;

public readonly record struct ZuneUsbDevice(string SerialNumber, string ProductId, string ModelName);

/// <summary>
/// Detects physically-connected Zune devices. Linux scans <c>/sys/bus/usb/devices</c>
/// (VID <c>045e</c> = Microsoft, the known Zune product IDs); other platforms return
/// an empty set until a native backend is added.
/// </summary>
public static class ZuneUsbDeviceProbe
{
    private const string MicrosoftVendorId = "045e";

    public static IReadOnlyList<ZuneUsbDevice> Enumerate()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return Array.Empty<ZuneUsbDevice>();
        }

        const string sysUsbPath = "/sys/bus/usb/devices";
        if (!Directory.Exists(sysUsbPath))
        {
            return Array.Empty<ZuneUsbDevice>();
        }

        var devices = new List<ZuneUsbDevice>();
        foreach (var directory in Directory.EnumerateDirectories(sysUsbPath))
        {
            var vendorFile = Path.Combine(directory, "idVendor");
            var productFile = Path.Combine(directory, "idProduct");
            if (!File.Exists(vendorFile) || !File.Exists(productFile))
            {
                continue;
            }

            var vendor = File.ReadAllText(vendorFile).Trim();
            var product = File.ReadAllText(productFile).Trim().ToUpperInvariant();
            if (!string.Equals(vendor, MicrosoftVendorId, StringComparison.OrdinalIgnoreCase)
                || !ZuneDeviceSyncService.SupportedProductIds.TryGetValue(product, out var model))
            {
                continue;
            }

            var serialFile = Path.Combine(directory, "serial");
            var serial = File.Exists(serialFile)
                ? File.ReadAllText(serialFile).Trim()
                : $"ZUNE-{product}-{Path.GetFileName(directory)}";

            devices.Add(new ZuneUsbDevice(serial, product, model));
        }

        return devices;
    }
}
