using Dorado.Application.Models;
using Dorado.Domain.Models;

namespace Dorado.Application.Interfaces;

public interface IExternalMetadataService
{
    Task<ArtistMetadataResult?> FetchArtistMetadataAsync(string artistName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> FetchArtistBackgroundUrlsAsync(string artistName, string fanartTvApiKey, CancellationToken cancellationToken = default);

    /// <summary>Community-mirror fallback artwork when Fanart.tv yields nothing.</summary>
    Task<IReadOnlyList<string>> FetchFallbackArtistBackgroundUrlsAsync(string artistName, string? communityBaseUrl, CancellationToken cancellationToken = default);

    Task<AlbumArtworkResult?> FindAlbumArtworkAsync(string artistName, string albumTitle, int? year = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrackMatchCandidate>> FindTrackMatchesAsync(string artistName, string albumTitle, IReadOnlyList<Track> tracks, CancellationToken cancellationToken = default);

    Task<LyricsResult?> FetchLyricsAsync(string artistName, string trackTitle, TimeSpan? duration = null, CancellationToken cancellationToken = default);
}
