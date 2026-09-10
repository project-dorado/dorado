using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;

namespace Dorado.UI.Converters;

public class TransferActionColorConverter : IValueConverter
{
    public static readonly TransferActionColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            TransferAction.Add => "#FA2A55",
            TransferAction.Remove => "#E81123",
            TransferAction.Keep => "#777777",
            _ => "#777777"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class BytesToMegabytesConverter : IValueConverter
{
    public static readonly BytesToMegabytesConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long bytes)
        {
            return string.Empty;
        }

        return bytes >= 1024L * 1024 * 1024
            ? $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
            : $"{bytes / (1024.0 * 1024):F1} MB";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
