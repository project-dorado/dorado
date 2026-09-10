using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Dorado.Application.Interfaces;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;

namespace Dorado.Infrastructure.Persistence;

public class MediaLibraryService : IMediaLibraryService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public event EventHandler? LibraryUpdated;

    public MediaLibraryService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<Track>> GetAllTracksAsync()
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        return await ctx.Tracks.OrderBy(t => t.Title).ToListAsync();
    }

    public async Task<IReadOnlyList<Album>> GetAllAlbumsAsync()
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        return await ctx.Albums.Include(a => a.Tracks).OrderBy(a => a.Title).ToListAsync();
    }

    public async Task<IReadOnlyList<Artist>> GetAllArtistsAsync()
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        return await ctx.Artists.OrderBy(a => a.Name).ToListAsync();
    }

    public async Task<IReadOnlyList<Playlist>> GetAllPlaylistsAsync()
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        return await ctx.Playlists.OrderBy(p => p.Name).ToListAsync();
    }

    public async Task<IReadOnlyList<PlayHistoryEntry>> GetRecentHistoryAsync(int count = 20)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        return await ctx.PlayHistory
            .OrderByDescending(h => h.PlayedAtUtc)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Album>> GetRecentlyAddedAlbumsAsync(int count = 12)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        return await ctx.Albums
            .OrderByDescending(a => a.Year)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Track>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetAllTracksAsync();
        }

        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var q = query.Trim().ToLower();

        return await ctx.Tracks
            .Where(t => t.Title.ToLower().Contains(q) ||
                        t.ArtistName.ToLower().Contains(q) ||
                        t.AlbumTitle.ToLower().Contains(q) ||
                        t.Genre.ToLower().Contains(q))
            .OrderBy(t => t.Title)
            .Take(100)
            .ToListAsync();
    }

    public async Task SetTrackRatingAsync(Guid trackId, HeartRating rating)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var track = await ctx.Tracks.FindAsync(trackId);
        if (track != null)
        {
            track.Rating = rating;
            await ctx.SaveChangesAsync();
            LibraryUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task SetAlbumArtworkAsync(Guid albumId, string? artworkUri)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var album = await ctx.Albums.FindAsync(albumId);
        if (album != null)
        {
            album.ArtworkUri = artworkUri;
            await ctx.SaveChangesAsync();
        }
    }

    public async Task SetArtistMetadataAsync(string artistName, string? biography, string? thumbnailUri, string? backgroundImageUri, string? musicBrainzId)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var artist = await ctx.Artists.FirstOrDefaultAsync(a => a.Name == artistName);
        if (artist != null)
        {
            if (!string.IsNullOrWhiteSpace(biography))
            {
                artist.Biography = biography;
            }

            if (!string.IsNullOrWhiteSpace(thumbnailUri))
            {
                artist.ThumbnailUri = thumbnailUri;
            }

            if (!string.IsNullOrWhiteSpace(backgroundImageUri))
            {
                artist.BackgroundImageUri = backgroundImageUri;
            }

            if (!string.IsNullOrWhiteSpace(musicBrainzId))
            {
                artist.MusicBrainzId = musicBrainzId;
            }

            await ctx.SaveChangesAsync();
        }
    }

    public async Task ClearDemoDataAsync()
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        
        // Remove tracks with null or empty FilePath (placeholders)
        var demoTracks = await ctx.Tracks.Where(t => string.IsNullOrEmpty(t.FilePath)).ToListAsync();
        if (demoTracks.Count > 0)
        {
            var demoTrackIds = demoTracks.Select(t => t.Id).ToHashSet();
            
            // Remove demo play history referencing these tracks
            var demoHistory = await ctx.PlayHistory.Where(h => demoTrackIds.Contains(h.TrackId)).ToListAsync();
            ctx.PlayHistory.RemoveRange(demoHistory);

            ctx.Tracks.RemoveRange(demoTracks);
        }

        // Clean up empty demo albums that have no real tracks
        var emptyAlbums = await ctx.Albums.Include(a => a.Tracks)
            .Where(a => !a.Tracks.Any(t => !string.IsNullOrEmpty(t.FilePath)))
            .ToListAsync();
        ctx.Albums.RemoveRange(emptyAlbums);

        // Clean up empty demo artists that have no remaining albums
        var emptyArtists = await ctx.Artists
            .Where(a => !ctx.Tracks.Any(t => t.ArtistId == a.Id && !string.IsNullOrEmpty(t.FilePath)))
            .ToListAsync();
        ctx.Artists.RemoveRange(emptyArtists);

        await ctx.SaveChangesAsync();
        LibraryUpdated?.Invoke(this, EventArgs.Empty);
    }

    public async Task ScanDirectoryAsync(string directoryPath, IProgress<double>? progress = null)
    {
        if (!Directory.Exists(directoryPath)) return;

        var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".flac", ".m4a", ".ogg", ".wma", ".wav", ".aac", ".opus"
        };

        var files = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.AllDirectories)
            .Where(f => supportedExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        if (files.Count == 0) return;

        await using var ctx = await _contextFactory.CreateDbContextAsync();

        // Check if library currently only contains placeholder demo tracks
        var hasRealTracks = await ctx.Tracks.AnyAsync(t => !string.IsNullOrEmpty(t.FilePath));
        if (!hasRealTracks)
        {
            // Clear placeholder demo tracks when scanning the user's first real library
            var demoTracks = await ctx.Tracks.Where(t => string.IsNullOrEmpty(t.FilePath)).ToListAsync();
            var demoAlbums = await ctx.Albums.ToListAsync();
            var demoArtists = await ctx.Artists.ToListAsync();
            var demoHistory = await ctx.PlayHistory.ToListAsync();

            ctx.PlayHistory.RemoveRange(demoHistory);
            ctx.Tracks.RemoveRange(demoTracks);
            ctx.Albums.RemoveRange(demoAlbums);
            ctx.Artists.RemoveRange(demoArtists);
            await ctx.SaveChangesAsync();
        }

        // Cache existing artists and albums to minimize DB roundtrips
        var existingArtists = (await ctx.Artists.ToListAsync())
            .GroupBy(a => a.Name.Trim().ToLower())
            .ToDictionary(g => g.Key, g => g.First());

        var existingAlbums = (await ctx.Albums.ToListAsync())
            .GroupBy(a => $"{a.ArtistName.Trim().ToLower()}||{a.Title.Trim().ToLower()}")
            .ToDictionary(g => g.Key, g => g.First());

        var existingFilePaths = (await ctx.Tracks
            .Where(t => !string.IsNullOrEmpty(t.FilePath))
            .Select(t => t.FilePath)
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int total = files.Count;
        int current = 0;

        foreach (var file in files)
        {
            current++;
            progress?.Report((double)current / total);

            if (existingFilePaths.Contains(file)) continue;

            string title = Path.GetFileNameWithoutExtension(file);
            string artistName = "Unknown Artist";
            string albumTitle = "Unknown Album";
            int year = DateTime.UtcNow.Year;
            string genre = "Music";
            int trackNumber = 1;
            TimeSpan duration = TimeSpan.FromMinutes(3);

            try
            {
                using var tagFile = TagLib.File.Create(file);
                if (tagFile.Tag != null)
                {
                    if (!string.IsNullOrWhiteSpace(tagFile.Tag.Title))
                        title = tagFile.Tag.Title.Trim();

                    if (!string.IsNullOrWhiteSpace(tagFile.Tag.FirstPerformer))
                        artistName = tagFile.Tag.FirstPerformer.Trim();
                    else if (!string.IsNullOrWhiteSpace(tagFile.Tag.FirstAlbumArtist))
                        artistName = tagFile.Tag.FirstAlbumArtist.Trim();

                    if (!string.IsNullOrWhiteSpace(tagFile.Tag.Album))
                        albumTitle = tagFile.Tag.Album.Trim();

                    if (tagFile.Tag.Year > 0)
                        year = (int)tagFile.Tag.Year;

                    if (!string.IsNullOrWhiteSpace(tagFile.Tag.FirstGenre))
                        genre = tagFile.Tag.FirstGenre.Trim();

                    if (tagFile.Tag.Track > 0)
                        trackNumber = (int)tagFile.Tag.Track;
                }

                if (tagFile.Properties != null && tagFile.Properties.Duration > TimeSpan.Zero)
                {
                    duration = tagFile.Properties.Duration;
                }
            }
            catch
            {
                // Fallback: try parsing "Artist - Title.mp3" or "01 Title.mp3" from filename
                var baseName = Path.GetFileNameWithoutExtension(file);
                var parts = baseName.Split('-', 2);
                if (parts.Length == 2)
                {
                    artistName = parts[0].Trim();
                    title = parts[1].Trim();
                }
            }

            // 1. Ensure Artist exists
            var artistKey = artistName.Trim().ToLower();
            if (!existingArtists.TryGetValue(artistKey, out var artist))
            {
                artist = new Artist
                {
                    Name = artistName,
                    SortName = artistName
                };
                ctx.Artists.Add(artist);
                existingArtists[artistKey] = artist;
            }

            // 2. Ensure Album exists
            var albumKey = $"{artistName.Trim().ToLower()}||{albumTitle.Trim().ToLower()}";
            if (!existingAlbums.TryGetValue(albumKey, out var album))
            {
                album = new Album
                {
                    Title = albumTitle,
                    ArtistId = artist.Id,
                    ArtistName = artist.Name,
                    Year = year,
                    Genre = genre
                };
                ctx.Albums.Add(album);
                existingAlbums[albumKey] = album;
            }

            // 3. Create Track
            var track = new Track
            {
                Title = title,
                FilePath = file,
                ArtistId = artist.Id,
                ArtistName = artist.Name,
                AlbumId = album.Id,
                AlbumTitle = album.Title,
                TrackNumber = trackNumber,
                Duration = duration,
                Year = year,
                Genre = genre
            };

            ctx.Tracks.Add(track);
            existingFilePaths.Add(file);

            // Save in batches of 50 to maintain performance
            if (current % 50 == 0)
            {
                await ctx.SaveChangesAsync();
            }
        }

        await ctx.SaveChangesAsync();
        StartDirectoryWatcher(directoryPath);
        LibraryUpdated?.Invoke(this, EventArgs.Empty);
    }

    public async Task<Playlist> CreatePlaylistAsync(string name, string? description = null)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var playlist = new Playlist
        {
            Name = string.IsNullOrWhiteSpace(name) ? "New Playlist" : name.Trim(),
            Description = description,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            TrackIds = new List<Guid>()
        };

        ctx.Playlists.Add(playlist);
        await ctx.SaveChangesAsync();

        LibraryUpdated?.Invoke(this, EventArgs.Empty);
        return playlist;
    }

    public async Task DeletePlaylistAsync(Guid playlistId)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var playlist = await ctx.Playlists.FindAsync(playlistId);
        if (playlist != null)
        {
            ctx.Playlists.Remove(playlist);
            await ctx.SaveChangesAsync();
            LibraryUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task AddTrackToPlaylistAsync(Guid playlistId, Guid trackId)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var playlist = await ctx.Playlists.FindAsync(playlistId);
        if (playlist != null && !playlist.TrackIds.Contains(trackId))
        {
            var updated = new List<Guid>(playlist.TrackIds) { trackId };
            playlist.TrackIds = updated;
            playlist.UpdatedAtUtc = DateTime.UtcNow;
            ctx.Entry(playlist).Property(p => p.TrackIds).IsModified = true;
            await ctx.SaveChangesAsync();
            LibraryUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task RemoveTrackFromPlaylistAsync(Guid playlistId, Guid trackId)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var playlist = await ctx.Playlists.FindAsync(playlistId);
        if (playlist != null && playlist.TrackIds.Contains(trackId))
        {
            var updated = new List<Guid>(playlist.TrackIds);
            updated.Remove(trackId);
            playlist.TrackIds = updated;
            playlist.UpdatedAtUtc = DateTime.UtcNow;
            ctx.Entry(playlist).Property(p => p.TrackIds).IsModified = true;
            await ctx.SaveChangesAsync();
            LibraryUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task<IReadOnlyList<Track>> GetPlaylistTracksAsync(Guid playlistId)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var playlist = await ctx.Playlists.FindAsync(playlistId);
        if (playlist == null || playlist.TrackIds.Count == 0)
        {
            return Array.Empty<Track>();
        }

        var trackIds = playlist.TrackIds.ToHashSet();
        var tracks = await ctx.Tracks
            .Where(t => trackIds.Contains(t.Id))
            .ToListAsync();

        // Maintain playlist track order
        var trackMap = tracks.ToDictionary(t => t.Id);
        var ordered = new List<Track>();
        foreach (var id in playlist.TrackIds)
        {
            if (trackMap.TryGetValue(id, out var trk))
            {
                ordered.Add(trk);
            }
        }
        return ordered;
    }

    public async Task ExportPlaylistToZplAsync(Guid playlistId, string targetFilePath)
    {
        var tracks = await GetPlaylistTracksAsync(playlistId);
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var playlist = await ctx.Playlists.FindAsync(playlistId);
        var playlistName = playlist?.Name ?? "Playlist";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?zune-album-playlist version=\"2.0\"?>");
        sb.AppendLine("<smil>");
        sb.AppendLine("  <head>");
        sb.AppendLine("    <meta name=\"Generator\" content=\"Dorado v0.1.0\" />");
        sb.AppendLine($"    <meta name=\"ItemCount\" content=\"{tracks.Count}\" />");
        sb.AppendLine($"    <title>{System.Security.SecurityElement.Escape(playlistName)}</title>");
        sb.AppendLine("  </head>");
        sb.AppendLine("  <body>");
        sb.AppendLine("    <seq>");
        foreach (var trk in tracks)
        {
            var src = System.Security.SecurityElement.Escape(trk.FilePath ?? string.Empty);
            var albumTitle = System.Security.SecurityElement.Escape(trk.AlbumTitle);
            var artistName = System.Security.SecurityElement.Escape(trk.ArtistName);
            var title = System.Security.SecurityElement.Escape(trk.Title);
            var durationMs = (long)trk.Duration.TotalMilliseconds;
            sb.AppendLine($"      <media src=\"{src}\" albumTitle=\"{albumTitle}\" albumArtist=\"{artistName}\" trackTitle=\"{title}\" trackArtist=\"{artistName}\" duration=\"{durationMs}\" />");
        }
        sb.AppendLine("    </seq>");
        sb.AppendLine("  </body>");
        sb.AppendLine("</smil>");

        var targetDir = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }
        await File.WriteAllTextAsync(targetFilePath, sb.ToString(), System.Text.Encoding.UTF8);
    }

    public async Task UpdateTrackMetadataAsync(Guid trackId, string title, string artistName, string albumTitle, int? year, string genre, int trackNumber, int discNumber)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var track = await ctx.Tracks.FindAsync(trackId);
        if (track == null) return;

        // 1. Write tags back to physical file if present
        if (!string.IsNullOrWhiteSpace(track.FilePath) && File.Exists(track.FilePath))
        {
            try
            {
                using var tagFile = TagLib.File.Create(track.FilePath);
                tagFile.Tag.Title = title;
                tagFile.Tag.Performers = new[] { artistName };
                tagFile.Tag.Album = albumTitle;
                if (year.HasValue && year.Value > 0)
                {
                    tagFile.Tag.Year = (uint)year.Value;
                }
                if (!string.IsNullOrWhiteSpace(genre))
                {
                    tagFile.Tag.Genres = new[] { genre };
                }
                if (trackNumber > 0)
                {
                    tagFile.Tag.Track = (uint)trackNumber;
                }
                if (discNumber > 0)
                {
                    tagFile.Tag.Disc = (uint)discNumber;
                }
                tagFile.Save();
            }
            catch
            {
                // Fallback: Proceed to update database even if file tag writing has permission restriction
            }
        }

        // 2. Resolve/update Artist entity
        var artist = await ctx.Artists.FirstOrDefaultAsync(a => a.Name.ToLower() == artistName.Trim().ToLower());
        if (artist == null)
        {
            artist = new Artist { Name = artistName.Trim(), SortName = artistName.Trim() };
            ctx.Artists.Add(artist);
            await ctx.SaveChangesAsync();
        }

        // 3. Resolve/update Album entity
        var album = await ctx.Albums.FirstOrDefaultAsync(a => a.Title.ToLower() == albumTitle.Trim().ToLower() && a.ArtistName.ToLower() == artistName.Trim().ToLower());
        if (album == null)
        {
            album = new Album
            {
                Title = albumTitle.Trim(),
                ArtistId = artist.Id,
                ArtistName = artist.Name,
                Year = year,
                Genre = genre
            };
            ctx.Albums.Add(album);
            await ctx.SaveChangesAsync();
        }

        // 4. Update track fields
        track.Title = title.Trim();
        track.ArtistId = artist.Id;
        track.ArtistName = artist.Name;
        track.AlbumId = album.Id;
        track.AlbumTitle = album.Title;
        track.Year = year;
        track.Genre = genre.Trim();
        track.TrackNumber = trackNumber;
        track.DiscNumber = discNumber;

        await ctx.SaveChangesAsync();
        LibraryUpdated?.Invoke(this, EventArgs.Empty);
    }

    public async Task<IReadOnlyList<Album>> GetPinnedAlbumsAsync()
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var pinned = await ctx.Albums
            .Include(a => a.Tracks)
            .Where(a => a.IsPinned)
            .OrderByDescending(a => a.PinnedAtUtc)
            .ToListAsync();

        if (pinned.Count == 0)
        {
            // Fall back to first 6 albums so quickplay is never blank on initial launch
            return await ctx.Albums
                .Include(a => a.Tracks)
                .OrderBy(a => a.Title)
                .Take(6)
                .ToListAsync();
        }

        return pinned;
    }

    public async Task PinAlbumAsync(Guid albumId)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var album = await ctx.Albums.FindAsync(albumId);
        if (album != null)
        {
            album.IsPinned = true;
            album.PinnedAtUtc = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
            LibraryUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task UnpinAlbumAsync(Guid albumId)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var album = await ctx.Albums.FindAsync(albumId);
        if (album != null)
        {
            album.IsPinned = false;
            album.PinnedAtUtc = null;
            await ctx.SaveChangesAsync();
            LibraryUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    private FileSystemWatcher? _watcher;
    private System.Threading.Timer? _debounceTimer;
    private string? _watchedDirectory;
    private readonly object _watcherLock = new();

    public void StartDirectoryWatcher(string directoryPath)
    {
        lock (_watcherLock)
        {
            StopDirectoryWatcher();

            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                return;
            }

            _watchedDirectory = directoryPath;
            try
            {
                _watcher = new FileSystemWatcher(directoryPath)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size
                };

                _watcher.Created += (_, _) => ScheduleDebouncedScan();
                _watcher.Changed += (_, _) => ScheduleDebouncedScan();
                _watcher.Deleted += (_, _) => ScheduleDebouncedScan();
                _watcher.Renamed += (_, _) => ScheduleDebouncedScan();
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileSystemWatcher] Could not start watcher for '{directoryPath}': {ex.Message}");
            }
        }
    }

    public void StopDirectoryWatcher()
    {
        lock (_watcherLock)
        {
            if (_watcher != null)
            {
                try
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Dispose();
                }
                catch { }
                _watcher = null;
            }

            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }
    }

    private void ScheduleDebouncedScan()
    {
        lock (_watcherLock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new System.Threading.Timer(async _ =>
            {
                if (!string.IsNullOrEmpty(_watchedDirectory) && Directory.Exists(_watchedDirectory))
                {
                    try
                    {
                        await ScanDirectoryAsync(_watchedDirectory);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[FileSystemWatcher] Auto-scan error: {ex.Message}");
                    }
                }
            }, null, 1500, System.Threading.Timeout.Infinite);
        }
    }
}
