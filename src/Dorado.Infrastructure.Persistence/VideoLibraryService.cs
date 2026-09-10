using Microsoft.EntityFrameworkCore;
using Dorado.Application.Interfaces;
using Dorado.Domain.Models;

namespace Dorado.Infrastructure.Persistence;

/// <summary>
/// SQLite-backed video library with recursive folder scanning (VIDEOLIBRARY parity).
/// </summary>
public sealed class VideoLibraryService : IVideoLibraryService
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".mpg", ".mpeg", ".ts"
    };

    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public VideoLibraryService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<Video>> GetAllVideosAsync()
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var videos = await ctx.Videos.OrderBy(v => v.Title).ToListAsync();
        return videos;
    }

    public async Task ScanDirectoryAsync(string directoryPath, IProgress<double>? progress = null)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        var files = Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
            .Where(f => VideoExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        var added = 0;
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var existingPaths = (await ctx.Videos.ToListAsync()).Select(v => v.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            if (existingPaths.Contains(file))
            {
                continue;
            }

            var video = new Video
            {
                Title = Path.GetFileNameWithoutExtension(file),
                FilePath = file,
                SizeBytes = new FileInfo(file).Length,
                AddedAtUtc = DateTime.UtcNow
            };

            try
            {
                using var tagFile = TagLib.File.Create(file);
                if (tagFile.Properties?.Duration.TotalMilliseconds > 0)
                {
                    video.Duration = tagFile.Properties.Duration;
                }
            }
            catch
            {
                // Unsupported containers (e.g. mkv) simply keep a zero duration until played.
            }

            ctx.Videos.Add(video);
            added++;

            if (added % 10 == 0)
            {
                await ctx.SaveChangesAsync();
                progress?.Report(files.Count == 0 ? 1.0 : (double)i / files.Count);
            }
        }

        await ctx.SaveChangesAsync();
        progress?.Report(1.0);
    }

    public async Task MarkPlayedAsync(Guid videoId)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var video = await ctx.Videos.FindAsync(videoId);
        if (video != null)
        {
            video.PlayCount++;
            video.LastPlayedAtUtc = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
        }
    }
}
