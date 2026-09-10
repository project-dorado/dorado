using Dorado.Domain.Models;

namespace Dorado.Application.Interfaces;

/// <summary>Persistence boundary for computed audio features.</summary>
public interface IAudioFeatureStore
{
    Task<IReadOnlyDictionary<Guid, AudioFeatures>> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AudioFeatures features, CancellationToken cancellationToken = default);
}
