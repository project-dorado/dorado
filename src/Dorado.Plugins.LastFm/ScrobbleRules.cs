namespace Dorado.Plugins.LastFm;

/// <summary>Last.fm's scrobble eligibility rules, kept pure for testing.</summary>
public static class ScrobbleRules
{
    public const int MinimumTrackSeconds = 30;
    public const int MaximumThresholdSeconds = 240;

    public static bool ShouldScrobble(TimeSpan trackDuration, TimeSpan elapsed)
    {
        if (trackDuration.TotalSeconds < MinimumTrackSeconds)
        {
            return false;
        }

        var thresholdSeconds = Math.Min(trackDuration.TotalSeconds / 2.0, MaximumThresholdSeconds);
        return elapsed.TotalSeconds >= thresholdSeconds;
    }
}
