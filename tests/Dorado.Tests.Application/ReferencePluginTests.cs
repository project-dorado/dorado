using System.Text.Json;
using Dorado.Plugins.Discord;
using Dorado.Plugins.LastFm;
using Dorado.Plugins.Protocol.Dto;

namespace Dorado.Tests.Application;

public class ScrobbleRulesTests
{
    [Fact]
    public void Short_tracks_are_never_scrobbled()
    {
        Assert.False(ScrobbleRules.ShouldScrobble(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20)));
    }

    [Fact]
    public void Half_duration_is_the_threshold()
    {
        var duration = TimeSpan.FromSeconds(200);
        Assert.False(ScrobbleRules.ShouldScrobble(duration, TimeSpan.FromSeconds(99)));
        Assert.True(ScrobbleRules.ShouldScrobble(duration, TimeSpan.FromSeconds(100)));
    }

    [Fact]
    public void Threshold_is_capped_at_four_minutes()
    {
        var duration = TimeSpan.FromMinutes(30);
        Assert.False(ScrobbleRules.ShouldScrobble(duration, TimeSpan.FromSeconds(239)));
        Assert.True(ScrobbleRules.ShouldScrobble(duration, TimeSpan.FromSeconds(240)));
    }
}

public class LastFmApiTests
{
    private static readonly Dictionary<string, string> Base = new()
    {
        ["artist"] = "Rush",
        ["track"] = "Subdivisions",
        ["api_key"] = "key",
        ["format"] = "json"
    };

    [Fact]
    public void Signature_is_deterministic_and_order_independent()
    {
        var reversed = Base.Reverse().ToDictionary(p => p.Key, p => p.Value);

        Assert.Equal(LastFmApi.Sign(Base, "secret"), LastFmApi.Sign(reversed, "secret"));
    }

    [Fact]
    public void Signature_ignores_format_and_changes_with_values()
    {
        var withFormat = new Dictionary<string, string>(Base) { ["format"] = "xml", ["callback"] = "cb" };
        Assert.Equal(LastFmApi.Sign(Base, "secret"), LastFmApi.Sign(withFormat, "secret"));

        var changed = new Dictionary<string, string>(Base) { ["track"] = "The Analog Kid" };
        Assert.NotEqual(LastFmApi.Sign(Base, "secret"), LastFmApi.Sign(changed, "secret"));
    }
}

public class DiscordActivityTests
{
    [Fact]
    public void Null_track_produces_no_activity()
    {
        Assert.Null(DiscordActivityBuilder.Build(null, isPlaying: false, startTimestampMs: null));
    }

    [Fact]
    public void Playing_activity_includes_timestamps_and_labels()
    {
        var activity = DiscordActivityBuilder.Build(
            new TrackDto { Title = "Digital Love", Artist = "Daft Punk" },
            isPlaying: true,
            startTimestampMs: 123456);

        Assert.NotNull(activity);
        Assert.Contains("Digital Love", activity!.Details);
        Assert.Contains("Daft Punk", activity.Details);
        Assert.Equal("Playing", activity.State);
        Assert.Equal(123456, activity.Timestamps!.Start);
    }

    [Fact]
    public void Paused_activity_omits_timestamps()
    {
        var activity = DiscordActivityBuilder.Build(
            new TrackDto { Title = "Voyager", Artist = "Daft Punk" },
            isPlaying: false,
            startTimestampMs: 999);

        Assert.NotNull(activity);
        Assert.Equal("Paused", activity!.State);
        Assert.Null(activity.Timestamps);
    }

    [Fact]
    public void Set_activity_payload_serializes_cmd_and_pid()
    {
        var activity = DiscordActivityBuilder.Build(new TrackDto { Title = "T", Artist = "A" }, true, 5);
        var payload = DiscordActivityBuilder.BuildSetActivity(activity, processId: 4242);
        var json = JsonSerializer.Serialize(payload);

        using var document = JsonDocument.Parse(json);
        Assert.Equal("SET_ACTIVITY", document.RootElement.GetProperty("cmd").GetString());
        Assert.Equal(4242, document.RootElement.GetProperty("args").GetProperty("pid").GetInt32());
    }
}

public class DiscordIpcFramingTests
{
    [Fact]
    public async Task Frames_round_trip()
    {
        using var stream = new MemoryStream();
        await DiscordIpcFraming.WriteFrameAsync(stream, DiscordIpcFraming.OpHandshake, """{"v":1}""");
        stream.Position = 0;

        var frame = await DiscordIpcFraming.ReadFrameAsync(stream);

        Assert.NotNull(frame);
        Assert.Equal(DiscordIpcFraming.OpHandshake, frame!.Value.Opcode);
        Assert.Equal("""{"v":1}""", frame.Value.Json);
    }
}
