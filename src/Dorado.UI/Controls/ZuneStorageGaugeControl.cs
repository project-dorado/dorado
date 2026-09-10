using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Dorado.UI.Controls;

/// <summary>
/// One proportional segment of a <see cref="ZuneStorageGaugeControl"/>.
/// </summary>
/// <param name="Label">Human-readable category (e.g. "MUSIC").</param>
/// <param name="Bytes">Relative weight used to size the segment (bytes of the category).</param>
/// <param name="Color">Signature Zune media-type fill colour.</param>
/// <param name="Tooltip">Hover text, typically the exact size + item count.</param>
public sealed record ZuneStorageSegment(string Label, double Bytes, Color Color, string Tooltip);

/// <summary>
/// Zune 4.8 segmented storage "gas gauge" (GASGAUGE / MINIGASGAUGE parity). Renders one
/// borderless, zero-radius proportional segment per <see cref="ZuneStorageSegment"/>, sized by
/// star weight so the bar always fills its container regardless of width.
/// </summary>
public sealed class ZuneStorageGaugeControl : Grid
{
    public static readonly StyledProperty<IEnumerable<ZuneStorageSegment>?> SegmentsProperty =
        AvaloniaProperty.Register<ZuneStorageGaugeControl, IEnumerable<ZuneStorageSegment>?>(nameof(Segments));

    public ZuneStorageGaugeControl()
    {
        Height = 20;
        ClipToBounds = true;
    }

    /// <summary>Ordered storage segments; each renders as a proportional coloured band.</summary>
    public IEnumerable<ZuneStorageSegment>? Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SegmentsProperty)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        ColumnDefinitions.Clear();
        Children.Clear();

        if (Segments is null)
        {
            return;
        }

        var column = 0;
        foreach (var segment in Segments)
        {
            ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(Math.Max(0.001, segment.Bytes), GridUnitType.Star)
            });

            var band = new Border
            {
                Background = new SolidColorBrush(segment.Color),
                CornerRadius = new CornerRadius(0)
            };
            SetColumn(band, column);
            ToolTip.SetTip(band, segment.Tooltip);
            Children.Add(band);
            column++;
        }
    }
}
