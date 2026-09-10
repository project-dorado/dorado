using Dorado.Domain.Models;

namespace Dorado.Application.Services;

/// <summary>
/// Computes Zune-style reputation badges with Bronze/Silver/Gold tiers. Inputs are
/// monotonic (append-only play history, authored reviews, created playlists/pins), so a
/// reached tier never regresses — matching Zune's "badges did not expire" rule without
/// persisting unlock state.
/// </summary>
public static class ReputationEngine
{
    public static ZuneBadge AlbumPowerListener(int topAlbumPlays)
        => Build("badge_album_power", "Album Power Listener", "Listening",
            "Listen to one album again and again.", topAlbumPlays, 10, 30, 75);

    public static ZuneBadge ArtistPowerListener(int topArtistPlays)
        => Build("badge_artist_power", "Artist Power Listener", "Listening",
            "Listen to one artist again and again.", topArtistPlays, 25, 75, 200);

    public static ZuneBadge Milestone(int totalPlays)
        => Build("badge_milestone", "Milestone", "Milestone",
            "Log track plays across your collection.", totalPlays, 100, 1000, 5000);

    public static ZuneBadge Marathon(double listeningHours)
        => Build("badge_marathon", "Marathon", "Milestone",
            "Accumulate listening hours.", (int)listeningHours, 10, 100, 500);

    public static ZuneBadge Reviewer(int reviewCount)
        => Build("badge_reviews", "Reviewer", "Reviews",
            "Write reviews for albums in your collection.", reviewCount, 1, 5, 20);

    public static ZuneBadge Curator(int contributions)
        => Build("badge_curator", "Curator", "Forums",
            "Contribute playlists and pinned albums to your library.", contributions, 3, 10, 25);

    public static ZuneBadge Build(
        string id, string title, string category, string description,
        int value, int bronze, int silver, int gold)
    {
        var tier = value >= gold ? BadgeTier.Gold
            : value >= silver ? BadgeTier.Silver
            : value >= bronze ? BadgeTier.Bronze
            : BadgeTier.None;

        var next = tier switch
        {
            BadgeTier.None => bronze,
            BadgeTier.Bronze => silver,
            BadgeTier.Silver => gold,
            _ => gold
        };

        return new ZuneBadge
        {
            Id = id,
            Title = title,
            Category = category,
            Description = description,
            Tier = tier,
            Progress = value,
            NextThreshold = next,
            IsUnlocked = tier != BadgeTier.None,
            UnlockedAtUtc = tier != BadgeTier.None ? DateTime.UtcNow : null
        };
    }
}
