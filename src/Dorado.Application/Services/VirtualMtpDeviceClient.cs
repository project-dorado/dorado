using Dorado.Application.Interfaces;
using Dorado.Domain.Models;

namespace Dorado.Application.Services;

/// <summary>
/// In-memory MTP target used to exercise <see cref="MtpTransport"/> (and the
/// <see cref="IDeviceTransport"/> contract) without hardware.
/// </summary>
public sealed class VirtualMtpDeviceClient : IMtpDeviceClient
{
    private readonly Dictionary<Guid, DeviceContentItem> _objects = new();
    private readonly object _gate = new();

    public VirtualMtpDeviceClient(string deviceName = "Zune HD 32GB", long capacityBytes = 32L * 1024 * 1024 * 1024)
    {
        DeviceName = deviceName;
        StorageCapacityBytes = capacityBytes <= 0 ? 32L * 1024 * 1024 * 1024 : capacityBytes;
        SystemBytes = (long)(StorageCapacityBytes * 0.035);
    }

    public bool IsOpen { get; private set; }

    public string DeviceName { get; }

    public long StorageCapacityBytes { get; }

    public long SystemBytes { get; }

    public long StorageFreeBytes
    {
        get
        {
            lock (_gate)
            {
                return Math.Max(0, StorageCapacityBytes - SystemBytes - _objects.Values.Sum(o => o.SizeBytes));
            }
        }
    }

    public bool TryOpen(string serialNumber)
    {
        IsOpen = true;
        return true;
    }

    public IReadOnlyList<DeviceContentItem> ListObjects()
    {
        lock (_gate)
        {
            return _objects.Values.OrderBy(o => o.Category).ThenBy(o => o.Title).ToList();
        }
    }

    public bool TryGetObject(Guid entityId, out DeviceContentItem item)
    {
        lock (_gate)
        {
            if (_objects.TryGetValue(entityId, out var found))
            {
                item = found;
                return true;
            }
        }

        item = null!;
        return false;
    }

    public void Upload(TransferItem item)
    {
        lock (_gate)
        {
            _objects[item.EntityId] = new DeviceContentItem
            {
                Category = item.Category,
                EntityId = item.EntityId,
                Title = item.Title,
                DevicePath = $"\\Content\\{item.Category}\\{item.EntityId:N}",
                SizeBytes = item.SizeBytes,
                AddedAtUtc = DateTime.UtcNow
            };
        }
    }

    public void Delete(DeviceContentItem item)
    {
        lock (_gate)
        {
            _objects.Remove(item.EntityId);
        }
    }

    public void Dispose()
    {
        IsOpen = false;
    }
}
