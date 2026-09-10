using Microsoft.EntityFrameworkCore;
using Dorado.Application.Interfaces;
using Dorado.Application.Services;
using Dorado.Domain.Models;

namespace Dorado.Infrastructure.Persistence;

/// <summary>
/// SQLite persistence for smart (auto) playlists, with rule evaluation delegated to
/// the pure <see cref="SmartPlaylistRules"/> engine.
/// </summary>
public sealed class SmartPlaylistService : ISmartPlaylistService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public SmartPlaylistService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<SmartPlaylist>> GetAllAsync()
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var playlists = await ctx.SmartPlaylists.OrderBy(p => p.Name).ToListAsync();
        return playlists;
    }

    public async Task SaveAsync(SmartPlaylist playlist)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var existing = await ctx.SmartPlaylists.FindAsync(playlist.Id);
        if (existing == null)
        {
            ctx.SmartPlaylists.Add(playlist);
        }
        else
        {
            existing.Name = playlist.Name;
            existing.Description = playlist.Description;
            existing.Match = playlist.Match;
            existing.TrackLimit = playlist.TrackLimit;
            existing.SortField = playlist.SortField;
            existing.SortDescending = playlist.SortDescending;
            existing.Rules = playlist.Rules;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await ctx.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid playlistId)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var existing = await ctx.SmartPlaylists.FindAsync(playlistId);
        if (existing != null)
        {
            ctx.SmartPlaylists.Remove(existing);
            await ctx.SaveChangesAsync();
        }
    }

    public IReadOnlyList<Track> Evaluate(SmartPlaylist playlist, IEnumerable<Track> libraryTracks)
        => SmartPlaylistRules.Evaluate(playlist, libraryTracks);
}
