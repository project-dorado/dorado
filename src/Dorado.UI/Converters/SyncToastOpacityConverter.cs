using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Dorado.UI.Converters;

/// <summary>Tier A4 — SyncToast opacity: true → 1, false → 0.</summary>
public sealed class SyncToastOpacityConverter : IValueConverter
{
    public static readonly SyncToastOpacityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool isVisible && isVisible ? 1.0 : 0.0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
