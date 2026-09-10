using Dorado.UI.Views;

namespace Dorado.Tests.Application;

public class HubMapLayoutTests
{
    [Fact]
    public void Produces_requested_node_count_within_bounds()
    {
        var nodes = HubMapLayout.Compute(24, width: 800, height: 500, seed: 11);

        Assert.Equal(24, nodes.Count);
        Assert.All(nodes, n =>
        {
            Assert.InRange(n.X, 0, 800);
            Assert.InRange(n.Y, 0, 500);
            Assert.True(n.Radius >= 3);
        });
    }

    [Fact]
    public void Is_deterministic_per_seed()
    {
        var first = HubMapLayout.Compute(10, 600, 400, seed: 7);
        var second = HubMapLayout.Compute(10, 600, 400, seed: 7);
        var different = HubMapLayout.Compute(10, 600, 400, seed: 99);

        Assert.Equal(first, second);
        Assert.NotEqual(first, different);
    }

    [Fact]
    public void Empty_or_invalid_inputs_produce_no_nodes()
    {
        Assert.Empty(HubMapLayout.Compute(0, 800, 500));
        Assert.Empty(HubMapLayout.Compute(10, 0, 0));
    }
}

public class IrisGeometryTests
{
    [Fact]
    public void Rings_are_bounded_and_fade_outward()
    {
        var rings = IrisGeometry.Rings(maxRadius: 200, progress: 0.3);

        Assert.Equal(IrisGeometry.RingCount, rings.Count);
        Assert.All(rings, r =>
        {
            Assert.InRange(r.Radius, 0, 200);
            Assert.InRange(r.Opacity, 0, 1);
        });
    }

    [Fact]
    public void Progress_advances_the_rings()
    {
        var early = IrisGeometry.Rings(200, 0.1);
        var later = IrisGeometry.Rings(200, 0.6);

        Assert.NotEqual(early, later);
        Assert.Empty(IrisGeometry.Rings(0, 0.5));
    }
}
