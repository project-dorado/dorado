using Dorado.Application.Models;
using Dorado.Application.Interfaces;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;

namespace Dorado.Infrastructure.Devices;

/// <summary>
/// Read-only <see cref="IDeviceTransport"/> backed by the content list a phone
/// sends over the LAN sync protocol. The manifest computation only reads
/// contents and byte accounting, so copy/remove are no-ops here — the phone
/// performs the transfer against its own store.
/// </summary>
public sealed class RemoteDeviceTransport : IDeviceTransport
{
    private readonly List<DeviceContentItem> _contents;

    public RemoteDeviceTransport(
        string deviceSerialNumber,
        string deviceName,
        long totalCapacityBytes,
        long systemBytes,
        IEnumerable<SyncDeviceContentDto> contents)
    {
        DeviceSerialNumber = deviceSerialNumber;
        DeviceName = deviceName;
        TotalCapacityBytes = totalCapacityBytes;
        SystemBytes = systemBytes;
        _contents = contents.Select(ToContentItem).ToList();
    }

    public string DeviceSerialNumber { get; }

    public string DeviceName { get; }

    public long TotalCapacityBytes { get; }

    public long SystemBytes { get; }

    public long UsedBytes => _contents.Sum(c => c.SizeBytes);

    public long FreeBytes => Math.Max(0, TotalCapacityBytes - SystemBytes - UsedBytes);

    public IReadOnlyList<DeviceContentItem> GetContents() => _contents;

    public bool TryGetItem(Guid entityId, out DeviceContentItem item)
    {
        item = _contents.FirstOrDefault(c => c.EntityId == entityId)!;
        return item is not null;
    }

    public void CopyToDevice(TransferItem item)
    {
        _contents.Add(new DeviceContentItem
        {
            Category = item.Category,
            EntityId = item.EntityId,
            Title = item.Title,
            DevicePath = item.SourcePath,
            SizeBytes = item.SizeBytes,
        });
    }

    public void RemoveFromDevice(DeviceContentItem item) => _contents.RemoveAll(c => c.EntityId == item.EntityId);

    private static DeviceContentItem ToContentItem(SyncDeviceContentDto dto) => new()
    {
        Category = SyncMapping.ParseCategory(dto.Category),
        EntityId = ParseEntityId(dto.EntityId),
        Title = dto.Title,
        SizeBytes = dto.SizeBytes,
    };

    /// <summary>
    /// Phone entity ids are opaque strings (MediaStore ids); the shared domain
    /// uses <see cref="Guid"/>. Hash the string deterministically so the same
    /// device item maps to the same id on both sides.
    /// </summary>
    internal static Guid ParseEntityId(string entityId)
    {
        if (Guid.TryParse(entityId, out var guid))
        {
            return guid;
        }

        var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(entityId));
        return new Guid(bytes);
    }
}

/// <summary>Bidirectional string mapping for the sync enums.</summary>
internal static class SyncMapping
{
    public static string Category(SyncCategoryType category) => category.ToString().ToUpperInvariant();

    public static SyncCategoryType ParseCategory(string value) => value.ToUpperInvariant() switch
    {
        "MUSIC" => SyncCategoryType.Music,
        "PODCASTS" => SyncCategoryType.Podcasts,
        "VIDEOS" => SyncCategoryType.Videos,
        "PICTURES" => SyncCategoryType.Pictures,
        _ => SyncCategoryType.Music,
    };

    public static string Action(TransferAction action) => action.ToString().ToUpperInvariant();

    public static string? Rating(HeartRating rating) => rating switch
    {
        HeartRating.Favorite => "HEART",
        HeartRating.Dislike => "BROKEN",
        _ => "NONE",
    };

    public static HeartRating ParseRating(string? value) => value?.ToUpperInvariant() switch
    {
        "HEART" => HeartRating.Favorite,
        "BROKEN" => HeartRating.Dislike,
        _ => HeartRating.None,
    };
}
