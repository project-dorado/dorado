using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Dorado.Application;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.Application.Services;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;
using Dorado.Infrastructure.Audio;
using Dorado.Infrastructure.Devices;
using Dorado.Infrastructure.Emulator;
using Dorado.Infrastructure.External;
using Dorado.Infrastructure.Persistence;
using Dorado.Infrastructure.Video;
using Dorado.UI.Services;
using Dorado.Plugins.Host;
using Dorado.UI.ViewModels;
using DoradoCloud.Client;

namespace Dorado.Desktop;

public partial class App : Avalonia.Application
{
    private ServiceProvider? _serviceProvider;
    private SyncMdnsAdvertiser? _syncAdvertiser;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Provision the database schema and clean up any legacy demo/placeholder rows.
        InitializeDatabase(_serviceProvider);

        // Silent-fallback playback clock. Must be constructed on the UI thread so its
        // SynchronizationContext is captured; without it simulated playback (no audio
        // device, or source-less demo tracks) never advances and the HUD sticks at 0:00.
        _serviceProvider.GetRequiredService<AudioEngine>();

        // Plugin host: attach the player-event bridge and start enabled plugins.
        var pluginManager = _serviceProvider.GetRequiredService<PluginManager>();
        _serviceProvider.GetRequiredService<PluginEventBridge>().Attach();
        _ = pluginManager.StartEnabledAsync();

        // LAN sync: start the phone sync socket only when the user opted in.
        try
        {
            var lanSettings = _serviceProvider.GetRequiredService<ISettingsStore>().Load();
            if (lanSettings.LanSyncEnabled)
            {
                _serviceProvider.GetRequiredService<SyncTcpServer>().Start();
                // Advertise _dorado-sync._tcp so Dorado-HD discovers us automatically.
                _syncAdvertiser = new SyncMdnsAdvertiser("Dorado Desktop", lanSettings.LanSyncPort);
                _syncAdvertiser.Start();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LAN sync server failed to start: {ex.Message}");
        }

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

            desktop.Exit += (_, _) => _syncAdvertiser?.Dispose();
        }

        // Signed update check (opt-in). Runs off the UI thread; surfaces a verified
        // release through the in-shell dialog. Never blocks startup, never throws.
        _ = CheckForUpdatesAsync(_serviceProvider);

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
        // Real transcoding for device sync (FFmpeg on PATH); no-ops when absent.
        services.AddSingleton<ITranscodeService, FfmpegTranscodeService>();
        services.AddSingleton<IPlayerCoordinator>(sp => new PlaybackQueueCoordinator(
            sp.GetRequiredService<IAudioOutputEngine>(),
            sp.GetRequiredService<IReplayGainService>()));
        services.AddSingleton<IMediaLibraryService, MediaLibraryService>();
        services.AddSingleton<IDeviceSyncService, ZuneDeviceSyncService>();
        services.AddSingleton<ISmartDJService, SmartDJEngine>();
        services.AddSingleton<ISoundEffectService, SoundEffectService>();
        services.AddSingleton<ICloudSocialService>(sp =>
            new CloudSocialService(() => sp.GetRequiredService<ISettingsStore>().Load()));
        services.AddSingleton<IUserStatsService>(sp => new UserStatsService(
            sp.GetRequiredService<IMediaLibraryService>(),
            sp.GetRequiredService<IReviewService>(),
            sp.GetRequiredService<ICloudSocialService>()));
        services.AddSingleton<IPodcastFeedClient, PodcastFeedClient>();
        services.AddSingleton<ICloudDirectoryService>(sp =>
            new CloudDirectoryService(() => sp.GetRequiredService<ISettingsStore>().Load()));
        services.AddSingleton<ICloudUpdateService>(sp =>
            new CloudUpdateService(() => sp.GetRequiredService<ISettingsStore>().Load()));
        // OIDC Authorization Code + PKCE sign-in (browser + loopback callback).
        services.AddSingleton<IOAuthPkceService, OAuthPkceService>();
        services.AddSingleton<ICloudSignInService>(sp => new CloudSignInService(
            sp.GetRequiredService<ISettingsStore>(),
            sp.GetRequiredService<IOAuthPkceService>()));
        services.AddSingleton<IPodcastService>(sp => new PodcastService(
            sp.GetRequiredService<IPlayerCoordinator>(),
            sp.GetRequiredService<IPodcastFeedClient>(),
            sp.GetRequiredService<ICloudDirectoryService>()));
        services.AddSingleton<IFolderPickerService, AvaloniaFolderPickerService>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IArtworkCacheService, ArtworkCacheService>();
        services.AddSingleton<ExternalMetadataService>();
        // Dorado Cloud client — centralized auth via the SDK. The credential
        // store persists access/refresh tokens in settings; CloudClientProvider
        // rebuilds the client when the base URL changes, and the SDK auth handler
        // refreshes tokens on expiry. All gated by settings.CloudEnabled.
        services.AddSingleton<ICloudCredentialStore, SettingsCloudCredentialStore>();
        services.AddSingleton(sp => new CloudClientProvider(
            () => sp.GetRequiredService<ISettingsStore>().Load(),
            sp.GetRequiredService<ICloudCredentialStore>()));
        services.AddSingleton<IExternalMetadataService>(sp => new CloudBackedMetadataService(
            sp.GetRequiredService<ExternalMetadataService>(),
            () => sp.GetRequiredService<CloudClientProvider>().Get(),
            () => sp.GetRequiredService<ISettingsStore>().Load()));
        // Mixview external related-artist enrichment reuses the MusicBrainz-backed
        // facade (cached + rate-limited); optional and failure-tolerant.
        services.AddSingleton<IArtistRelationshipService>(sp => sp.GetRequiredService<ExternalMetadataService>());

        // Emulator bridge: lazily spawns `dorado --ipc` on first use and drives
        // it over JSON-RPC. The emulator CLI is a separate repo/process; no
        // process starts unless a caller actually resolves this service.
        services.AddSingleton<IEmulatorBridge>(sp =>
        {
            var settings = sp.GetRequiredService<ISettingsStore>().Load();
            var options = new EmulatorProcessOptions
            {
                EntryPointPath = string.IsNullOrWhiteSpace(settings.EmulatorCliPath) ? "dorado" : settings.EmulatorCliPath,
            };
            return new JsonRpcEmulatorBridge(new EmulatorProcessTransport(options));
        });

        // LAN sync endpoint: the desktop is the server for the phone sync
        // protocol (sync.hello/pair/manifest/pull/push). The TCP listener only
        // starts when settings.LanSyncEnabled is true.
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<ISettingsStore>().Load();
            var library = sp.GetRequiredService<IMediaLibraryService>();
            var videos = sp.GetRequiredService<IVideoLibraryService>();
            var photos = sp.GetRequiredService<IPhotoLibraryService>();
            var podcasts = sp.GetRequiredService<IPodcastService>();

            async Task<SyncInput> BuildInputAsync(CancellationToken cancellationToken)
            {
                var tracks = await library.GetAllTracksAsync();
                var videoItems = await videos.GetAllVideosAsync();
                var photoItems = await photos.GetAllPhotosAsync();
                var episodes = Array.Empty<PodcastEpisode>();
                try
                {
                    var series = await podcasts.GetAllPodcastsAsync();
                    episodes = series.SelectMany(s => s.Episodes).ToArray();
                }
                catch
                {
                    // podcast store optional
                }

                return new SyncInput
                {
                    Tracks = tracks,
                    Videos = videoItems,
                    Photos = photoItems,
                    PodcastEpisodes = episodes,
                };
            }

            return new SyncEndpointHost(
                sp.GetRequiredService<ISyncEngine>(),
                BuildInputAsync,
                pairingCode: string.IsNullOrWhiteSpace(settings.LanSyncPairingCode) ? null : settings.LanSyncPairingCode);
        });
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<ISettingsStore>().Load();
            return new SyncTcpServer(sp.GetRequiredService<SyncEndpointHost>(), port: settings.LanSyncPort);
        });
        services.AddSingleton<IArtistEnrichmentService, ArtistEnrichmentCoordinator>();
        services.AddSingleton<ISmartPlaylistService, SmartPlaylistService>();
        services.AddSingleton<IVideoLibraryService, VideoLibraryService>();
        services.AddSingleton<IPhotoLibraryService, PhotoLibraryService>();
        services.AddSingleton<IVideoPlaybackEngine, VideoPlaybackEngine>();
        services.AddSingleton<ISyncEngine, SyncEngine>();
        services.AddSingleton<ISyncGroupService, SyncGroupService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>());
        services.AddSingleton<IAudioFeatureStore, SqliteAudioFeatureStore>();
        services.AddSingleton<IReviewService, SqliteReviewService>();
        services.AddSingleton<IAudioAnalysisService, AudioAnalysisService>();
        services.AddSingleton<IDynamicMixService, DynamicMixService>();

        // Plugin host (Phase 12): out-of-process plugins with an event bridge.
        services.AddSingleton(sp =>
        {
            var options = new PluginManagerOptions();
            var storage = new PluginStorage(Path.Combine(options.ConfigDirectory, "storage"));
            var hostServices = new PluginHostServices(
                storage,
                sp.GetRequiredService<IMediaLibraryService>(),
                sp.GetRequiredService<IPlayerCoordinator>());
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

    /// <summary>
    /// Best-effort background update check: honours <c>AutoCheckForUpdates</c>,
    /// consults <see cref="ICloudUpdateService"/>, and only ever surfaces a
    /// release whose detached signature verified.
    /// </summary>
    private static async Task CheckForUpdatesAsync(IServiceProvider provider)
    {
        try
        {
            if (!provider.GetRequiredService<ISettingsStore>().Load().AutoCheckForUpdates)
            {
                return;
            }

            var updates = provider.GetRequiredService<ICloudUpdateService>();
            if (!updates.IsEnabled)
            {
                return;
            }

            var info = await updates.CheckAsync("dorado").ConfigureAwait(false);
            if (info is not { SignatureVerified: true })
            {
                return;
            }

            var dialog = provider.GetRequiredService<IDialogService>();
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => dialog.AlertAsync(
                "Update available",
                $"Dorado {info.Version} is available.\n\n{info.Notes}\n\n{info.Url}")).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
        }
    }

    private static void InitializeDatabase(IServiceProvider provider)
    {
        try
        {
            var factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using var ctx = factory.CreateDbContext();
            ctx.Database.EnsureCreated();

            // WAL journaling persists in the database file and keeps large library
            // scans/sync-group writes responsive.
            ctx.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");

            // FTS5 full-text search index over the collection catalog.
            SearchIndex.Ensure(ctx);

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

            ctx.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS TrackAudioFeatures (
                TrackId TEXT NOT NULL PRIMARY KEY,
                Bpm REAL NOT NULL,
                Energy REAL NOT NULL,
                Valence REAL NOT NULL,
                Acousticness REAL NOT NULL,
                Danceability REAL NOT NULL,
                SpectralCentroid REAL NOT NULL,
                AnalyzedAtUtc TEXT NOT NULL)");

            ctx.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS Reviews (
                Id TEXT NOT NULL PRIMARY KEY,
                AlbumId TEXT,
                AlbumTitle TEXT NOT NULL,
                ArtistName TEXT NOT NULL,
                Body TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL)");

            // Release the connection before the cleanup pass opens its own context.
            ctx.Dispose();
        }
        catch
        {
            // Database initialization is best-effort; the app still starts with defaults.
        }

        try
        {
            PurgeLegacyDemoData(provider);
        }
        catch
        {
            // Cleanup is best-effort.
        }
    }

    /// <summary>
    /// Removes placeholder rows left by earlier builds that seeded a sample library
    /// (tracks with no on-disk source, plus their orphaned albums/artists/history).
    /// </summary>
    private static void PurgeLegacyDemoData(IServiceProvider provider)
    {
        Task.Run(() => provider.GetRequiredService<IMediaLibraryService>().ClearDemoDataAsync())
            .GetAwaiter()
            .GetResult();
    }
}
