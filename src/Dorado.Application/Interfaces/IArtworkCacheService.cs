namespace Dorado.Application.Interfaces;

public interface IArtworkCacheService
{
    string CacheDirectory { get; }

    Task<string?> GetOrDownloadAsync(string url, CancellationToken cancellationToken = default);
}
