using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Dorado.UI.Animations;

/// <summary>
/// Zune 4.8 pivot transition (Tier A2): when the user navigates between pivot
/// strips, the outgoing view slides + scales out toward the new pivot's direction,
/// the incoming view slides + scales in from the opposite side. Quickplay gets
/// a slightly stronger parallax + 320 ms duration to feel "parked left and rear".
/// </summary>
public sealed class PivotParallaxTransition : IPageTransition
{
    private const double DefaultDurationMs = 420;
    private const double QuickplayDurationMs = 320;
    private const double DefaultScaleOut = 0.92;
    private const double QuickplayScaleOut = 0.85;

    /// <summary>When true, use the Quickplay-tighter (faster, deeper parallax) variant.</summary>
    public bool ToQuickplay { get; set; }

    public PivotParallaxTransition()
    {
    }

    public Task Start(Visual? from, Visual? to, bool forward, CancellationToken ct)
    {
        if (to is null)
        {
            return Task.CompletedTask;
        }

        var (durationMs, scaleOut) = ResolveVariant();

        if (from != null && from != to)
        {
            AnimateOut(from, forward, durationMs, scaleOut);
        }

        AnimateIn(to, forward, durationMs, scaleOut);

        return Task.CompletedTask;
    }

    private (double DurationMs, double ScaleOut) ResolveVariant()
    {
        return ToQuickplay
            ? (QuickplayDurationMs, QuickplayScaleOut)
            : (DefaultDurationMs, DefaultScaleOut);
    }

    private static void AnimateOut(Visual visual, bool forward, double durationMs, double scaleOut)
    {
        double width = visual.Bounds.Width;
        if (width <= 0)
        {
            width = EstimateVisualWidth(visual);
        }
        if (width <= 0)
        {
            width = 1024;
        }

        ApplyTransforms(visual);

        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(durationMs),
            Easing = new CubicEaseOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Setters =
                    {
                        new Setter(TranslateTransform.XProperty, 0d),
                        new Setter(ScaleTransform.ScaleXProperty, 1d),
                        new Setter(ScaleTransform.ScaleYProperty, 1d),
                        new Setter(Visual.OpacityProperty, 1d),
                    },
                    Cue = new Cue(0d),
                },
                new KeyFrame
                {
                    Setters =
                    {
                        new Setter(TranslateTransform.XProperty, forward ? -width : width),
                        new Setter(ScaleTransform.ScaleXProperty, scaleOut),
                        new Setter(ScaleTransform.ScaleYProperty, scaleOut),
                        new Setter(Visual.OpacityProperty, 0d),
                    },
                    Cue = new Cue(1d),
                },
            },
        };

        _ = animation.RunAsync(visual);
    }

    private static void AnimateIn(Visual visual, bool forward, double durationMs, double scaleOut)
    {
        double width = visual.Bounds.Width;
        if (width <= 0)
        {
            width = EstimateVisualWidth(visual);
        }
        if (width <= 0)
        {
            width = 1024;
        }

        ApplyTransforms(visual);
        visual.Opacity = 0;

        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(durationMs),
            Easing = new CubicEaseOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Setters =
                    {
                        new Setter(TranslateTransform.XProperty, forward ? width : -width),
                        new Setter(ScaleTransform.ScaleXProperty, scaleOut),
                        new Setter(ScaleTransform.ScaleYProperty, scaleOut),
                        new Setter(Visual.OpacityProperty, 0d),
                    },
                    Cue = new Cue(0d),
                },
                new KeyFrame
                {
                    Setters =
                    {
                        new Setter(TranslateTransform.XProperty, 0d),
                        new Setter(ScaleTransform.ScaleXProperty, 1d),
                        new Setter(ScaleTransform.ScaleYProperty, 1d),
                        new Setter(Visual.OpacityProperty, 1d),
                    },
                    Cue = new Cue(1d),
                },
            },
        };

        _ = animation.RunAsync(visual);
    }

    private static void ApplyTransforms(Visual visual)
    {
        var group = new TransformGroup
        {
            Children =
            {
                new TranslateTransform(),
                new ScaleTransform(),
            },
        };
        visual.RenderTransform = group;
    }

    private static double EstimateVisualWidth(Visual visual)
    {
        Visual? current = visual;
        while (current != null)
        {
            if (current.Bounds.Width > 0)
            {
                return current.Bounds.Width;
            }
            current = current.GetVisualParent();
        }
        return 1024;
    }
}
