using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Dorado.Domain.Models;
using Dorado.Infrastructure.Persistence;

namespace Dorado.Tests.Application;

public class SearchIndexTests : IDisposable
{
    private readonly string _dbPath;
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly MediaLibraryService _service;

    public SearchIndexTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"dorado_search_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;

        _factory = new Factory(options);
        _service = new MediaLibraryService(_factory);

        using var ctx = _factory.CreateDbContext();
        ctx.Database.EnsureCreated();
        SearchIndex.Ensure(ctx);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }

    [Fact]
    public void Match_expression_prefixes_and_sanitizes_tokens()
    {
        Assert.Equal("daft* AND punk*", SearchIndex.BuildMatchExpression("Daft-Punk!"));
        Assert.Null(SearchIndex.BuildMatchExpression("   "));
    }

    [Fact]
    public async Task Search_matches_title_artist_album_and_genre()
    {
        await using (var ctx = _factory.CreateDbContext())
        {
            AddTrack(ctx, "Subdivisions", "Rush", "Signals", "Progressive Rock");
            AddTrack(ctx, "One More Time", "Daft Punk", "Discovery", "Electronic");
            await ctx.SaveChangesAsync();
        }

        Assert.Single(await _service.SearchAsync("subdiv"));
        Assert.Single(await _service.SearchAsync("rush"));
        Assert.Single(await _service.SearchAsync("signals"));
        Assert.Single(await _service.SearchAsync("electronic"));
        // Multi-term queries AND across columns (artist + album).
        Assert.Single(await _service.SearchAsync("daft discovery"));
    }

    [Fact]
    public async Task Search_index_tracks_insert_update_and_delete()
    {
        Guid id;
        await using (var ctx = _factory.CreateDbContext())
        {
            var track = AddTrack(ctx, "Alpha", "A", "First", "Rock");
            id = track.Id;
            await ctx.SaveChangesAsync();
        }

        Assert.Single(await _service.SearchAsync("alpha"));

        await using (var ctx = _factory.CreateDbContext())
        {
            var track = await ctx.Tracks.FindAsync(id);
            track!.Title = "Omega";
            await ctx.SaveChangesAsync();
        }

        Assert.Empty(await _service.SearchAsync("alpha"));
        Assert.Single(await _service.SearchAsync("omega"));

        await using (var ctx = _factory.CreateDbContext())
        {
            ctx.Tracks.Remove(await ctx.Tracks.FindAsync(id) ?? throw new InvalidOperationException());
            await ctx.SaveChangesAsync();
        }

        Assert.Empty(await _service.SearchAsync("omega"));
    }

    private static Track AddTrack(AppDbContext ctx, string title, string artistName, string albumTitle, string genre)
    {
        var artist = new Artist { Name = artistName };
        var album = new Album { Title = albumTitle, ArtistId = artist.Id, ArtistName = artistName };
        ctx.Artists.Add(artist);
        ctx.Albums.Add(album);

        var track = new Track
        {
            Title = title,
            ArtistId = artist.Id,
            ArtistName = artistName,
            AlbumId = album.Id,
            AlbumTitle = albumTitle,
            Genre = genre
        };
        ctx.Tracks.Add(track);
        return track;
    }

    private sealed class Factory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;
        public Factory(DbContextOptions<AppDbContext> options) => _options = options;
        public AppDbContext CreateDbContext() => new(_options);
    }
}
