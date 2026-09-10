using System.Globalization;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;

namespace Dorado.Application.Services;

/// <summary>
/// The sync-group engine (SchemaSyncGroup / DetailsBackedSchemaSyncGroup parity):
/// turns Settings rules into a planned transfer set (add / remove / keep) against the
/// device's current contents, then applies that plan to the transport.
/// Guest sessions are add-only and never remove device content.
/// </summary>
public class SyncEngine : ISyncEngine
{
    private readonly Func<string, IDeviceTransport>? _transportFactory;
    private readonly Dictionary<string, IDeviceTransport> _transports = new(StringComparer.OrdinalIgnoreCase);

    public SyncEngine(Func<string, IDeviceTransport>? transportFactory = null)
    {
        _transportFactory = transportFactory;
    }

    public IDeviceTransport GetTransport(string deviceSerialNumber, string deviceName, long capacityBytes)
    {
        lock (_transports)
        {
            if (!_transports.TryGetValue(deviceSerialNumber, out var transport))
            {
                transport = _transportFactory != null
                    ? _transportFactory(deviceSerialNumber)
                    : new SimulatedDeviceTransport(deviceSerialNumber, deviceName, capacityBytes);
                _transports[deviceSerialNumber] = transport;
            }

            return transport;
        }
    }

    public SyncGroup BuildDefaultGroup(string deviceSerialNumber, AppSettings settings, bool isGuestSession = false)
    {
        var group = new SyncGroup
        {
            DeviceSerialNumber = deviceSerialNumber,
            Name = isGuestSession ? $"Guest Session — {deviceSerialNumber}" : $"Sync Group — {deviceSerialNumber}",
            IsGuestSession = isGuestSession
        };

        // Music rule (mirrors Settings > Device > Music Synchronization Rule)
        var musicMode = settings.MusicSyncRule.Contains("Manual", StringComparison.OrdinalIgnoreCase) ? SyncMode.Manual
            : settings.MusicSyncRule.Contains("Selected", StringComparison.OrdinalIgnoreCase) ? SyncMode.SelectedItems
            : SyncMode.Automatic;
        group.Categories.Add(new SyncCategoryRule { Category = SyncCategoryType.Music, Mode = musicMode, RuleText = settings.MusicSyncRule, PreferFavorites = musicMode == SyncMode.SelectedItems });

        // Podcast rule
        var podcastMode = SyncMode.SelectedItems;
        int? podcastCount = ParseTrailingCount(settings.PodcastSyncRule);
        if (settings.PodcastSyncRule.Contains("Manual", StringComparison.OrdinalIgnoreCase))
        {
            podcastMode = SyncMode.Manual;
        }
        else if (settings.PodcastSyncRule.StartsWith("All", StringComparison.OrdinalIgnoreCase))
        {
            podcastMode = SyncMode.Automatic;
            podcastCount = null;
        }
        group.Categories.Add(new SyncCategoryRule { Category = SyncCategoryType.Podcasts, Mode = podcastMode, RuleText = settings.PodcastSyncRule, NewestCount = podcastMode == SyncMode.SelectedItems ? podcastCount : null });

        // Media rules (videos / pictures share the MediaSyncRules list)
        var mediaMode = settings.VideoSyncRule.Contains("Manual", StringComparison.OrdinalIgnoreCase) || settings.VideoSyncRule.Contains("Nothing", StringComparison.OrdinalIgnoreCase)
            ? SyncMode.Manual
            : settings.VideoSyncRule.StartsWith("All", StringComparison.OrdinalIgnoreCase) ? SyncMode.Automatic
            : SyncMode.SelectedItems;
        group.Categories.Add(new SyncCategoryRule { Category = SyncCategoryType.Videos, Mode = mediaMode, RuleText = settings.VideoSyncRule, NewestCount = mediaMode == SyncMode.SelectedItems ? ParseTrailingCount(settings.VideoSyncRule) : null });
        group.Categories.Add(new SyncCategoryRule { Category = SyncCategoryType.Pictures, Mode = mediaMode, RuleText = settings.PicturesSyncRule, NewestCount = mediaMode == SyncMode.SelectedItems ? ParseTrailingCount(settings.PicturesSyncRule) : null });

        return group;
    }

    public SyncPlan BuildPlan(SyncGroup group, SyncInput input, IDeviceTransport transport)
    {
        var plan = new SyncPlan
        {
            DeviceSerialNumber = group.DeviceSerialNumber,
            IsGuestSession = group.IsGuestSession
        };

        var desired = new Dictionary<Guid, TransferItem>();

        foreach (var rule in group.Categories)
        {
            foreach (var item in SelectDesired(rule, input))
            {
                desired[item.EntityId] = item;
            }
        }

        var deviceContents = transport.GetContents().ToDictionary(c => c.EntityId, c => c);
        var managedCategories = group.Categories
            .Where(r => r.Mode != SyncMode.Manual)
            .Select(r => r.Category)
            .ToHashSet();

        // Removals are computed first so their freed space can back later adds.
        // Guest sessions never remove anything.
        if (!group.IsGuestSession)
        {
            foreach (var content in deviceContents.Values)
            {
                if (!desired.ContainsKey(content.EntityId) && managedCategories.Contains(content.Category))
                {
                    plan.Items.Add(new TransferItem
                    {
                        Action = TransferAction.Remove,
                        Category = content.Category,
                        EntityId = content.EntityId,
                        Title = content.Title,
                        Detail = "No longer in sync group"
                    });
                }
            }
        }

        // Adds + keeps, truncated to projected free space (Zune skips content that cannot fit).
        var projectedUsed = deviceContents.Values.Sum(c => c.SizeBytes) - plan.TotalRemoveBytes;
        var freeBytes = transport.TotalCapacityBytes - transport.SystemBytes - projectedUsed;

        foreach (var (entityId, item) in desired)
        {
            if (deviceContents.ContainsKey(entityId))
            {
                plan.Items.Add(new TransferItem
                {
                    Action = TransferAction.Keep,
                    Category = item.Category,
                    EntityId = entityId,
                    Title = item.Title,
                    SizeBytes = item.SizeBytes
                });
            }
            else if (item.SizeBytes <= freeBytes)
            {
                plan.Items.Add(item);
                freeBytes -= item.SizeBytes;
            }
        }

        return plan;
    }

    private static IEnumerable<TransferItem> SelectDesired(SyncCategoryRule rule, SyncInput input)
    {
        if (rule.Mode == SyncMode.Manual)
        {
            yield break;
        }

        switch (rule.Category)
        {
            case SyncCategoryType.Music:
            {
                var candidates = rule.PreferFavorites
                    ? input.Tracks.Where(t => t.Rating == HeartRating.Favorite)
                    : input.Tracks;
                foreach (var track in LimitIfConfigured(candidates.OrderBy(t => t.Title), rule))
                {
                    yield return new TransferItem
                    {
                        Action = TransferAction.Add,
                        Category = SyncCategoryType.Music,
                        EntityId = track.Id,
                        Title = track.Title,
                        SourcePath = track.FilePath,
                        SizeBytes = EstimateAudioBytes(track.Duration),
                        Detail = $"{track.ArtistName} — {track.AlbumTitle}"
                    };
                }

                break;
            }

            case SyncCategoryType.Podcasts:
            {
                var candidates = input.PodcastEpisodes.Where(e => !e.IsPlayed);
                foreach (var episode in LimitIfConfigured(candidates.OrderByDescending(e => e.PublishedAtUtc), rule))
                {
                    yield return new TransferItem
                    {
                        Action = TransferAction.Add,
                        Category = SyncCategoryType.Podcasts,
                        EntityId = episode.Id,
                        Title = episode.Title,
                        SourcePath = episode.AudioUrl,
                        SizeBytes = EstimateAudioBytes(episode.Duration),
                        Detail = episode.SeriesTitle
                    };
                }

                break;
            }

            case SyncCategoryType.Videos:
            {
                foreach (var video in LimitIfConfigured(input.Videos.OrderByDescending(v => v.AddedAtUtc), rule))
                {
                    yield return new TransferItem
                    {
                        Action = TransferAction.Add,
                        Category = SyncCategoryType.Videos,
                        EntityId = video.Id,
                        Title = video.Title,
                        SourcePath = video.FilePath,
                        SizeBytes = video.SizeBytes
                    };
                }

                break;
            }

            case SyncCategoryType.Pictures:
            {
                foreach (var photo in LimitIfConfigured(input.Photos.OrderByDescending(p => p.AddedAtUtc), rule))
                {
                    yield return new TransferItem
                    {
                        Action = TransferAction.Add,
                        Category = SyncCategoryType.Pictures,
                        EntityId = photo.Id,
                        Title = photo.Title,
                        SourcePath = photo.FilePath,
                        SizeBytes = photo.SizeBytes,
                        Detail = photo.FolderPath
                    };
                }

                break;
            }
        }
    }

    private static IEnumerable<T> LimitIfConfigured<T>(IEnumerable<T> ordered, SyncCategoryRule rule)
        => rule.NewestCount.HasValue ? ordered.Take(rule.NewestCount.Value) : ordered;

    /// <summary>Audio size estimate at ~128 kbps when the exact file size is unknown.</summary>
    private static long EstimateAudioBytes(TimeSpan duration)
        => duration.TotalSeconds <= 0 ? 0 : (long)(duration.TotalSeconds * 16_000);

    private static int? ParseTrailingCount(string ruleText)
    {
        if (string.IsNullOrWhiteSpace(ruleText))
        {
            return null;
        }

        var digits = new string(ruleText.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : null;
    }

    public async Task ApplyPlanAsync(SyncPlan plan, IDeviceTransport transport, IProgress<double>? progress = null)
    {
        // Zune deletes stale content before copying new content so removals free
        // space for the adds that follow.
        var items = plan.Items.Where(i => i.Action == TransferAction.Remove)
            .Concat(plan.Items.Where(i => i.Action == TransferAction.Add))
            .ToList();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            switch (item.Action)
            {
                case TransferAction.Add:
                    transport.CopyToDevice(item);
                    break;
                case TransferAction.Remove:
                    if (transport.TryGetItem(item.EntityId, out var existing))
                    {
                        transport.RemoveFromDevice(existing);
                    }

                    break;
            }

            progress?.Report(items.Count == 0 ? 1.0 : (double)(i + 1) / items.Count);
            await Task.Yield();
        }

        progress?.Report(1.0);
    }
}
