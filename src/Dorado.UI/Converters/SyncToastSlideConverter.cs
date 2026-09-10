using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Dorado.UI.Converters;

/// <summary>Tier A4 — SyncToast slide offset (Y axis): true → 0 (on screen), false → 80 (below).</summary>
public sealed class SyncToastSlideConverter : IValueConverter
{
    public static readonly SyncToastSlideConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool isVisible && isVisible ? 0.0 : 80.0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
