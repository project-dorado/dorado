using Dorado.Application.Interfaces;
using Dorado.Domain.Models;

namespace Dorado.Application.Services;

/// <summary>
/// Adapts a low-level <see cref="IMtpDeviceClient"/> (real libusb or in-memory virtual)
/// to the <see cref="IDeviceTransport"/> contract the sync engine consumes. Swapping the
/// client is the only change needed to move from the simulated flow to real hardware.
/// </summary>
public sealed class MtpTransport : IDeviceTransport
{
    private readonly IMtpDeviceClient _client;

    public MtpTransport(string deviceSerialNumber, IMtpDeviceClient client)
    {
        DeviceSerialNumber = deviceSerialNumber;
        _client = client;
        if (!_client.IsOpen)
        {
            _client.TryOpen(deviceSerialNumber);
        }
    }

    public string DeviceSerialNumber { get; }

    public string DeviceName => _client.DeviceName;

    public long TotalCapacityBytes => _client.StorageCapacityBytes;

    public long SystemBytes => _client.SystemBytes;

    public long UsedBytes => Math.Max(0, TotalCapacityBytes - _client.StorageFreeBytes);

    public long FreeBytes => _client.StorageFreeBytes;

    public IReadOnlyList<DeviceContentItem> GetContents() => _client.ListObjects();

    public bool TryGetItem(Guid entityId, out DeviceContentItem item) => _client.TryGetObject(entityId, out item);

    public void CopyToDevice(TransferItem item) => _client.Upload(item);

    public void RemoveFromDevice(DeviceContentItem item) => _client.Delete(item);
}
