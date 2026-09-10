using System;
using System.Collections.Generic;

namespace Dorado.Domain.Models;

public enum SmartPlaylistMatch
{
    All,
    Any
}

public enum SmartRuleField
{
    Genre,
    Artist,
    Album,
    Rating,
    PlayCount,
    LastPlayed,
    Year
}

public enum SmartRuleOperator
{
    Is,
    IsNot,
    Contains,
    NotContains,
    GreaterThan,
    LessThan,
    WithinLastDays
}

public class SmartPlaylistRule
{
    public SmartRuleField Field { get; set; } = SmartRuleField.Genre;
    public SmartRuleOperator Operator { get; set; } = SmartRuleOperator.Is;
    public string Value { get; set; } = string.Empty;
}

public class SmartPlaylist
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SmartPlaylistMatch Match { get; set; } = SmartPlaylistMatch.All;
    public int TrackLimit { get; set; } = 50;
    public string SortField { get; set; } = "Title";
    public bool SortDescending { get; set; }
    public List<SmartPlaylistRule> Rules { get; set; } = new();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
