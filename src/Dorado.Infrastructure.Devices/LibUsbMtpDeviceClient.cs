using Dorado.Application.Interfaces;
using Dorado.Domain.Models;

namespace Dorado.Infrastructure.Devices;

/// <summary>
/// Real-hardware MTP client. Device detection is implemented (USB probe by Zune
/// product ID); the MTPZ session layer (libusb bulk transport + auth + object store)
/// is not yet implemented because no Zune hardware is available to develop against.
/// Until it is, object queries return empty and mutations fail loudly rather than
/// silently risk data loss.
/// </summary>
public sealed class LibUsbMtpDeviceClient : IMtpDeviceClient
{
    private const string HardwareUnavailableMessage =
        "MTPZ session layer is not implemented (no Zune hardware available for development).";

    public bool IsOpen { get; private set; }

    public string DeviceName { get; private set; } = "Zune";

    public string SerialNumber { get; private set; } = string.Empty;

    public long StorageCapacityBytes { get; private set; }

    public long StorageFreeBytes { get; private set; }

    public long SystemBytes { get; private set; }

    public bool TryOpen(string serialNumber)
    {
        var device = ZuneUsbDeviceProbe.Enumerate()
            .FirstOrDefault(d => string.Equals(d.SerialNumber, serialNumber, StringComparison.OrdinalIgnoreCase)
                              || string.IsNullOrEmpty(serialNumber));

        if (device.SerialNumber is null)
        {
            return false;
        }

        SerialNumber = device.SerialNumber;
        DeviceName = device.ModelName;
        IsOpen = true;

        // Capacity is read from ZMDB once the session layer exists.
        return true;
    }

    public IReadOnlyList<DeviceContentItem> ListObjects() => Array.Empty<DeviceContentItem>();

    public bool TryGetObject(Guid entityId, out DeviceContentItem item)
    {
        item = null!;
        return false;
    }

    public void Upload(TransferItem item) => throw new NotSupportedException(HardwareUnavailableMessage);

    public void Delete(DeviceContentItem item) => throw new NotSupportedException(HardwareUnavailableMessage);

    public void Dispose() => IsOpen = false;
}
