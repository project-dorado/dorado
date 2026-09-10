using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Dorado.Domain.Models;

namespace Dorado.UI.Converters;

/// <summary>
/// Tier B2 — returns an accent brush when the bound Playlist equals the VM's currently
/// hovered drop target, transparent otherwise.
/// </summary>
public sealed class PlaylistDropHighlightConverter : IMultiValueConverter
{
    public static readonly PlaylistDropHighlightConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return Avalonia.Media.Brushes.Transparent;
        var playlist = values[0] as Playlist;
        var hovered = values[1] as Playlist;

        if (playlist == null || hovered == null) return Avalonia.Media.Brushes.Transparent;
        return playlist.Id == hovered.Id
            ? new SolidColorBrush(Color.FromArgb(0x33, 0xF1, 0x0D, 0xA2))
            : Avalonia.Media.Brushes.Transparent;
    }
}
