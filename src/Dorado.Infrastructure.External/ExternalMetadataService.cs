using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.Domain.Models;

namespace Dorado.Infrastructure.External;

/// <summary>
/// Orchestrates the individual keyless/keyed metadata providers into one resilient facade.
/// All failures degrade gracefully to null results; results are memoized for 24 hours.
/// </summary>
public sealed class ExternalMetadataService : IExternalMetadataService, IArtistRelationshipService
{
    private const string UserAgent = "Dorado/1.0 (+https://github.com/project-dorado/dorado)";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private readonly MusicBrainzClient _musicBrainz;
    private readonly WikipediaClient _wikipedia;
    private readonly CoverArtArchiveClient _coverArt;
    private readonly FanartTvClient _fanart;
    private readonly LrcLibClient _lrcLib;
    private readonly CommunityArtistImageProvider _communityImages;
    private readonly ConcurrentDictionary<string, (DateTimeOffset ExpiresAtUtc, object? Value)> _cache = new();

    public ExternalMetadataService(HttpMessageHandler? innerHandler = null, TimeSpan? rateLimitInterval = null)
    {
        var shared = innerHandler ?? new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true
        };

        var interval = rateLimitInterval ?? TimeSpan.FromSeconds(1.05);
        _musicBrainz = new MusicBrainzClient(CreateClient(shared, interval));
        _wikipedia = new WikipediaClient(CreateClient(shared, rateLimitInterval is null ? TimeSpan.FromMilliseconds(50) : interval));
        _coverArt = new CoverArtArchiveClient(CreateClient(shared, interval));
        _fanart = new FanartTvClient(CreateClient(shared, rateLimitInterval is null ? TimeSpan.FromMilliseconds(100) : interval));
        _lrcLib = new LrcLibClient(CreateClient(shared, rateLimitInterval is null ? TimeSpan.FromMilliseconds(50) : interval));
        _communityImages = new CommunityArtistImageProvider(CreateClient(shared, rateLimitInterval is null ? TimeSpan.FromMilliseconds(100) : interval));
    }

    private static HttpClient CreateClient(HttpMessageHandler inner, TimeSpan minimumInterval)
    {
        var client = new HttpClient(new RateLimitedHttpMessageHandler(inner, minimumInterval))
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    }

    public async Task<IReadOnlyList<string>> GetRelatedArtistsAsync(
        string artistName,
        int limit = 8,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artistName))
        {
            return Array.Empty<string>();
        }

        var key = $"related:{artistName.ToLowerInvariant()}:{limit}";
        if (_cache.TryGetValue(key, out var hit) && hit.ExpiresAtUtc > DateTimeOffset.UtcNow)
        {
            return hit.Value as IReadOnlyList<string> ?? Array.Empty<string>();
        }

        var names = await _musicBrainz.LookupRelatedArtistsAsync(artistName, limit, cancellationToken).ConfigureAwait(false);
        _cache[key] = (DateTimeOffset.UtcNow.Add(CacheTtl), names);
        return names;
    }

    public async Task<ArtistMetadataResult?> FetchArtistMetadataAsync(string artistName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artistName))
        {
            return null;
        }

        var cacheKey = $"artist:{artistName.Trim().ToLowerInvariant()}";
        if (TryGetCached<ArtistMetadataResult>(cacheKey, out var cached))
        {
            return cached;
        }

        ArtistMetadataResult? result = null;
        try
        {
            var search = await _musicBrainz.SearchArtistAsync(artistName.Trim(), cancellationToken).ConfigureAwait(false);
            var candidate = new ArtistMetadataResult
            {
                Name = artistName,
                MusicBrainzId = search is { MbId: not null } artistMatch ? artistMatch.MbId : null
            };

            var biography = await _wikipedia.FetchSummaryExtractAsync(artistName.Trim(), cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(biography))
            {
                candidate.Biography = biography;
                candidate.BiographySource = "Wikipedia";
            }

            result = candidate;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
        }

        SetCached(cacheKey, result);
        return result;
    }

    public async Task<IReadOnlyList<string>> FetchArtistBackgroundUrlsAsync(string artistName, string fanartTvApiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artistName) || string.IsNullOrWhiteSpace(fanartTvApiKey))
        {
            return Array.Empty<string>();
        }

        var cacheKey = $"backgrounds:{artistName.Trim().ToLowerInvariant()}";
        if (TryGetCached<string[]>(cacheKey, out var cached))
        {
            return cached ?? Array.Empty<string>();
        }

        var urls = Array.Empty<string>();
        try
        {
            var search = await _musicBrainz.SearchArtistAsync(artistName.Trim(), cancellationToken).ConfigureAwait(false);
            if (search is { MbId: not null } artistMatch)
            {
                urls = (await _fanart.FetchArtistBackgroundsAsync(artistMatch.MbId!, fanartTvApiKey, cancellationToken).ConfigureAwait(false)).ToArray();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
        }

        SetCached(cacheKey, urls);
        return urls;
    }

    public async Task<IReadOnlyList<string>> FetchFallbackArtistBackgroundUrlsAsync(string artistName, string? communityBaseUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artistName) || string.IsNullOrWhiteSpace(communityBaseUrl))
        {
            return Array.Empty<string>();
        }

        var cacheKey = $"fallback-backgrounds:{artistName.Trim().ToLowerInvariant()}|{communityBaseUrl.Trim().ToLowerInvariant()}";
        if (TryGetCached<string[]>(cacheKey, out var cached))
        {
            return cached ?? Array.Empty<string>();
        }

        var urls = (await _communityImages.GetBackgroundUrlsAsync(communityBaseUrl, artistName, cancellationToken).ConfigureAwait(false)).ToArray();
        SetCached(cacheKey, urls);
        return urls;
    }

    public async Task<AlbumArtworkResult?> FindAlbumArtworkAsync(string artistName, string albumTitle, int? year = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artistName) || string.IsNullOrWhiteSpace(albumTitle))
        {
            return null;
        }

        var cacheKey = $"album:{artistName.Trim().ToLowerInvariant()}|{albumTitle.Trim().ToLowerInvariant()}";
        if (TryGetCached<AlbumArtworkResult>(cacheKey, out var cached))
        {
            return cached;
        }

        AlbumArtworkResult? result = null;
        try
        {
            var search = await _musicBrainz.SearchReleaseGroupAsync(artistName.Trim(), albumTitle.Trim(), cancellationToken).ConfigureAwait(false);
            if (search is { MbId: not null } releaseGroupMatch)
            {
                result = new AlbumArtworkResult
                {
                    AlbumTitle = !string.IsNullOrEmpty(releaseGroupMatch.Title) ? releaseGroupMatch.Title : albumTitle,
                    ArtistName = artistName,
                    MusicBrainzReleaseGroupId = releaseGroupMatch.MbId,
                    ArtworkUrl = _coverArt.BuildFrontUrl(releaseGroupMatch.MbId!)
                };
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
        }

        SetCached(cacheKey, result);
        return result;
    }

    public async Task<IReadOnlyList<TrackMatchCandidate>> FindTrackMatchesAsync(string artistName, string albumTitle, IReadOnlyList<Track> tracks, CancellationToken cancellationToken = default)
    {
        var candidates = new List<TrackMatchCandidate>();
        if (tracks.Count == 0 || string.IsNullOrWhiteSpace(artistName))
        {
            return candidates;
        }

        foreach (var track in tracks)
        {
            var cacheKey = $"recording:{artistName.Trim().ToLowerInvariant()}|{track.Title.Trim().ToLowerInvariant()}";
            if (TryGetCached<TrackMatchCandidate>(cacheKey, out var cached))
            {
                if (cached != null)
                {
                    candidates.Add(cached);
                }

                continue;
            }

            TrackMatchCandidate? candidate = null;
            try
            {
                var match = await _musicBrainz.SearchRecordingAsync(artistName.Trim(), track.Title.Trim(), cancellationToken).ConfigureAwait(false);
                if (match is { MbId: not null } recording && recording.Score >= 50)
                {
                    candidate = new TrackMatchCandidate
                    {
                        TrackId = track.Id,
                        OriginalTitle = track.Title,
                        MatchedTitle = recording.Title ?? track.Title,
                        MatchedArtist = recording.ArtistName ?? artistName,
                        MatchedDurationMs = recording.LengthMs,
                        MusicBrainzRecordingId = recording.MbId,
                        Score = recording.Score
                    };
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
            }

            SetCached(cacheKey, candidate);
            if (candidate != null)
            {
                candidates.Add(candidate);
            }
        }

        return candidates;
    }

    public async Task<LyricsResult?> FetchLyricsAsync(string artistName, string trackTitle, TimeSpan? duration = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artistName) || string.IsNullOrWhiteSpace(trackTitle))
        {
            return null;
        }

        var cacheKey = $"lyrics:{artistName.Trim().ToLowerInvariant()}|{trackTitle.Trim().ToLowerInvariant()}";
        if (TryGetCached<LyricsResult>(cacheKey, out var cached))
        {
            return cached;
        }

        LyricsResult? result = null;
        try
        {
            result = await _lrcLib.FetchLyricsAsync(artistName, trackTitle, duration, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
        }

        SetCached(cacheKey, result);
        return result;
    }

    private sealed record CacheBox(object? Value);

    private bool TryGetCached<T>(string cacheKey, out T? value)
    {
        value = default;
        if (_cache.TryGetValue(cacheKey, out var entry))
        {
            if (DateTimeOffset.UtcNow < entry.ExpiresAtUtc && entry.Value is CacheBox box)
            {
                if (box.Value is T typed)
                {
                    value = typed;
                }

                return true;
            }

            _cache.TryRemove(cacheKey, out _);
        }

        return false;
    }

    private void SetCached(string cacheKey, object? value)
    {
        _cache[cacheKey] = (DateTimeOffset.UtcNow + CacheTtl, new CacheBox(value));
    }
}
