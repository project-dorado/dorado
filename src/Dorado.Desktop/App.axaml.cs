using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Dorado.Application;
using Dorado.Application.Interfaces;
using Dorado.Application.Services;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;
using Dorado.Infrastructure.Audio;
using Dorado.Infrastructure.Devices;
using Dorado.Infrastructure.External;
using Dorado.Infrastructure.Persistence;
using Dorado.Infrastructure.Video;using Dorado.UI.Services;
using Dorado.Plugins.Host;
using Dorado.UI.ViewModels;

namespace Dorado.Desktop;

public partial class App : Avalonia.Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Seed initial rich demo data
        SeedDemoData(_serviceProvider);

        // Plugin host: attach the player-event bridge and start enabled plugins.
        var pluginManager = _serviceProvider.GetRequiredService<PluginManager>();
        _serviceProvider.GetRequiredService<PluginEventBridge>().Attach();
        _ = pluginManager.StartEnabledAsync();

        // Populate AppInfo for the About page: runtime + commit identifier.
        AppInfo.RuntimeIdentifier =
            $"{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription} on " +
            $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription}";
        AppInfo.BuildIdentifier =
            $"Built on {DateTime.UtcNow:yyyy-MM-dd HH:mm 'UTC'}";

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var shellVm = _serviceProvider.GetRequiredService<MainShellViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = shellVm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // 1. Persistence
        services.AddDbContextFactory<AppDbContext>(options =>
        {
            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dorado", "dorado.db");
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            options.UseSqlite($"Data Source={dbPath}");
        });

        // 2. Application Core Services
        services.AddSingleton<IAudioOutputEngine, BassAudioOutputEngine>();
        services.AddSingleton<IReplayGainService, TagLibReplayGainService>();
        services.AddSingleton<IPlayerCoordinator>(sp => new PlaybackQueueCoordinator(
            sp.GetRequiredService<IAudioOutputEngine>(),
            sp.GetRequiredService<IReplayGainService>()));
        services.AddSingleton<IMediaLibraryService, MediaLibraryService>();
        services.AddSingleton<IDeviceSyncService, ZuneDeviceSyncService>();
        services.AddSingleton<ISmartDJService, SmartDJEngine>();
        services.AddSingleton<ISoundEffectService, SoundEffectService>();
        services.AddSingleton<IUserStatsService, UserStatsService>();
        services.AddSingleton<IPodcastService, PodcastService>();
        services.AddSingleton<IFolderPickerService, AvaloniaFolderPickerService>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IArtworkCacheService, ArtworkCacheService>();
        services.AddSingleton<IExternalMetadataService, ExternalMetadataService>();
        services.AddSingleton<IArtistEnrichmentService, ArtistEnrichmentCoordinator>();
        services.AddSingleton<ISmartPlaylistService, SmartPlaylistService>();
        services.AddSingleton<IVideoLibraryService, VideoLibraryService>();
        services.AddSingleton<IPhotoLibraryService, PhotoLibraryService>();
        services.AddSingleton<IVideoPlaybackEngine, VideoPlaybackEngine>();
        services.AddSingleton<ISyncEngine, SyncEngine>();
        services.AddSingleton<ISyncGroupService, SyncGroupService>();

        // Plugin host (Phase 12): out-of-process plugins with an event bridge.
        services.AddSingleton(sp =>
        {
            var options = new PluginManagerOptions();
            var storage = new PluginStorage(Path.Combine(options.ConfigDirectory, "storage"));
            var hostServices = new PluginHostServices(storage, sp.GetRequiredService<IMediaLibraryService>());
            return new PluginManager(options, plugin => new ProcessPluginTransport(plugin), hostServices);
        });
        services.AddSingleton(sp => new PluginEventBridge(
            sp.GetRequiredService<IPlayerCoordinator>(),
            sp.GetRequiredService<PluginManager>()));

        // 3. Audio & Hardware Subsystems
        services.AddSingleton<AudioEngine>();
        services.AddSingleton<ZuneUsbHttpInterceptor>();

        // 4. ViewModels
        services.AddSingleton<MainShellViewModel>();
    }

    private static void SeedDemoData(IServiceProvider provider)
    {
        try
        {
            var factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using var ctx = factory.CreateDbContext();
            ctx.Database.EnsureCreated();

            // WAL journaling persists in the database file and keeps large library
            // scans/sync-group writes responsive.
            ctx.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");

            // Schema upgrades for databases created before later phases (EnsureCreated
            // only provisions brand-new databases; it never alters existing ones).
            ctx.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS SmartPlaylists (
                Id TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL,
                Description TEXT,
                Match INTEGER NOT NULL,
                TrackLimit INTEGER NOT NULL,
                SortField TEXT NOT NULL,
                SortDescending INTEGER NOT NULL,
                Rules TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL)");

            ctx.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS Videos (
                Id TEXT NOT NULL PRIMARY KEY,
                Title TEXT NOT NULL,
                FilePath TEXT NOT NULL,
                Duration TEXT NOT NULL,
                Year INTEGER,
                Genre TEXT NOT NULL,
                ArtworkUri TEXT,
                PlayCount INTEGER NOT NULL,
                LastPlayedAtUtc TEXT,
                SizeBytes INTEGER NOT NULL,
                AddedAtUtc TEXT NOT NULL)");

            ctx.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS Photos (
                Id TEXT NOT NULL PRIMARY KEY,
                Title TEXT NOT NULL,
                FilePath TEXT NOT NULL,
                FolderPath TEXT NOT NULL,
                TakenDate TEXT,
                SizeBytes INTEGER NOT NULL,
                AddedAtUtc TEXT NOT NULL)");

            ctx.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS SyncGroups (
                Id TEXT NOT NULL PRIMARY KEY,
                DeviceSerialNumber TEXT NOT NULL,
                Name TEXT NOT NULL,
                IsGuestSession INTEGER NOT NULL,
                Categories TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL)");

            if (!ctx.Tracks.Any())
            {
                // Artists
                var rush = new Artist { Name = "Rush", SortName = "Rush" };
                var daftPunk = new Artist { Name = "Daft Punk", SortName = "Daft Punk" };
                var pinkFloyd = new Artist { Name = "Pink Floyd", SortName = "Pink Floyd" };
                var fleetwoodMac = new Artist { Name = "Fleetwood Mac", SortName = "Fleetwood Mac" };
                var milesDavis = new Artist { Name = "Miles Davis", SortName = "Miles Davis" };
                var newOrder = new Artist { Name = "New Order", SortName = "New Order" };

                ctx.Artists.AddRange(rush, daftPunk, pinkFloyd, fleetwoodMac, milesDavis, newOrder);

                // Albums
                var albumSignals = new Album { Title = "Signals", ArtistId = rush.Id, ArtistName = "Rush", Year = 1982, Genre = "Progressive Rock" };
                var albumDiscovery = new Album { Title = "Discovery", ArtistId = daftPunk.Id, ArtistName = "Daft Punk", Year = 2001, Genre = "Electronic" };
                var albumDarkSide = new Album { Title = "The Dark Side of the Moon", ArtistId = pinkFloyd.Id, ArtistName = "Pink Floyd", Year = 1973, Genre = "Progressive Rock" };
                var albumRumours = new Album { Title = "Rumours", ArtistId = fleetwoodMac.Id, ArtistName = "Fleetwood Mac", Year = 1977, Genre = "Classic Rock" };
                var albumKindBlue = new Album { Title = "Kind of Blue", ArtistId = milesDavis.Id, ArtistName = "Miles Davis", Year = 1959, Genre = "Jazz" };
                var albumPcl = new Album { Title = "Power, Corruption & Lies", ArtistId = newOrder.Id, ArtistName = "New Order", Year = 1983, Genre = "Post-Punk" };

                ctx.Albums.AddRange(albumSignals, albumDiscovery, albumDarkSide, albumRumours, albumKindBlue, albumPcl);

                // Helper to create tracks
                Track MakeTrack(Album album, Artist artist, int num, string title, int mins, int secs, HeartRating rating = HeartRating.None) => new()
                {
                    Title = title,
                    ArtistId = artist.Id,
                    ArtistName = artist.Name,
                    AlbumId = album.Id,
                    AlbumTitle = album.Title,
                    TrackNumber = num,
                    Duration = TimeSpan.FromMinutes(mins) + TimeSpan.FromSeconds(secs),
                    Year = album.Year,
                    Genre = album.Genre,
                    Rating = rating
                };

                var tracks = new List<Track>
                {
                    // Rush - Signals
                    MakeTrack(albumSignals, rush, 1, "Subdivisions", 5, 34, HeartRating.Favorite),
                    MakeTrack(albumSignals, rush, 2, "The Analog Kid", 4, 47),
                    MakeTrack(albumSignals, rush, 3, "Chemistry", 4, 57),
                    MakeTrack(albumSignals, rush, 4, "Digital Man", 5, 52),
                    MakeTrack(albumSignals, rush, 5, "The Weapon", 6, 24),
                    MakeTrack(albumSignals, rush, 6, "New World Man", 3, 42),

                    // Daft Punk - Discovery
                    MakeTrack(albumDiscovery, daftPunk, 1, "One More Time", 5, 20, HeartRating.Favorite),
                    MakeTrack(albumDiscovery, daftPunk, 2, "Aerodynamic", 3, 27),
                    MakeTrack(albumDiscovery, daftPunk, 3, "Digital Love", 4, 58),
                    MakeTrack(albumDiscovery, daftPunk, 4, "Harder, Better, Faster, Stronger", 3, 45, HeartRating.Favorite),
                    MakeTrack(albumDiscovery, daftPunk, 5, "Crescendolls", 3, 31),
                    MakeTrack(albumDiscovery, daftPunk, 6, "Voyager", 3, 47, HeartRating.Favorite),

                    // Pink Floyd - Dark Side
                    MakeTrack(albumDarkSide, pinkFloyd, 1, "Speak to Me", 1, 13),
                    MakeTrack(albumDarkSide, pinkFloyd, 2, "Breathe (In the Air)", 2, 43),
                    MakeTrack(albumDarkSide, pinkFloyd, 3, "On the Run", 3, 36),
                    MakeTrack(albumDarkSide, pinkFloyd, 4, "Time", 6, 53, HeartRating.Favorite),
                    MakeTrack(albumDarkSide, pinkFloyd, 5, "The Great Gig in the Sky", 4, 43),
                    MakeTrack(albumDarkSide, pinkFloyd, 6, "Money", 6, 22, HeartRating.Favorite),
                    MakeTrack(albumDarkSide, pinkFloyd, 7, "Us and Them", 7, 49),

                    // Fleetwood Mac - Rumours
                    MakeTrack(albumRumours, fleetwoodMac, 1, "Second Hand News", 2, 53),
                    MakeTrack(albumRumours, fleetwoodMac, 2, "Dreams", 4, 17, HeartRating.Favorite),
                    MakeTrack(albumRumours, fleetwoodMac, 3, "Never Going Back Again", 2, 14),
                    MakeTrack(albumRumours, fleetwoodMac, 4, "Don't Stop", 3, 13),
                    MakeTrack(albumRumours, fleetwoodMac, 5, "Go Your Own Way", 3, 38, HeartRating.Favorite),
                    MakeTrack(albumRumours, fleetwoodMac, 6, "The Chain", 4, 30, HeartRating.Favorite),

                    // Miles Davis - Kind of Blue
                    MakeTrack(albumKindBlue, milesDavis, 1, "So What", 9, 22, HeartRating.Favorite),
                    MakeTrack(albumKindBlue, milesDavis, 2, "Freddie Freeloader", 9, 46),
                    MakeTrack(albumKindBlue, milesDavis, 3, "Blue in Green", 5, 37),
                    MakeTrack(albumKindBlue, milesDavis, 4, "All Blues", 11, 33),

                    // New Order - Power, Corruption & Lies
                    MakeTrack(albumPcl, newOrder, 1, "Age of Consent", 5, 15, HeartRating.Favorite),
                    MakeTrack(albumPcl, newOrder, 2, "We All Stand", 5, 14),
                    MakeTrack(albumPcl, newOrder, 3, "The Village", 4, 37),
                    MakeTrack(albumPcl, newOrder, 4, "Your Silent Face", 6, 0),
                    MakeTrack(albumPcl, newOrder, 5, "Blue Monday", 7, 29, HeartRating.Favorite)
                };

                ctx.Tracks.AddRange(tracks);

                // Initial Play History
                ctx.PlayHistory.Add(new PlayHistoryEntry
                {
                    TrackId = tracks[0].Id,
                    TrackTitle = tracks[0].Title,
                    ArtistName = tracks[0].ArtistName,
                    AlbumTitle = tracks[0].AlbumTitle,
                    PlayedAtUtc = DateTime.UtcNow.AddHours(-1),
                    DurationPlayed = tracks[0].Duration,
                    Completed = true
                });

                ctx.PlayHistory.Add(new PlayHistoryEntry
                {
                    TrackId = tracks[6].Id,
                    TrackTitle = tracks[6].Title,
                    ArtistName = tracks[6].ArtistName,
                    AlbumTitle = tracks[6].AlbumTitle,
                    PlayedAtUtc = DateTime.UtcNow.AddMinutes(-30),
                    DurationPlayed = tracks[6].Duration,
                    Completed = true
                });

                ctx.PlayHistory.Add(new PlayHistoryEntry
                {
                    TrackId = tracks[15].Id,
                    TrackTitle = tracks[15].Title,
                    ArtistName = tracks[15].ArtistName,
                    AlbumTitle = tracks[15].AlbumTitle,
                    PlayedAtUtc = DateTime.UtcNow.AddMinutes(-12),
                    DurationPlayed = tracks[15].Duration,
                    Completed = true
                });

                ctx.SaveChanges();
            }
        }
        catch
        {
            // Non-critical demo seeding fallback
        }
    }
}
