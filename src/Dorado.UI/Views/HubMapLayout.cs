using System;
using System.Collections.Generic;

namespace Dorado.UI.Views;

public readonly record struct HubMapNode(double X, double Y, double Radius, double Angle);

/// <summary>
/// Deterministic golden-angle ("sunflower") layout for the Quickplay hub map. Pure and
/// seeded so the map is stable across frames; replaced the absent copyrighted map artwork.
/// </summary>
public static class HubMapLayout
{
    private static readonly double GoldenAngle = Math.PI * (3 - Math.Sqrt(5));

    public static IReadOnlyList<HubMapNode> Compute(int count, double width, double height, int seed = 7)
    {
        var nodes = new List<HubMapNode>();
        if (count <= 0 || width <= 0 || height <= 0)
        {
            return nodes;
        }

        var centerX = width / 2;
        var centerY = height / 2;
        var extent = Math.Min(width, height) * 0.46;

        for (var i = 0; i < count; i++)
        {
            var t = (i + 0.5) / count;
            var radius = Math.Sqrt(t) * extent;
            var angle = i * GoldenAngle + seed;
            var jitter = ((seed * 31 + i * 17) % 11) / 11.0 - 0.5;

            nodes.Add(new HubMapNode(
                centerX + Math.Cos(angle) * radius + jitter * 12,
                centerY + Math.Sin(angle) * radius + jitter * 8,
                3 + (seed + i) % 4,
                angle));
        }

        return nodes;
    }
}
