using Dorado.Application.Interfaces;
using Dorado.Domain.Models;

namespace Dorado.Application.Services;

public class UserStatsService : IUserStatsService
{
    private readonly IMediaLibraryService _libraryService;
    private readonly IReviewService? _reviewService;
    private readonly ICloudSocialService? _cloudSocial;
    private readonly List<PlayHistoryEntry> _sessionPlays = new();

    public UserStatsService(
        IMediaLibraryService libraryService,
        IReviewService? reviewService = null,
        ICloudSocialService? cloudSocial = null)
    {
        _libraryService = libraryService;
        _reviewService = reviewService;
        _cloudSocial = cloudSocial;
    }

    public async Task<ZuneProfile> GetProfileAsync()
    {
        var history = await _libraryService.GetRecentHistoryAsync(100);
        var allPlays = history.Concat(_sessionPlays).ToList();
        var totalSecs = allPlays.Sum(p => p.DurationPlayed.TotalSeconds);

        return new ZuneProfile
        {
            ZuneTag = "ZuneFan_2006",
            StatusMessage = "Bringing back the authentic Metro experience",
            MemberSinceUtc = new DateTime(2006, 11, 14), // Zune launch date!
            TotalTracksPlayed = allPlays.Count,
            TotalListeningTime = TimeSpan.FromSeconds(totalSecs),
            AvatarUri = string.Empty
        };
    }

    public async Task<IReadOnlyList<TopArtistStat>> GetTopArtistsAsync(int count = 5)
    {
        var history = await _libraryService.GetRecentHistoryAsync(100);
        var allPlays = history.Concat(_sessionPlays).ToList();
        if (allPlays.Count == 0)
        {
            return new List<TopArtistStat>
            {
                new() { ArtistName = "Rush", PlayCount = 14, Percentage = 40.0 },
                new() { ArtistName = "Daft Punk", PlayCount = 11, Percentage = 31.0 },
                new() { ArtistName = "Pink Floyd", PlayCount = 6, Percentage = 17.0 },
                new() { ArtistName = "Fleetwood Mac", PlayCount = 4, Percentage = 12.0 }
            };
        }

        var groups = allPlays
            .GroupBy(p => p.ArtistName)
            .OrderByDescending(g => g.Count())
            .Take(count)
            .ToList();

        var total = allPlays.Count;
        return groups.Select(g => new TopArtistStat
        {
            ArtistName = g.Key,
            PlayCount = g.Count(),
            Percentage = total > 0 ? Math.Round((double)g.Count() / total * 100.0, 1) : 0
        }).ToList();
    }

    public async Task<IReadOnlyList<ZuneBadge>> GetBadgesAsync()
    {
        var history = await _libraryService.GetRecentHistoryAsync(100);
        var allPlays = history.Concat(_sessionPlays).ToList();
        var totalPlays = allPlays.Count;
        var listeningHours = allPlays.Sum(p => p.DurationPlayed.TotalHours);

        var topAlbumPlays = allPlays.GroupBy(p => p.AlbumTitle).Select(g => g.Count()).DefaultIfEmpty(0).Max();
        var topArtistPlays = allPlays.GroupBy(p => p.ArtistName).Select(g => g.Count()).DefaultIfEmpty(0).Max();

        var reviewCount = _reviewService is null ? 0 : await _reviewService.GetReviewCountAsync();
        var playlistCount = (await _libraryService.GetAllPlaylistsAsync()).Count;
        var pinnedCount = (await _libraryService.GetPinnedAlbumsAsync()).Count;

        return new List<ZuneBadge>
        {
            ReputationEngine.AlbumPowerListener(topAlbumPlays),
            ReputationEngine.ArtistPowerListener(topArtistPlays),
            ReputationEngine.Milestone(totalPlays),
            ReputationEngine.Marathon(listeningHours),
            ReputationEngine.Reviewer(reviewCount),
            ReputationEngine.Curator(playlistCount + pinnedCount)
        };
    }

    public async Task RecordTrackPlayedAsync(Track track)
    {
        _sessionPlays.Add(new PlayHistoryEntry
        {
            TrackId = track.Id,
            TrackTitle = track.Title,
            ArtistName = track.ArtistName,
            AlbumTitle = track.AlbumTitle,
            PlayedAtUtc = DateTime.UtcNow,
            DurationPlayed = track.Duration,
            Completed = true
        });

        // Mirror the listen to the cloud so the live Zune Card reflects play
        // history across devices. Never let a cloud failure break playback.
        if (_cloudSocial is { IsEnabled: true })
        {
            try
            {
                await _cloudSocial.RecordListenAsync(track.ArtistName, track.Title, track.AlbumTitle).ConfigureAwait(false);
            }
            catch
            {
                // Offline / token expired: the local history is authoritative.
            }
        }
    }
}
