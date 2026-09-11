namespace Dorado.Application.Interfaces;

/// <summary>
/// Supplies artist-to-artist relationships (collaborators, band members, long-time
/// collaborators) from an external knowledge base. Used by Mixview to enrich the
/// local genre-affinity satellites; optional and failure-tolerant — a null/empty
/// result leaves the local constellation intact.
/// </summary>
public interface IArtistRelationshipService
{
    /// <summary>Names of artists related to <paramref name="artistName"/>, best-effort and capped.</summary>
    Task<IReadOnlyList<string>> GetRelatedArtistsAsync(
        string artistName,
        int limit = 8,
        CancellationToken cancellationToken = default);
}
