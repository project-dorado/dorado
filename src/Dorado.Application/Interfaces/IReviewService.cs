using Dorado.Domain.Models;

namespace Dorado.Application.Interfaces;

/// <summary>Local user-authored album reviews (a substitute for dead Zune social reviews).</summary>
public interface IReviewService
{
    Task<int> GetReviewCountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Review>> GetReviewsForAlbumAsync(Guid albumId, CancellationToken cancellationToken = default);

    Task AddReviewAsync(Review review, CancellationToken cancellationToken = default);
}
