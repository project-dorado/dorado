using System;
using System.Globalization;
using Avalonia.Data.Converters;
using LibVLCSharp.Shared;

namespace Dorado.UI.Converters;

/// <summary>
/// Casts the engine's MediaPlayerHandle (object) to the LibVLCSharp.MediaPlayer
/// that the LibVLCSharp.Avalonia VideoView control binds to.
/// </summary>
public class ObjectToMediaPlayerConverter : IValueConverter
{
    public static readonly ObjectToMediaPlayerConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value as MediaPlayer;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
