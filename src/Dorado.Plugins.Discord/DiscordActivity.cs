using System.Text.Json.Serialization;
using Dorado.Plugins.Protocol.Dto;

namespace Dorado.Plugins.Discord;

public sealed class DiscordActivity
{
    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Details { get; init; }

    [JsonPropertyName("state")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? State { get; init; }

    [JsonPropertyName("timestamps")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DiscordTimestamps? Timestamps { get; init; }
}

public sealed class DiscordTimestamps
{
    [JsonPropertyName("start")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Start { get; init; }
}

public sealed class DiscordActivityArgs
{
    [JsonPropertyName("pid")]
    public int Pid { get; init; }

    [JsonPropertyName("activity")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DiscordActivity? Activity { get; init; }
}

public sealed class DiscordSetActivity
{
    [JsonPropertyName("cmd")]
    public string Cmd => "SET_ACTIVITY";

    [JsonPropertyName("args")]
    public DiscordActivityArgs Args { get; init; } = new();

    [JsonPropertyName("nonce")]
    public string Nonce { get; init; } = Guid.NewGuid().ToString();
}

/// <summary>Builds the Discord SET_ACTIVITY payload from a Dorado track.</summary>
public static class DiscordActivityBuilder
{
    public static DiscordActivity? Build(TrackDto? track, bool isPlaying, long? startTimestampMs)
    {
        if (track is null)
        {
            return null;
        }

        var details = string.IsNullOrWhiteSpace(track.Artist)
            ? track.Title
            : $"{track.Title} — {track.Artist}";

        var state = isPlaying ? "Playing" : "Paused";

        return new DiscordActivity
        {
            Details = Truncate(details, 128),
            State = state,
            Timestamps = isPlaying && startTimestampMs is { } start ? new DiscordTimestamps { Start = start } : null
        };
    }

    public static DiscordSetActivity BuildSetActivity(DiscordActivity? activity, int processId)
        => new() { Args = new DiscordActivityArgs { Pid = processId, Activity = activity } };

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];
}
