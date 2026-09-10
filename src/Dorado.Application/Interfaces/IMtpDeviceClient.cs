using Dorado.Domain.Models;

namespace Dorado.Application.Interfaces;

/// <summary>
/// Low-level MTP/MTPZ session boundary. <see cref="MtpTransport"/> adapts this to the
/// <see cref="IDeviceTransport"/> contract. A real libusb-backed client and an
/// in-memory virtual client both implement it, so the transport is testable without
/// hardware.
/// </summary>
public interface IMtpDeviceClient : IDisposable
{
    bool IsOpen { get; }

    string DeviceName { get; }

    long StorageCapacityBytes { get; }

    long StorageFreeBytes { get; }

    long SystemBytes { get; }

    bool TryOpen(string serialNumber);

    IReadOnlyList<DeviceContentItem> ListObjects();

    bool TryGetObject(Guid entityId, out DeviceContentItem item);

    void Upload(TransferItem item);

    void Delete(DeviceContentItem item);
}
