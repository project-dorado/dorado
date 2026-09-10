using System;
using System.Collections.Generic;
using System.Linq;

namespace Dorado.Domain.Models;

public enum SyncMode
{
    /// <summary>Everything matching the category flows to the device automatically.</summary>
    Automatic,
    /// <summary>A constrained selection (favorites/pinned/newest-N) flows automatically.</summary>
    SelectedItems,
    /// <summary>Nothing flows automatically; only explicit drag-and-drop/queue actions.</summary>
    Manual
}

public enum SyncCategoryType
{
    Music,
    Podcasts,
    Videos,
    Pictures
}

public class SyncCategoryRule
{
    public SyncCategoryType Category { get; set; }
    public SyncMode Mode { get; set; }
    /// <summary>Raw rule text mirrored from Settings (e.g. "3 Newest Episodes").</summary>
    public string RuleText { get; set; } = string.Empty;
    /// <summary>Constrains automatic selection to the newest N items when applicable.</summary>
    public int? NewestCount { get; set; }
    /// <summary>When true (SelectedItems), favorites/pinned content forms the selection.</summary>
    public bool PreferFavorites { get; set; }
}

/// <summary>
/// A persisted sync group per device (ZMDB SchemaSyncGroup parity); guest sessions are
/// separate, add-only groups.
/// </summary>
public class SyncGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DeviceSerialNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsGuestSession { get; set; }
    public List<SyncCategoryRule> Categories { get; set; } = new();
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public enum TransferAction
{
    Add,
    Remove,
    Keep
}

public class TransferItem
{
    public TransferAction Action { get; set; }
    public SyncCategoryType Category { get; set; }
    public Guid EntityId { get; set; }
    public string Title { get; set; } = string.Empty;
    /// <summary>PC-side source path (empty for device-side removals).</summary>
    public string SourcePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? Detail { get; set; }
}

public class DeviceContentItem
{
    public SyncCategoryType Category { get; set; }
    public Guid EntityId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DevicePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
    public long? DurationSeconds { get; set; }
    public int? PlayCount { get; set; }
}

/// <summary>
/// The computed difference between the sync group's rules and the device's current
/// contents — Zune's "what will sync" review surface.
/// </summary>
public class SyncPlan
{
    public string DeviceSerialNumber { get; set; } = string.Empty;
    public bool IsGuestSession { get; set; }
    public List<TransferItem> Items { get; set; } = new();

    public long TotalAddBytes => Items.Where(i => i.Action == TransferAction.Add).Sum(i => i.SizeBytes);
    public long TotalRemoveBytes => Items.Where(i => i.Action == TransferAction.Remove).Sum(i => i.SizeBytes);

    public int AddCount => Items.Count(i => i.Action == TransferAction.Add);
    public int RemoveCount => Items.Count(i => i.Action == TransferAction.Remove);
    public int KeepCount => Items.Count(i => i.Action == TransferAction.Keep);

    public long AddBytesFor(SyncCategoryType category)
        => Items.Where(i => i.Action == TransferAction.Add && i.Category == category).Sum(i => i.SizeBytes);

    public long RemoveBytesFor(SyncCategoryType category)
        => Items.Where(i => i.Action == TransferAction.Remove && i.Category == category).Sum(i => i.SizeBytes);
}
