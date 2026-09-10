using Dorado.Application.Interfaces;
using Dorado.Domain.Models;

namespace Dorado.Application.Services;

/// <summary>
/// In-memory device content store with plausible defaults (32 GB Zune HD, some pre-synced
/// content). Powers the entire sync UI flow today; a future MtpTransport implements
/// the same contract against real hardware.
/// </summary>
public sealed class SimulatedDeviceTransport : IDeviceTransport
{
    private readonly Dictionary<Guid, DeviceContentItem> _contents = new();
    private readonly object _gate = new();

    public string DeviceSerialNumber { get; }

    public string DeviceName { get; }

    public long TotalCapacityBytes { get; }

    public long SystemBytes { get; }

    public SimulatedDeviceTransport(string deviceSerialNumber, string deviceName, long capacityBytes)
    {
        DeviceSerialNumber = deviceSerialNumber;
        DeviceName = string.IsNullOrWhiteSpace(deviceName) ? "Zune HD 32GB" : deviceName;
        TotalCapacityBytes = capacityBytes <= 0 ? 32L * 1024 * 1024 * 1024 : capacityBytes;
        SystemBytes = (long)(TotalCapacityBytes * 0.035); // ~1.1 GB firmware + OS partition

        if (TotalCapacityBytes >= 4L * 1024 * 1024 * 1024)
        {
            SeedDefaultContent();
        }
    }

    public long UsedBytes
    {
        get
        {
            lock (_gate)
            {
                return SystemBytes + _contents.Values.Sum(c => c.SizeBytes);
            }
        }
    }

    public long FreeBytes
    {
        get
        {
            lock (_gate)
            {
                return Math.Max(0, TotalCapacityBytes - UsedBytes);
            }
        }
    }

    public IReadOnlyList<DeviceContentItem> GetContents()
    {
        lock (_gate)
        {
            return _contents.Values.OrderBy(c => c.Category).ThenBy(c => c.Title).ToList();
        }
    }

    public bool TryGetItem(Guid entityId, out DeviceContentItem item)
    {
        lock (_gate)
        {
            if (_contents.TryGetValue(entityId, out var found))
            {
                item = found;
                return true;
            }
        }

        item = null!;
        return false;
    }

    public void CopyToDevice(TransferItem item)
    {
        lock (_gate)
        {
            _contents[item.EntityId] = new DeviceContentItem
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

    public void RemoveFromDevice(DeviceContentItem item)
    {
        lock (_gate)
        {
            _contents.Remove(item.EntityId);
        }
    }

    /// <summary>
    /// Seeds a plausible pre-existing device state so Keep/Remove actions are visible:
    /// two stale music items, a podcast, a video and a photo.
    /// </summary>
    private void SeedDefaultContent()
    {
        var staleMusic = new[]
        {
            new DeviceContentItem { Category = SyncCategoryType.Music, EntityId = Guid.NewGuid(), Title = "Ice Ice Baby", SizeBytes = 4_800_000, DevicePath = "\\Content\\Music\\legacy1", AddedAtUtc = DateTime.UtcNow.AddDays(-400) },
            new DeviceContentItem { Category = SyncCategoryType.Music, EntityId = Guid.NewGuid(), Title = "Macarena", SizeBytes = 4_100_000, DevicePath = "\\Content\\Music\\legacy2", AddedAtUtc = DateTime.UtcNow.AddDays(-400) }
        };
        var podcast = new DeviceContentItem { Category = SyncCategoryType.Podcasts, EntityId = Guid.NewGuid(), Title = "KEXP — Music That Matters 512", SizeBytes = 58_000_000, DevicePath = "\\Content\\Podcasts\\kexp512", AddedAtUtc = DateTime.UtcNow.AddDays(-9) };
        var video = new DeviceContentItem { Category = SyncCategoryType.Videos, EntityId = Guid.NewGuid(), Title = "Zune Marketing Reel 2006", SizeBytes = 412_000_000, DevicePath = "\\Content\\Videos\\reel", AddedAtUtc = DateTime.UtcNow.AddDays(-120) };
        var photo = new DeviceContentItem { Category = SyncCategoryType.Pictures, EntityId = Guid.NewGuid(), Title = "Wallpaper", SizeBytes = 2_400_000, DevicePath = "\\Content\\Pictures\\wallpaper", AddedAtUtc = DateTime.UtcNow.AddDays(-120) };

        foreach (var item in staleMusic.Concat(new[] { podcast, video, photo }))
        {
            _contents[item.EntityId] = item;
        }
    }
}
