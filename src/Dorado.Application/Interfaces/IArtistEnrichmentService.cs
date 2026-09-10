using Dorado.Application.Models;

namespace Dorado.Application.Interfaces;

public interface IArtistEnrichmentService
{
    ArtistEnrichmentSnapshot? GetCached(string artistName);

    void RequestEnrichment(string artistName);

    Task<LyricsResult?> GetLyricsAsync(string artistName, string trackTitle, TimeSpan? duration = null, CancellationToken cancellationToken = default);

    event EventHandler<ArtistEnrichmentSnapshot>? EnrichmentCompleted;
}
