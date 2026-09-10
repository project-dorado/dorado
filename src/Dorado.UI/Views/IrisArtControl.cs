using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Dorado.UI.Views;

/// <summary>
/// Procedural "iris" animation rendered behind the Now Playing text: concentric rings
/// that expand outward and fade, looping. Pulse is driven by a dispatcher timer.
/// </summary>
public sealed class IrisArtControl : Control
{
    private readonly DispatcherTimer _timer;
    private double _progress;
    private static readonly IBrush RingBrush = new SolidColorBrush(Color.Parse("#22FA2A55"));

    public IrisArtControl()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _timer.Tick += (_, _) =>
        {
            _progress = (_progress + 0.012) % 1.0;
            InvalidateVisual();
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var center = new Point(width / 2, height / 2);
        var maxRadius = Math.Min(width, height) * 0.48;

        foreach (var ring in IrisGeometry.Rings(maxRadius, _progress))
        {
            if (ring.Radius <= 0 || ring.Opacity <= 0)
            {
                continue;
            }

            var pen = new Pen(RingBrush, 2, new DashStyle(new double[] { 6, 10 }, 0));
            context.DrawEllipse(null, pen, center, ring.Radius, ring.Radius);
        }
    }
}
