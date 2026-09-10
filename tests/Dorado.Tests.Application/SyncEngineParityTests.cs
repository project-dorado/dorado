using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.Application.Services;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;
using Dorado.Infrastructure.Persistence;
using Xunit;

namespace Dorado.Tests.Application;

public class SyncEngineParityTests
{
    private static SyncInput BuildInput(int trackCount = 3, int videoCount = 1)
    {
        var tracks = Enumerable.Range(1, trackCount).Select(i => new Track
        {
            Title = $"Track {i:D2}",
            ArtistName = $"Artist {i}",
            AlbumTitle = $"Album {i}",
            FilePath = $"/music/track{i}.mp3",
            Duration = TimeSpan.FromMinutes(3 + i),
            Rating = i == 1 ? HeartRating.Favorite : HeartRating.None
        }).ToList();

        var videos = Enumerable.Range(1, videoCount).Select(i => new Video
        {
            Title = $"Video {i}",
            FilePath = $"/video/video{i}.mp4",
            Duration = TimeSpan.FromMinutes(10 * i)
        }).ToList();

        var episodes = Enumerable.Range(1, 4).Select(i => new PodcastEpisode
        {
            Title = $"Episode {i}",
            AudioUrl = $"/podcast/ep{i}.mp3",
            PublishedAtUtc = DateTime.UtcNow.AddDays(-i),
            IsPlayed = i > 2
        }).ToList();

        return new SyncInput { Tracks = tracks, Videos = videos, PodcastEpisodes = episodes };
    }

    private static IDeviceTransport BuildTransport(string serial = "SIM-001") => new SimulatedDeviceTransport(serial, "Zune 30", 32L * 1024 * 1024 * 1024);

    private static SyncEngine CreateEngine() => new();

    [Fact]
    public void BuildDefaultGroup_MapsSettingsToSyncCategories()
    {
        var settings = new AppSettings
        {
            MusicSyncRule = "All Music (Automatic Sync)",
            PodcastSyncRule = "3 Newest Episodes",
            VideoSyncRule = "Nothing (Manual Drag and Drop)"
        };

        var group = CreateEngine().BuildDefaultGroup("SIM-001", settings);

        var music = Assert.Single(group.Categories, c => c.Category == SyncCategoryType.Music);
        Assert.Equal(SyncMode.Automatic, music.Mode);
        Assert.Null(music.NewestCount);

        var podcasts = Assert.Single(group.Categories, c => c.Category == SyncCategoryType.Podcasts);
        Assert.Equal(SyncMode.SelectedItems, podcasts.Mode);
        Assert.Equal(3, podcasts.NewestCount);

        var video = Assert.Single(group.Categories, c => c.Category == SyncCategoryType.Videos);
        Assert.Equal(SyncMode.Manual, video.Mode);
    }

    [Fact]
    public void BuildDefaultGroup_GuestSession_NeverRemoves()
    {
        var group = CreateEngine().BuildDefaultGroup("SIM-001", new AppSettings(), isGuestSession: true);

        Assert.True(group.IsGuestSession);
        Assert.Equal("Guest Session — SIM-001", group.Name);
    }

    [Fact]
    public void BuildPlan_AddsAllMusic_WhenDeviceEmpty()
    {
        var engine = CreateEngine();
        var group = engine.BuildDefaultGroup("SIM-001", new AppSettings());
        var input = BuildInput(trackCount: 3);
        var transport = BuildTransport();

        var plan = engine.BuildPlan(group, input, transport);

        Assert.Equal(3, plan.Items.Count(i => i.Action == TransferAction.Add && i.Category == SyncCategoryType.Music));
        Assert.True(plan.TotalAddBytes > 0);
    }

    [Fact]
    public void BuildPlan_KeepsExisting_AndRemovesStale()
    {
        var engine = CreateEngine();
        var input = BuildInput(trackCount: 2);
        var transport = BuildTransport();
        // Seed device with: one matching track + one stale item.
        transport.CopyToDevice(new TransferItem { Action = TransferAction.Add, Category = SyncCategoryType.Music, EntityId = input.Tracks[0].Id, Title = "Track 01", SourcePath = input.Tracks[0].FilePath });
        transport.CopyToDevice(new TransferItem { Action = TransferAction.Add, Category = SyncCategoryType.Music, EntityId = Guid.NewGuid(), Title = "Old Song", SourcePath = "/music/old.mp3" });

        var group = engine.BuildDefaultGroup(
            "SIM-001",
            new AppSettings
            {
                PodcastSyncRule = "Nothing (Manual Drag and Drop)",
                VideoSyncRule = "Nothing (Manual Drag and Drop)"
            });
        var plan = engine.BuildPlan(group, input, transport);

        var adds = plan.Items.Where(i => i.Action == TransferAction.Add).ToList();
        var keeps = plan.Items.Where(i => i.Action == TransferAction.Keep).ToList();
        var removes = plan.Items.Where(i => i.Action == TransferAction.Remove).ToList();

        Assert.Single(adds);
        Assert.Equal("Track 02", adds[0].Title);
        Assert.Single(keeps);
        Assert.Equal("Track 01", keeps[0].Title);
        Assert.Equal(3, removes.Count);
        Assert.Contains(removes, r => r.Title == "Old Song");
        Assert.Contains(removes, r => r.Title == "Ice Ice Baby");
        Assert.Contains(removes, r => r.Title == "Macarena");
        Assert.DoesNotContain(removes, r => r.Category == SyncCategoryType.Podcasts);
        Assert.DoesNotContain(removes, r => r.Category == SyncCategoryType.Videos);
        Assert.DoesNotContain(removes, r => r.Category == SyncCategoryType.Pictures);
    }

    [Fact]
    public void BuildPlan_HonorsLimitPerCategory()
    {
        var engine = CreateEngine();
        var group = new SyncGroup
        {
            DeviceSerialNumber = "SIM-001",
            Categories = new List<SyncCategoryRule>
            {
                new() { Category = SyncCategoryType.Music, Mode = SyncMode.Automatic, NewestCount = 2 }
            }
        };
        var transport = BuildTransport();

        var plan = engine.BuildPlan(group, BuildInput(trackCount: 5), transport);

        Assert.Equal(2, plan.Items.Count(i => i.Action == TransferAction.Add && i.Category == SyncCategoryType.Music));
    }

    [Fact]
    public void BuildPlan_FavoritesOnly_SelectsHearted()
    {
        var engine = CreateEngine();
        var group = new SyncGroup
        {
            DeviceSerialNumber = "SIM-001",
            Categories = new List<SyncCategoryRule>
            {
                new() { Category = SyncCategoryType.Music, Mode = SyncMode.Automatic, PreferFavorites = true }
            }
        };
        var transport = BuildTransport();

        var plan = engine.BuildPlan(group, BuildInput(trackCount: 3), transport);

        var adds = plan.Items.Where(i => i.Action == TransferAction.Add && i.Category == SyncCategoryType.Music).ToList();
        Assert.Single(adds);
        Assert.Equal("Track 01", adds[0].Title);
    }

    [Fact]
    public void BuildPlan_GuestSession_NeverQueuesRemovals()
    {
        var engine = CreateEngine();
        var input = BuildInput(trackCount: 2);
        var transport = BuildTransport();
        transport.CopyToDevice(new TransferItem { Action = TransferAction.Add, Category = SyncCategoryType.Music, EntityId = Guid.NewGuid(), Title = "Old Song", SourcePath = "/music/old.mp3" });

        var group = engine.BuildDefaultGroup("SIM-001", new AppSettings(), isGuestSession: true);
        var plan = engine.BuildPlan(group, input, transport);

        Assert.Empty(plan.Items.Where(i => i.Action == TransferAction.Remove));
        Assert.NotEmpty(plan.Items.Where(i => i.Action == TransferAction.Add));
    }

    [Fact]
    public void BuildPlan_SkipsItemsBeyondFreeSpace()
    {
        var engine = CreateEngine();
        var group = new SyncGroup
        {
            DeviceSerialNumber = "SIM-001",
            Categories = new List<SyncCategoryRule>
            {
                new() { Category = SyncCategoryType.Music, Mode = SyncMode.Automatic }
            }
        };
        // 10 MB device: after system reservation only one small track fits.
        var transport = new SimulatedDeviceTransport("SIM-001", "Tiny Zune", 11L * 1024 * 1024);
        var plan = engine.BuildPlan(group, BuildInput(trackCount: 5), transport);

        var adds = plan.Items.Where(i => i.Action == TransferAction.Add).ToList();
        Assert.NotEmpty(adds);
        Assert.True(adds.Count < 5);
    }

    [Fact]
    public async Task ApplyPlanAsync_CopiesAndRemoves_ViaTransport()
    {
        var engine = CreateEngine();
        var input = BuildInput(trackCount: 2);
        var transport = BuildTransport();
        transport.CopyToDevice(new TransferItem { Action = TransferAction.Add, Category = SyncCategoryType.Music, EntityId = Guid.NewGuid(), Title = "Old Song", SourcePath = "/music/old.mp3" });

        var group = engine.BuildDefaultGroup("SIM-001", new AppSettings());
        var plan = engine.BuildPlan(group, input, transport);

        var progressValues = new List<double>();
        await engine.ApplyPlanAsync(plan, transport, new Progress<double>(progressValues.Add));

        var contents = transport.GetContents().Where(c => c.Category == SyncCategoryType.Music).ToList();
        Assert.Equal(2, contents.Count);
        Assert.Contains(contents, c => c.Title == "Track 01");
        Assert.Contains(contents, c => c.Title == "Track 02");
        Assert.DoesNotContain(contents, c => c.Title == "Old Song");
    }

    [Fact]
    public void SimulatedDeviceTransport_TracksFreeSpace()
    {
        var transport = BuildTransport();
        var freeBefore = transport.FreeBytes;

        transport.CopyToDevice(new TransferItem { Action = TransferAction.Add, Category = SyncCategoryType.Music, EntityId = Guid.NewGuid(), Title = "T", SourcePath = "/x.mp3", SizeBytes = 1024 * 1024 * 10 });

        Assert.Equal(freeBefore - 1024L * 1024 * 10, transport.FreeBytes);
    }
}

public class SyncGroupPersistenceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dbPath;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public SyncGroupPersistenceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dorado-sync-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "test.db");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_dbPath}").Options;
        _dbFactory = new TestDbContextFactory(options);
        using var ctx = _dbFactory.CreateDbContext();
        ctx.Database.EnsureCreated();
    }

    [Fact]
    public async Task SaveAndGetForDevice_RoundTripsCategories()
    {
        var service = new SyncGroupService(_dbFactory);
        var group = new SyncGroup
        {
            DeviceSerialNumber = "SIM-777",
            IsGuestSession = false,
            Categories = new List<SyncCategoryRule>
            {
                new() { Category = SyncCategoryType.Music, Mode = SyncMode.Automatic, NewestCount = 1200, PreferFavorites = true },
                new() { Category = SyncCategoryType.Podcasts, Mode = SyncMode.Automatic, NewestCount = 3 }
            }
        };

        await service.SaveAsync(group);
        var loaded = await service.GetForDeviceAsync("SIM-777");

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.Categories.Count);
        var music = loaded.Categories.Single(c => c.Category == SyncCategoryType.Music);
        Assert.Equal(SyncMode.Automatic, music.Mode);
        Assert.Equal(1200, music.NewestCount);
        Assert.True(music.PreferFavorites);
    }

    [Fact]
    public async Task GetForDevice_Missing_ReturnsNull()
    {
        var service = new SyncGroupService(_dbFactory);
        var loaded = await service.GetForDeviceAsync("UNKNOWN");

        Assert.Null(loaded);
    }

    [Fact]
    public async Task DeleteAsync_RemovesGroup()
    {
        var service = new SyncGroupService(_dbFactory);
        var group = new SyncGroup
        {
            DeviceSerialNumber = "SIM-888",
            Categories = new List<SyncCategoryRule> { new() { Category = SyncCategoryType.Music, Mode = SyncMode.Manual } }
        };
        await service.SaveAsync(group);

        await service.DeleteAsync(group.Id);

        Assert.Null(await service.GetForDeviceAsync("SIM-888"));
    }

    public void Dispose()
    {
        // Windows keeps pooled SQLite connections open, which would lock test.db.
        SqliteConnection.ClearAllPools();

        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup; a lingering temp dir must not fail the test run.
        }
    }

    private sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public TestDbContextFactory(DbContextOptions<AppDbContext> options) => _options = options;

        public AppDbContext CreateDbContext() => new(_options);
    }
}
