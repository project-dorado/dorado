using Dorado.UI.Views;

namespace Dorado.Tests.Application;

public class TypeAheadTests
{
    [Fact]
    public void Buffer_accumulates_then_resets_after_idle()
    {
        var buffer = new TypeAheadBuffer();
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal("r", buffer.Append('R', start));
        Assert.Equal("ru", buffer.Append('u', start.AddMilliseconds(200)));
        // After the reset window the prefix starts over.
        Assert.Equal("s", buffer.Append('s', start.AddMilliseconds(1500)));
    }

    [Fact]
    public void Search_finds_first_prefix_match_case_insensitively()
    {
        var items = new[] { "Alpha", "Beta", "Breathe", "Blue" };

        Assert.Equal(1, TypeAheadSearch.FindIndex(items, "be", s => s));
        Assert.Equal(2, TypeAheadSearch.FindIndex(items, "br", s => s));
        Assert.Equal(-1, TypeAheadSearch.FindIndex(items, "zz", s => s));
        Assert.Equal(-1, TypeAheadSearch.FindIndex(items, string.Empty, s => s));
    }

    [Theory]
    [InlineData(700, 0, true)]
    [InlineData(699, 0, false)]
    [InlineData(900, 8, true)]
    [InlineData(900, 9, false)]
    public void Long_press_triggers_on_hold_without_movement(long elapsed, double moved, bool expected)
    {
        Assert.Equal(expected, LongPressMath.ShouldTrigger(elapsed, moved));
    }
}
