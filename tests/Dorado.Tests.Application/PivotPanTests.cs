using Dorado.UI.Views;

namespace Dorado.Tests.Application;

public class PivotPanTests
{
    [Fact]
    public void Offset_clamps_to_scrollable_range()
    {
        Assert.Equal(0, PivotPanMath.ClampOffset(-40, extent: 500, viewport: 300));
        Assert.Equal(40, PivotPanMath.ClampOffset(40, extent: 400, viewport: 300));
        Assert.Equal(200, PivotPanMath.ClampOffset(500, extent: 500, viewport: 300));
        // Content narrower than the viewport is not scrollable.
        Assert.Equal(0, PivotPanMath.ClampOffset(50, extent: 200, viewport: 300));
    }

    [Fact]
    public void Inertia_decelerates_and_stops()
    {
        var velocity = 1.0;
        var ticks = 0;
        while (PivotPanMath.ShouldContinue(velocity) && ticks < 1000)
        {
            velocity = PivotPanMath.NextVelocity(velocity);
            ticks++;
        }

        Assert.True(ticks is > 10 and < 200);
        Assert.False(PivotPanMath.ShouldContinue(velocity));
    }
}
