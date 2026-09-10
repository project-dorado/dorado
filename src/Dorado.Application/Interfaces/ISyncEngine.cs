using Dorado.Application.Models;
using Dorado.Domain.Models;

namespace Dorado.Application.Interfaces;

/// <summary>
/// Computes the planned transfer set (add/remove/keep) between a sync group's rules
/// and the device's current contents — the dry-run review surface, and the script
/// the sync run executes.
/// </summary>
public interface ISyncEngine
{
    /// <summary>Returns (creating if needed) the transport for a device serial.</summary>
    IDeviceTransport GetTransport(string deviceSerialNumber, string deviceName, long capacityBytes);

    /// <summary>Builds the default sync group for a device from the persisted settings rules.</summary>
    SyncGroup BuildDefaultGroup(string deviceSerialNumber, AppSettings settings, bool isGuestSession = false);

    SyncPlan BuildPlan(SyncGroup group, SyncInput input, IDeviceTransport transport);

    /// <summary>Applies the plan to the transport, reporting per-item progress (0..1).</summary>
    Task ApplyPlanAsync(SyncPlan plan, IDeviceTransport transport, IProgress<double>? progress = null);
}

/// <summary>Library snapshot handed to the engine so it stays decoupled from persistence.</summary>
public class SyncInput
{
    public IReadOnlyList<Track> Tracks { get; set; } = Array.Empty<Track>();
    public IReadOnlyList<Video> Videos { get; set; } = Array.Empty<Video>();
    public IReadOnlyList<Photo> Photos { get; set; } = Array.Empty<Photo>();
    public IReadOnlyList<PodcastEpisode> PodcastEpisodes { get; set; } = Array.Empty<PodcastEpisode>();
}
