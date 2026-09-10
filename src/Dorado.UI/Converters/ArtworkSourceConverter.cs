using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Dorado.UI.Converters;

/// <summary>
/// Converts a local cached artwork file path (or avares:// asset URI) into an IImage for XAML bindings.
/// Returns null for missing sources so typographic fallback tiles remain visible.
/// Decoded bitmaps are cached (keyed by source + decode width) so scrolling the collection or
/// cycling the slideshow never re-decodes from disk.
/// </summary>
public class ArtworkSourceConverter : IValueConverter
{
    public static readonly ArtworkSourceConverter Instance = new();

    /// <summary>High-resolution variant for photo zoom/slideshow surfaces.</summary>
    public static readonly ArtworkSourceConverter Large = new() { DecodeWidth = 1600 };

    private const int CacheCapacity = 600;
    private static readonly ConcurrentDictionary<string, IImage?> Cache = new();

    public int DecodeWidth { get; set; } = 300;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string source || string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var key = $"{DecodeWidth}|{source}";
        // High-resolution slideshow/zoom surfaces get a far smaller cache (memory).
        var capacity = DecodeWidth > 600 ? 48 : CacheCapacity;
        if (Cache.Count > capacity)
        {
            Cache.Clear();
        }

        return Cache.GetOrAdd(key, _ => Decode(source));
    }

    private IImage? Decode(string source)
    {
        try
        {
            if (source.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
            {
                return new Bitmap(AssetLoader.Open(new Uri(source)));
            }

            if (File.Exists(source))
            {
                using var stream = File.OpenRead(source);
                return Bitmap.DecodeToWidth(stream, DecodeWidth);
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
