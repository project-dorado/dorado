using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Dorado.UI.Converters;

/// <summary>Tier A3 — idle progress 0..1 → -80 px translate (drifts text off-screen).</summary>
public sealed class IdleDriftConverter : IValueConverter
{
    public static readonly IdleDriftConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var progress = value switch
        {
            double d => d,
            float f => (double)f,
            _ => 0.0,
        };
        return -80.0 * Math.Clamp(progress, 0.0, 1.0);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
