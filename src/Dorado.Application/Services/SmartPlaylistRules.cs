using System.Globalization;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;

namespace Dorado.Application.Services;

/// <summary>
/// Pure smart (auto) playlist rule evaluation, Zune "Auto Playlist" style.
/// </summary>
public static class SmartPlaylistRules
{
    public static IReadOnlyList<Track> Evaluate(SmartPlaylist playlist, IEnumerable<Track> libraryTracks)
    {
        if (playlist == null)
        {
            return Array.Empty<Track>();
        }

        var matches = libraryTracks.Where(t => MatchesRules(playlist, t));

        matches = playlist.SortField switch
        {
            "ArtistName" => playlist.SortDescending ? matches.OrderByDescending(t => t.ArtistName) : matches.OrderBy(t => t.ArtistName),
            "PlayCount" => playlist.SortDescending ? matches.OrderByDescending(t => t.PlayCount) : matches.OrderBy(t => t.PlayCount),
            "LastPlayed" => playlist.SortDescending
                ? matches.OrderByDescending(t => t.LastPlayedAtUtc ?? DateTime.MinValue)
                : matches.OrderBy(t => t.LastPlayedAtUtc ?? DateTime.MinValue),
            "Year" => playlist.SortDescending ? matches.OrderByDescending(t => t.Year ?? 0) : matches.OrderBy(t => t.Year ?? 0),
            "Duration" => playlist.SortDescending ? matches.OrderByDescending(t => t.Duration) : matches.OrderBy(t => t.Duration),
            _ => playlist.SortDescending ? matches.OrderByDescending(t => t.Title) : matches.OrderBy(t => t.Title)
        };

        var result = matches.ToList();
        return playlist.TrackLimit > 0 ? result.Take(playlist.TrackLimit).ToList() : result;
    }

    private static bool MatchesRules(SmartPlaylist playlist, Track track)
    {
        if (playlist.Rules.Count == 0)
        {
            return true;
        }

        return playlist.Match == SmartPlaylistMatch.All
            ? playlist.Rules.All(r => MatchesRule(r, track))
            : playlist.Rules.Any(r => MatchesRule(r, track));
    }

    private static bool MatchesRule(SmartPlaylistRule rule, Track track)
    {
        return rule.Field switch
        {
            SmartRuleField.Genre => MatchesString(rule, track.Genre),
            SmartRuleField.Artist => MatchesString(rule, track.ArtistName),
            SmartRuleField.Album => MatchesString(rule, track.AlbumTitle),
            SmartRuleField.Rating => MatchesNumber(rule, (int)track.Rating),
            SmartRuleField.PlayCount => MatchesNumber(rule, track.PlayCount),
            SmartRuleField.Year => MatchesNumber(rule, track.Year ?? 0),
            SmartRuleField.LastPlayed => MatchesLastPlayed(rule, track.LastPlayedAtUtc),
            _ => false
        };
    }

    private static bool MatchesString(SmartPlaylistRule rule, string actual)
    {
        actual ??= string.Empty;
        var value = rule.Value?.Trim() ?? string.Empty;
        var comparison = StringComparison.OrdinalIgnoreCase;

        return rule.Operator switch
        {
            SmartRuleOperator.Is => actual.Equals(value, comparison),
            SmartRuleOperator.IsNot => !actual.Equals(value, comparison),
            SmartRuleOperator.Contains => actual.Contains(value, comparison),
            SmartRuleOperator.NotContains => !actual.Contains(value, comparison),
            _ => false
        };
    }

    private static bool MatchesNumber(SmartPlaylistRule rule, int actual)
    {
        if (!int.TryParse(rule.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        return rule.Operator switch
        {
            SmartRuleOperator.Is => actual == value,
            SmartRuleOperator.GreaterThan => actual > value,
            SmartRuleOperator.LessThan => actual < value,
            _ => false
        };
    }

    private static bool MatchesLastPlayed(SmartPlaylistRule rule, DateTime? lastPlayedUtc)
    {
        if (rule.Operator != SmartRuleOperator.WithinLastDays
            || !int.TryParse(rule.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days))
        {
            return false;
        }

        return lastPlayedUtc.HasValue && lastPlayedUtc.Value >= DateTime.UtcNow.AddDays(-days);
    }
}
