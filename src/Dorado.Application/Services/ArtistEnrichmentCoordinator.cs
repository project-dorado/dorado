using System.Collections.Concurrent;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;

namespace Dorado.Application.Services;

/// <summary>
/// Orchestrates artist enrichment (biography + backdrop imagery) for the Now Playing experience.
/// Requests are debounced and single-flighted; failures are sticky for the session so a flaky
/// network never causes repeated stalls. Results are memoized and persisted back to the library.
/// </summary>
public sealed class ArtistEnrichmentCoordinator : IArtistEnrichmentService, IDisposable
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(300);
    private static readonly int MaxBackdropDownloads = 4;

    private readonly IExternalMetadataService _metadataService;
    private readonly IArtworkCacheService _artworkCache;
    private readonly ISettingsStore _settingsStore;
    private readonly IMediaLibraryService? _libraryService;

    private readonly System.Timers.Timer _debounceTimer;
    private readonly ConcurrentDictionary<string, ArtistEnrichmentSnapshot> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _inFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, LyricsResult?> _lyricsCache = new(StringComparer.OrdinalIgnoreCase);

    private string? _pendingArtist;

    public ArtistEnrichmentCoordinator(
        IExternalMetadataService metadataService,
        IArtworkCacheService artworkCache,
        ISettingsStore settingsStore,
        IMediaLibraryService? libraryService = null)
    {
        _metadataService = metadataService;
        _artworkCache = artworkCache;
        _settingsStore = settingsStore;
        _libraryService = libraryService;

        _debounceTimer = new System.Timers.Timer(DebounceInterval.TotalMilliseconds) { AutoReset = false };
        _debounceTimer.Elapsed += (_, _) => ProcessPendingArtist();
    }

    public event EventHandler<ArtistEnrichmentSnapshot>? EnrichmentCompleted;

    public ArtistEnrichmentSnapshot? GetCached(string artistName)
    {
        if (string.IsNullOrWhiteSpace(artistName))
        {
            return null;
        }

        return _cache.TryGetValue(artistName.Trim(), out var snapshot) ? snapshot : null;
    }

    public void RequestEnrichment(string artistName)
    {
        if (!IsEnrichmentEnabled())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(artistName) || _inFlight.ContainsKey(artistName.Trim()))
        {
            return;
        }

        _pendingArtist = artistName.Trim();
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    public async Task<LyricsResult?> GetLyricsAsync(string artistName, string trackTitle, TimeSpan? duration = null, CancellationToken cancellationToken = default)
    {
        if (!IsLyricsEnabled())
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(artistName) || string.IsNullOrWhiteSpace(trackTitle))
        {
            return null;
        }

        var cacheKey = $"{artistName.Trim()}|{trackTitle.Trim()}";
        if (_lyricsCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        LyricsResult? result = null;
        try
        {
            result = await _metadataService.FetchLyricsAsync(artistName, trackTitle, duration, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            result = null;
        }

        _lyricsCache[cacheKey] = result;
        return result;
    }

    private bool IsEnrichmentEnabled()
    {
        var settings = _settingsStore.Load();
        return settings.MusicBrainzEnabled && settings.AutoFetchMetadata;
    }

    private bool IsLyricsEnabled()
    {
        var settings = _settingsStore.Load();
        return settings.MusicBrainzEnabled && settings.AutoFetchMetadata && settings.LrcLibEnabled;
    }

    private void ProcessPendingArtist()
    {
        var artistName = _pendingArtist;
        _pendingArtist = null;
        if (string.IsNullOrWhiteSpace(artistName) || !_inFlight.TryAdd(artistName, 0))
        {
            return;
        }

        _ = EnrichAsync(artistName);
    }

    private async Task EnrichAsync(string artistName)
    {
        try
        {
            var settings = _settingsStore.Load();

            var metadata = await _metadataService.FetchArtistMetadataAsync(artistName).ConfigureAwait(false);
            if (metadata == null)
            {
                return;
            }

            var backdrops = new List<string>();
            if (settings.AutoDownloadArtistArt && !string.IsNullOrWhiteSpace(settings.FanartTvApiKey))
            {
                var backgroundUrls = await _metadataService.FetchArtistBackgroundUrlsAsync(artistName, settings.FanartTvApiKey).ConfigureAwait(false);
                foreach (var url in backgroundUrls.Take(MaxBackdropDownloads))
                {
                    var localPath = await _artworkCache.GetOrDownloadAsync(url).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(localPath))
                    {
                        backdrops.Add(localPath);
                    }
                }
            }

            if (backdrops.Count == 0
                && settings.ArtistImageFallbackEnabled
                && !string.IsNullOrWhiteSpace(settings.CommunityArtistImageBaseUrl))
            {
                var fallbackUrls = await _metadataService
                    .FetchFallbackArtistBackgroundUrlsAsync(artistName, settings.CommunityArtistImageBaseUrl)
                    .ConfigureAwait(false);
                foreach (var url in fallbackUrls.Take(MaxBackdropDownloads))
                {
                    var localPath = await _artworkCache.GetOrDownloadAsync(url).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(localPath))
                    {
                        backdrops.Add(localPath);
                    }
                }
            }

            if (_libraryService != null)
            {
                try
                {
                    await _libraryService.SetArtistMetadataAsync(
                        artistName,
                        metadata.Biography,
                        null,
                        backdrops.Count > 0 ? backdrops[0] : null,
                        metadata.MusicBrainzId).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or InvalidOperationException)
                {
                    // Library persistence is best-effort enrichment only.
                }
            }

            var snapshot = new ArtistEnrichmentSnapshot
            {
                ArtistName = artistName,
                Biography = metadata.Biography,
                BiographySource = metadata.BiographySource,
                BackdropLocalPaths = backdrops
            };
            _cache[artistName] = snapshot;
            EnrichmentCompleted?.Invoke(this, snapshot);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            // Network enrichment failures degrade silently to local content.
        }
        finally
        {
            _inFlight.TryRemove(artistName, out _);
        }
    }

    public void Dispose()
    {
        _debounceTimer.Dispose();
    }
}
