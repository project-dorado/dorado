using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Dorado.UI.Design;

/// <summary>
/// Builds the Now Playing equalizer geometry at bind time (in the UI, where the
/// Avalonia platform is available) from the ViewModel's
/// <c>NowPlayingIconFrame</c> (int) and <c>NowPlayingIconPlaying</c> (bool).
/// This keeps geometry construction out of the platform-free ViewModel.
/// </summary>
public sealed class EqualizerGeometryConverter : IMultiValueConverter
{
    public static EqualizerGeometryConverter Instance { get; } = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var frame = values.Count > 0 && values[0] is int f ? f : 1;
        var playing = values.Count > 1 && values[1] is bool p && p;
        return ZuneGlyphs.Equalizer(frame, playing);
    }
}
