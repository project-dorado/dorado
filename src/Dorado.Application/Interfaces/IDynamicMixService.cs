using Dorado.Domain.Models;

namespace Dorado.Application.Interfaces;

/// <summary>Materializes auto-updating "Mixes" from the current library.</summary>
public interface IDynamicMixService
{
    /// <summary>Builds the default seedless mixes (top played, similar to favorites).</summary>
    IReadOnlyList<DynamicMix> BuildDefaultMixes();

    Task<IReadOnlyList<Track>> MaterializeAsync(
        DynamicMix mix,
        IReadOnlyList<Track> library,
        CancellationToken cancellationToken = default);
}
