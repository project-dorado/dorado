namespace Dorado.Domain.Models;

public class ZuneProfile
{
    public string ZuneTag { get; set; } = "ZuneUser";
    public string StatusMessage { get; set; } = "Listening to great music on Dorado";
    public DateTime MemberSinceUtc { get; set; } = DateTime.UtcNow.AddMonths(-6);
    public int TotalTracksPlayed { get; set; }
    public TimeSpan TotalListeningTime { get; set; }
    public string AvatarUri { get; set; } = "avares://Dorado.UI/Assets/Zune/Branding/ZUNEUSER.PNG";
}

public class TopArtistStat
{
    public string ArtistName { get; set; } = string.Empty;
    public int PlayCount { get; set; }
    public double Percentage { get; set; }
}

public enum BadgeTier
{
    None,
    Bronze,
    Silver,
    Gold
}

public class ZuneBadge
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Milestone";
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedAtUtc { get; set; }
    public BadgeTier Tier { get; set; }
    public int Progress { get; set; }
    public int NextThreshold { get; set; }
    public string IconUri { get; set; } = "avares://Dorado.UI/Assets/Zune/Social/PROFILE.BADGE.SEAL.PNG";

    public string TierText => Tier == BadgeTier.None ? "LOCKED" : Tier.ToString().ToUpperInvariant();
    public string ProgressText => $"{Math.Min(Progress, NextThreshold)} / {NextThreshold}";
}

/// <summary>Local, user-authored album review (a substitute for the dead Zune forums/reviews).</summary>
public class Review
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? AlbumId { get; set; }
    public string AlbumTitle { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
