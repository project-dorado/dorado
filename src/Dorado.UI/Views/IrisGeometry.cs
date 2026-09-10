using System;
using System.Collections.Generic;

namespace Dorado.UI.Views;

public readonly record struct IrisRing(double Radius, double Opacity);

/// <summary>
/// Pure geometry for the procedural "iris" reveal behind the Now Playing text — a
/// clean-room replacement for the absent `NOWPLAYINGARTLOGO`/`NOWPLAYINGARTSHAPE` frames.
/// </summary>
public static class IrisGeometry
{
    public const int RingCount = 6;

    public static IReadOnlyList<IrisRing> Rings(double maxRadius, double progress)
    {
        var rings = new List<IrisRing>(RingCount);
        if (maxRadius <= 0)
        {
            return rings;
        }

        var normalized = progress % 1.0;

        for (var i = 0; i < RingCount; i++)
        {
            var phase = (normalized + i / (double)RingCount) % 1.0;
            rings.Add(new IrisRing(maxRadius * phase, 0.5 * (1 - phase)));
        }

        return rings;
    }
}
