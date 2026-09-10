using Dorado.Domain.Models;

namespace Dorado.Application.Interfaces;

/// <summary>
/// Transport boundary to a Zune device's content store (MTPZ semantics).
/// The simulated implementation powers the full UI flow today; a real MtpTransport
/// can implement the same contract later with zero UI changes.
/// </summary>
public interface IDeviceTransport
{
    string DeviceSerialNumber { get; }
    string DeviceName { get; }
    long TotalCapacityBytes { get; }
    long SystemBytes { get; }

    IReadOnlyList<DeviceContentItem> GetContents();

    bool TryGetItem(Guid entityId, out DeviceContentItem item);

    void CopyToDevice(TransferItem item);

    void RemoveFromDevice(DeviceContentItem item);

    long UsedBytes { get; }

    long FreeBytes { get; }
}
