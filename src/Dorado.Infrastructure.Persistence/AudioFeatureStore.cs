using Dorado.Application.Interfaces;
using Dorado.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Dorado.Infrastructure.Persistence;

public sealed class SqliteAudioFeatureStore : IAudioFeatureStore
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public SqliteAudioFeatureStore(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyDictionary<Guid, AudioFeatures>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var rows = await context.TrackAudioFeatures.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.ToDictionary(f => f.TrackId);
    }

    public async Task SaveAsync(AudioFeatures features, CancellationToken cancellationToken = default)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var existing = await context.TrackAudioFeatures.FindAsync(new object?[] { features.TrackId }, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            context.TrackAudioFeatures.Add(features);
        }
        else
        {
            context.Entry(existing).CurrentValues.SetValues(features);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
