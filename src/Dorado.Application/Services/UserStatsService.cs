using Dorado.Application.Interfaces;
using Dorado.Domain.Models;

namespace Dorado.Application.Services;

public class UserStatsService : IUserStatsService
{
    private readonly IMediaLibraryService _libraryService;
    private readonly List<PlayHistoryEntry> _sessionPlays = new();

    public UserStatsService(IMediaLibraryService libraryService)
    {
        _libraryService = libraryService;
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
            AvatarUri = "avares://Dorado.UI/Assets/Zune/Branding/ZUNEUSER.PNG"
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

        return new List<ZuneBadge>
        {
            new()
            {
                Id = "badge_early_adopter",
                Title = "Early Adopter",
                Description = "First to embrace the Dorado revolution.",
                Category = "Pioneer",
                IsUnlocked = true,
                UnlockedAtUtc = DateTime.UtcNow.AddDays(-10),
                IconUri = "avares://Dorado.UI/Assets/Zune/Social/PROFILE.BADGE.SEAL.PNG"
            },
            new()
            {
                Id = "badge_heavy_rotation",
                Title = "Heavy Rotation",
                Description = "Played an artist on continuous repeat.",
                Category = "Listening",
                IsUnlocked = totalPlays >= 3,
                UnlockedAtUtc = totalPlays >= 3 ? DateTime.UtcNow.AddDays(-2) : null,
                IconUri = "avares://Dorado.UI/Assets/Zune/Social/PROFILE.BADGE.SEAL.PNG"
            },
            new()
            {
                Id = "badge_centurion",
                Title = "Centurion",
                Description = "Logged over 100 track scrobbles.",
                Category = "Milestone",
                IsUnlocked = totalPlays >= 100,
                UnlockedAtUtc = totalPlays >= 100 ? DateTime.UtcNow : null,
                IconUri = "avares://Dorado.UI/Assets/Zune/Social/PROFILE.BADGE.SEAL.PNG"
            },
            new()
            {
                Id = "badge_smart_dj",
                Title = "Smart DJ Master",
                Description = "Generated intelligent mixes from library seeds.",
                Category = "Discovery",
                IsUnlocked = true,
                UnlockedAtUtc = DateTime.UtcNow.AddDays(-5),
                IconUri = "avares://Dorado.UI/Assets/Zune/Social/PROFILE.BADGE.SEAL.PNG"
            },
            new()
            {
                Id = "badge_vinyl_purist",
                Title = "Audiophile Purist",
                Description = "Preserving album art and gapless transitions.",
                Category = "Audio",
                IsUnlocked = true,
                UnlockedAtUtc = DateTime.UtcNow.AddDays(-1),
                IconUri = "avares://Dorado.UI/Assets/Zune/Social/PROFILE.BADGE.SEAL.PNG"
            }
        };
    }

    public Task RecordTrackPlayedAsync(Track track)
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
        return Task.CompletedTask;
    }
}
