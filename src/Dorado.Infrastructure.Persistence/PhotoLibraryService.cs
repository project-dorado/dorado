using Microsoft.EntityFrameworkCore;
using Dorado.Application.Interfaces;
using Dorado.Domain.Models;

namespace Dorado.Infrastructure.Persistence;

/// <summary>
/// SQLite-backed photo library with recursive folder scanning (PHOTOLIBRARY parity).
/// </summary>
public sealed class PhotoLibraryService : IPhotoLibraryService
{
    private static readonly HashSet<string> PhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"
    };

    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public PhotoLibraryService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<Photo>> GetAllPhotosAsync()
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var photos = await ctx.Photos.OrderBy(p => p.FolderPath).ThenBy(p => p.FilePath).ToListAsync();
        return photos;
    }

    public async Task ScanDirectoryAsync(string directoryPath, IProgress<double>? progress = null)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        var files = Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
            .Where(f => PhotoExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var existingPaths = (await ctx.Photos.ToListAsync()).Select(p => p.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            if (existingPaths.Contains(file))
            {
                continue;
            }

            var photo = new Photo
            {
                Title = Path.GetFileNameWithoutExtension(file),
                FilePath = file,
                FolderPath = Path.GetDirectoryName(file) ?? directoryPath,
                SizeBytes = new FileInfo(file).Length,
                TakenDate = TryGetTakenDate(file),
                AddedAtUtc = DateTime.UtcNow
            };

            ctx.Photos.Add(photo);
            added++;

            if (added % 50 == 0)
            {
                await ctx.SaveChangesAsync();
                progress?.Report(files.Count == 0 ? 1.0 : (double)i / files.Count);
            }
        }

        await ctx.SaveChangesAsync();
        progress?.Report(1.0);
    }

    public async Task<IReadOnlyList<string>> GetFoldersAsync()
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var folders = await ctx.Photos
            .Select(p => p.FolderPath)
            .Distinct()
            .OrderBy(f => f)
            .ToListAsync();
        return folders;
    }

    private static DateTime? TryGetTakenDate(string filePath)
    {
        try
        {
            // Prefer the file's creation timestamp as a lightweight "taken" approximation;
            // full EXIF parsing is deferred (TagLib# image support is format-limited).
            var info = new FileInfo(filePath);
            var candidate = info.CreationTimeUtc < info.LastWriteTimeUtc ? info.CreationTimeUtc : info.LastWriteTimeUtc;
            return candidate;
        }
        catch
        {
            return null;
        }
    }
}
