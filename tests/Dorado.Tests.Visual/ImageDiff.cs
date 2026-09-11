using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;

namespace Dorado.Tests.Visual;

/// <summary>
/// Tolerant pixel comparator for golden screenshots. A pixel counts as "changed"
/// only when a channel differs by more than <c>channelTolerance</c>, which absorbs
/// the minor antialiasing/font-hinting differences between hosts while still
/// catching real layout/token regressions.
/// </summary>
public static class ImageDiff
{
    public const int ChannelTolerance = 32;

    /// <summary>Fraction of pixels allowed to differ (default 6%; override via DORADO_VISUAL_TOLERANCE).</summary>
    public static readonly double ChangedRatioTolerance =
        double.TryParse(Environment.GetEnvironmentVariable("DORADO_VISUAL_TOLERANCE"),
            System.Globalization.CultureInfo.InvariantCulture, out var t) && t > 0 ? t : 0.06;

    public static (bool Same, double ChangedRatio) Compare(string actualPath, string goldenPath)
    {
        using var actual = new Bitmap(actualPath);
        using var golden = new Bitmap(goldenPath);

        if (actual.PixelSize != golden.PixelSize)
            return (false, 1.0);

        var pa = ReadPixels(actual);
        var pb = ReadPixels(golden);
        long total = actual.PixelSize.Width * (long)actual.PixelSize.Height;
        long changed = 0;

        for (int i = 0; i + 3 < pa.Length; i += 4)
        {
            if (Math.Abs(pa[i] - pb[i]) > ChannelTolerance ||
                Math.Abs(pa[i + 1] - pb[i + 1]) > ChannelTolerance ||
                Math.Abs(pa[i + 2] - pb[i + 2]) > ChannelTolerance ||
                Math.Abs(pa[i + 3] - pb[i + 3]) > ChannelTolerance)
            {
                changed++;
            }
        }

        double ratio = total == 0 ? 0 : (double)changed / total;
        return (ratio <= ChangedRatioTolerance, ratio);
    }

    private static byte[] ReadPixels(Bitmap bmp)
    {
        var size = bmp.PixelSize;
        var stride = size.Width * 4;
        var buffer = new byte[stride * size.Height];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            bmp.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), handle.AddrOfPinnedObject(), buffer.Length, stride);
        }
        finally
        {
            handle.Free();
        }
        return buffer;
    }
}
