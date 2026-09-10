using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Dorado.Application.Interfaces;
using Dorado.Domain.Models;

namespace Dorado.Infrastructure.Persistence;

/// <summary>
/// SQLite persistence for device sync groups (one per device serial, plus guest sessions).
/// </summary>
public sealed class SyncGroupService : ISyncGroupService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public SyncGroupService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<SyncGroup?> GetForDeviceAsync(string deviceSerialNumber)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var group = await ctx.SyncGroups
            .FirstOrDefaultAsync(g => g.DeviceSerialNumber == deviceSerialNumber && !g.IsGuestSession);
        return group;
    }

    public async Task SaveAsync(SyncGroup group)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var existing = await ctx.SyncGroups.FindAsync(group.Id);
        if (existing == null)
        {
            ctx.SyncGroups.Add(group);
        }
        else
        {
            existing.DeviceSerialNumber = group.DeviceSerialNumber;
            existing.Name = group.Name;
            existing.IsGuestSession = group.IsGuestSession;
            existing.Categories = group.Categories;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await ctx.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid groupId)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var existing = await ctx.SyncGroups.FindAsync(groupId);
        if (existing != null)
        {
            ctx.SyncGroups.Remove(existing);
            await ctx.SaveChangesAsync();
        }
    }
}
