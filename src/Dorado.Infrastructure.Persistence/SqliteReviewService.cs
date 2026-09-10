using Dorado.Application.Interfaces;
using Dorado.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Dorado.Infrastructure.Persistence;

public sealed class SqliteReviewService : IReviewService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public SqliteReviewService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<int> GetReviewCountAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Reviews.CountAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Review>> GetReviewsForAlbumAsync(Guid albumId, CancellationToken cancellationToken = default)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Reviews
            .Where(r => r.AlbumId == albumId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddReviewAsync(Review review, CancellationToken cancellationToken = default)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.Reviews.Add(review);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
