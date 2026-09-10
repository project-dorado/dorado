using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Dorado.UI.Views;

/// <summary>
/// Procedural Quickplay hub map: a constellation of library nodes connected in sequence.
/// Clean-room replacement for the (absent) Zune map artwork.
/// </summary>
public sealed class HubMapControl : Control
{
    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<HubMapControl, int>(nameof(Count));

    public static readonly StyledProperty<int> SeedProperty =
        AvaloniaProperty.Register<HubMapControl, int>(nameof(Seed), 7);

    static HubMapControl()
    {
        AffectsRender<HubMapControl>(CountProperty, SeedProperty);
    }

    public int Count
    {
        get => GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public int Seed
    {
        get => GetValue(SeedProperty);
        set => SetValue(SeedProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var nodes = HubMapLayout.Compute(Count > 0 ? Count : 12, width, height, Seed);
        if (nodes.Count == 0)
        {
            return;
        }

        var linePen = new Pen(new SolidColorBrush(Color.Parse("#33FA2A55")), 1);
        var fill = new SolidColorBrush(Color.Parse("#40FA2A55"));

        for (var i = 1; i < nodes.Count; i++)
        {
            context.DrawLine(linePen,
                new Point(nodes[i - 1].X, nodes[i - 1].Y),
                new Point(nodes[i].X, nodes[i].Y));
        }

        foreach (var node in nodes)
        {
            context.DrawEllipse(fill, null, new Point(node.X, node.Y), node.Radius, node.Radius);
        }
    }
}
