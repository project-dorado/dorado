using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Dorado.UI.Converters;

/// <summary>Tier A3 — true → fully visible (1.0), false → 15% (controls fade during screensaver).</summary>
public sealed class IdleOpacityConverter : IValueConverter
{
    public static readonly IdleOpacityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool isIdle && isIdle ? 0.15 : 1.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
