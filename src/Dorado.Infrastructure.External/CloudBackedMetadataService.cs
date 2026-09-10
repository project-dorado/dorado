using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.Domain.Models;
using DoradoCloud.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dorado.Infrastructure.External;

/// <summary>
/// Decorator over <see cref="IExternalMetadataService"/> that prefers the
/// Dorado Cloud catalog and artwork CDN when enabled and reachable, falling back
/// to the inner (direct MusicBrainz / Cover Art Archive / Fanart.tv) service
/// when the cloud is disabled, misconfigured, or transiently unreachable.
///
/// The two services that have a 1:1 cloud mapping are short-circuited here:
/// <see cref="FetchArtistMetadataAsync"/> resolves through the cloud's
/// <c>GET /v1/catalog/artists/{mbid}</c> + <c>GET /v1/catalog/search</c>, and
/// <see cref="FindAlbumArtworkAsync"/> through <c>GET /v1/artwork/front/{mbid}</c>.
/// Everything else (track matches, lyrics, fallback artist backgrounds) keeps
/// using the inner service — the cloud does not (yet) back those.
/// </summary>
public sealed class CloudBackedMetadataService : IExternalMetadataService
{
    private readonly IExternalMetadataService _inner;
    private readonly DoradoCloudClient _cloud;
    private readonly Func<AppSettings> _settings;
    private readonly ILogger<CloudBackedMetadataService> _logger;

    public CloudBackedMetadataService(
        IExternalMetadataService inner,
        DoradoCloudClient cloud,
        Func<AppSettings> settings,
        ILogger<CloudBackedMetadataService>? logger = null)
    {
        _inner = inner;
        _cloud = cloud;
        _settings = settings;
        _logger = logger ?? NullLogger<CloudBackedMetadataService>.Instance;
    }

    public async Task<ArtistMetadataResult?> FetchArtistMetadataAsync(
        string artistName,
        CancellationToken cancellationToken = default)
    {
        var (enabled, _) = ResolveCloud();
        if (enabled)
        {
            try
            {
                var hit = await _cloud.CatalogSearchAsync(artistName, "artist", limit: 1, cancellationToken)
                    .ConfigureAwait(false);
                var first = hit?.Items.FirstOrDefault();
                if (first is { Mbid: { Length: > 0 } })
                {
                    var detail = await _cloud.CatalogArtistAsync(first.Mbid, cancellationToken)
                        .ConfigureAwait(false);
                    if (detail is not null)
                    {
                        return new ArtistMetadataResult
                        {
                            Name = string.IsNullOrWhiteSpace(detail.Name) ? artistName : detail.Name,
                            MusicBrainzId = detail.Mbid,
                            Biography = detail.Disambiguation,
                            BiographySource = "Dorado Cloud (MusicBrainz via Catalog)",
                            ThumbnailUrl = detail.CoverArtUrl,
                            BackgroundImageUrls = detail.CoverArtUrl is null
                                ? new List<string>()
                                : new List<string> { detail.CoverArtUrl },
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cloud catalog lookup failed for {Artist}; falling back.", artistName);
            }
        }

        return await _inner.FetchArtistMetadataAsync(artistName, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<string>> FetchArtistBackgroundUrlsAsync(
        string artistName, string fanartTvApiKey, CancellationToken cancellationToken = default)
        => _inner.FetchArtistBackgroundUrlsAsync(artistName, fanartTvApiKey, cancellationToken);

    public Task<IReadOnlyList<string>> FetchFallbackArtistBackgroundUrlsAsync(
        string artistName, string? communityBaseUrl, CancellationToken cancellationToken = default)
        => _inner.FetchFallbackArtistBackgroundUrlsAsync(artistName, communityBaseUrl, cancellationToken);

    public async Task<AlbumArtworkResult?> FindAlbumArtworkAsync(
        string artistName, string albumTitle, int? year = null, CancellationToken cancellationToken = default)
    {
        var (enabled, _) = ResolveCloud();
        if (enabled)
        {
            try
            {
                var hit = await _cloud.CatalogSearchAsync($"{artistName} {albumTitle}", "release-group", limit: 5, cancellationToken)
                    .ConfigureAwait(false);
                var candidate = hit?.Items.FirstOrDefault(i =>
                    string.Equals(i.Artist, artistName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(i.Title, albumTitle, StringComparison.OrdinalIgnoreCase));
                if (candidate is { Mbid: { Length: > 0 } })
                {
                    var bytes = await _cloud.ArtworkFrontAsync(candidate.Mbid, size: 500, cancellationToken)
                        .ConfigureAwait(false);
                    if (bytes is { Length: > 0 })
                    {
                        return new AlbumArtworkResult
                        {
                            AlbumTitle = albumTitle,
                            ArtistName = artistName,
                            MusicBrainzReleaseGroupId = candidate.Mbid,
                            ArtworkUrl = $"https://dorado-cloud.example/v1/artwork/front/{candidate.Mbid}?size=500",
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cloud artwork lookup failed for {Artist}/{Album}; falling back.", artistName, albumTitle);
            }
        }

        return await _inner.FindAlbumArtworkAsync(artistName, albumTitle, year, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<TrackMatchCandidate>> FindTrackMatchesAsync(
        string artistName, string albumTitle, IReadOnlyList<Track> tracks, CancellationToken cancellationToken = default)
        => _inner.FindTrackMatchesAsync(artistName, albumTitle, tracks, cancellationToken);

    public Task<LyricsResult?> FetchLyricsAsync(
        string artistName, string trackTitle, TimeSpan? duration = null, CancellationToken cancellationToken = default)
        => _inner.FetchLyricsAsync(artistName, trackTitle, duration, cancellationToken);

    private (bool Enabled, AppSettings Snapshot) ResolveCloud()
    {
        var snapshot = _settings();
        var enabled = snapshot.CloudEnabled && !string.IsNullOrWhiteSpace(snapshot.CloudBaseUrl);
        return (enabled, snapshot);
    }
}
