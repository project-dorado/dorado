using Avalonia.Media;

namespace Dorado.UI.Design;

/// <summary>
/// Clean-room vector glyphs (Vector 4 asset normalization). The Zune HD's
/// rating artwork (`RATING.LIKEIT.*.PNG` / `RATING.HATEIT.*.PNG`) was a
/// Microsoft asset; these vector hearts replace it and match the HD client's
/// procedural hearts. Zero rounded corners, single-tone fills driven by tokens.
/// </summary>
public static class ZuneGlyphs
{
    /// <summary>The "favorite" (heart) glyph.</summary>
    public static Geometry Heart { get; } = StreamGeometry.Parse(
        "M12,21.35 L10.55,20.03 C5.4,15.36 2,12.28 2,8.5 C2,5.42 4.42,3 7.5,3 " +
        "C9.24,3 10.91,3.81 12,5.09 C13.09,3.81 14.76,3 16.5,3 C19.58,3 22,5.42 22,8.5 " +
        "C22,12.28 18.6,15.36 13.45,20.04 L12,21.35 Z");

    /// <summary>
    /// The "dislike" (broken heart) glyph: the heart with a jagged bolt cut out.
    /// EvenOdd makes the second subpath a hole rather than a fill.
    /// </summary>
    public static Geometry BrokenHeart { get; } = BuildBrokenHeart();

    // ---- transport glyphs (filled) --------------------------------------
    // The Zune 4.8 TRANSPORT.*.PNG set was a Microsoft asset; these replace it.

    public static Geometry Play { get; } = StreamGeometry.Parse("M8,5 L19,12 L8,19 Z");

    public static Geometry Pause { get; } = StreamGeometry.Parse("M8,5 H11 V19 H8 Z M13,5 H16 V19 H13 Z");

    /// <summary>Previous: a leading bar plus a left-pointing triangle.</summary>
    public static Geometry SkipBack { get; } = StreamGeometry.Parse("M6,5 H9 V19 H6 Z M20,5 L11,12 L20,19 Z");

    /// <summary>Next: a right-pointing triangle plus a trailing bar.</summary>
    public static Geometry SkipForward { get; } = StreamGeometry.Parse("M18,5 H15 V19 H18 Z M4,5 L13,12 L4,19 Z");

    /// <summary>A speaker body; draw with a fill.</summary>
    public static Geometry Speaker { get; } = StreamGeometry.Parse(
        "M4,9 H8 L13,5 V19 L8,15 H4 Z");

    // ---- transport glyphs (stroked) -------------------------------------

    /// <summary>Two looping arrows (repeat). Draw with Stroke, no Fill.</summary>
    public static Geometry Repeat { get; } = StreamGeometry.Parse(
        "M17,2 L21,6 L17,10 M21,6 H7 A4,4 0 0 0 3,10 M7,22 L3,18 L7,14 M3,18 H17 A4,4 0 0 1 21,14");

    /// <summary>Crossing arrows (shuffle). Draw with Stroke, no Fill.</summary>
    public static Geometry Shuffle { get; } = StreamGeometry.Parse(
        "M16,3 H21 V8 M21,3 L4,20 M21,16 V21 H16 M4,4 L9,9 M15,15 L21,21");

    /// <summary>Queue lines with a play marker (showlist). Draw with Stroke.</summary>
    public static Geometry ShowList { get; } = StreamGeometry.Parse(
        "M4,6 H20 M4,12 H14 M4,18 H20 M16,10 L21,12 L16,14 Z");

    // ---- window chrome glyphs (stroked) ---------------------------------
    // The Zune 4.8 WINDOW.*.PNG chrome set was a Microsoft asset.

    public static Geometry Minimize { get; } = StreamGeometry.Parse("M4,12 H20");

    public static Geometry Maximize { get; } = StreamGeometry.Parse("M4,4 H20 V20 H4 Z");

    public static Geometry Restore { get; } = StreamGeometry.Parse("M4,8 H16 V20 H4 Z M8,4 H20 V16 H16");

    public static Geometry Close { get; } = StreamGeometry.Parse("M5,5 L19,19 M19,5 L5,19");

    public static Geometry ArrowBack { get; } = StreamGeometry.Parse("M20,12 H4 M11,5 L4,12 L11,19");

    /// <summary>Compact-mode toggle: a rectangle with a divider bar.</summary>
    public static Geometry CompactMode { get; } = StreamGeometry.Parse("M5,4 H19 V20 H5 Z M5,9 H19");

    // ---- status / social glyphs -----------------------------------------

    /// <summary>Actively-syncing circular arrow. Draw with Stroke.</summary>
    public static Geometry Sync { get; } = StreamGeometry.Parse(
        "M12,4 A8,8 0 1 1 5.4,8.6 M12,4 L8.2,2.2 M12,4 L9.6,7.8");

    /// <summary>Badge seal rosette. Draw with Fill.</summary>
    public static Geometry Seal { get; } = StreamGeometry.Parse(
        "M12,1 L14.2,6.1 L19.5,4.2 L17.9,9.5 L23,12 L17.9,14.5 L19.5,19.8 L14.2,17.9 " +
        "L12,23 L9.8,17.9 L4.5,19.8 L6.1,14.5 L1,12 L6.1,9.5 L4.5,4.2 L9.8,6.1 Z");

    /// <summary>Default profile tile (person silhouette). Draw with Fill.</summary>
    public static Geometry ProfileTile { get; } = StreamGeometry.Parse(
        "M12,3 A4,4 0 1 1 11.99,3 Z M4,21 A8,5.5 0 0 1 20,21 Z");

    // ---- media / mixview glyphs (filled) --------------------------------

    /// <summary>Info glyph. Draw with Stroke.</summary>
    public static Geometry Info { get; } = StreamGeometry.Parse("M12,3 A9,9 0 1 1 11.99,3 Z M12,10 V17 M12,7 V7.6");

    /// <summary>Plus / add glyph. Draw with Stroke.</summary>
    public static Geometry Add { get; } = StreamGeometry.Parse("M12,5 V19 M5,12 H19");

    /// <summary>Four-point sparkle (Mix). Draw with Fill.</summary>
    public static Geometry Mix { get; } = StreamGeometry.Parse(
        "M12,2 L14,9 L21,11 L14,13 L12,20 L10,13 L3,11 L10,9 Z");

    /// <summary>Disc with a play cutout. Draw with Fill (EvenOdd).</summary>
    public static Geometry PlayCircle { get; } = StreamGeometry.Parse(
        "M12,2 A10,10 0 1 1 11.99,2 Z M9.5,7.5 L17,12 L9.5,16.5 Z");

    /// <summary>Empty photo/video placeholder. Draw with Stroke.</summary>
    public static Geometry PhotoFrame { get; } = StreamGeometry.Parse(
        "M4,5 H20 V19 H4 Z M4,15 L9,10 L13,14 L16,11 L20,15");

    // ---- brand marks (clean-room; no Microsoft logos) -------------------

    /// <summary>Dorado mark: a square with a play cutout. Draw with Fill (EvenOdd).</summary>
    public static Geometry BrandMark { get; } = StreamGeometry.Parse(
        "M3,3 H21 V21 H3 Z M8.5,7.5 L16.5,12 L8.5,16.5 Z");

    /// <summary>Clean-room portable-device silhouette. Draw with Stroke.</summary>
    public static Geometry Device { get; } = StreamGeometry.Parse(
        "M5,2 H19 V22 H5 Z M10,19 H14");

    /// <summary>Fullscreen expand / pop-out toggle (two opposite-pointing corner arrows). Draw with Stroke.</summary>
    public static Geometry FullscreenExpand { get; } = StreamGeometry.Parse(
        "M15,3 H21 V9 M21,3 L13,11 M9,21 H3 V15 M3,21 L11,13");

    /// <summary>Clean-room forward navigation arrow. Draw with Stroke.</summary>
    public static Geometry ArrowRight { get; } = StreamGeometry.Parse(
        "M4,12 H20 M13,5 L20,12 L13,19");

    /// <summary>Clean-room geometric bullet diamond. Draw with Fill.</summary>
    public static Geometry Diamond { get; } = StreamGeometry.Parse(
        "M12,2 L21,12 L12,22 L3,12 Z");

    /// <summary>Clean-room musical note placeholder. Draw with Fill.</summary>
    public static Geometry MusicNote { get; } = StreamGeometry.Parse(
        "M12,3 V14.5 A3.5,3.5 0 1 1 8.5,11 A3.5,3.5 0 0 1 12,11.5 V5 H18 V3 Z");

    /// <summary>Clean-room subtle clear / cross glyph. Draw with Stroke.</summary>
    public static Geometry Cross { get; } = StreamGeometry.Parse(
        "M6,6 L18,18 M18,6 L6,18");

    /// <summary>Clean-room queue lines / dynamic mix list icon. Draw with Stroke.</summary>
    public static Geometry QueueLines { get; } = StreamGeometry.Parse(
        "M4,7 H20 M4,12 H20 M4,17 H20");

    /// <summary>Clean-room 2x2 grid / mosaic wall toggle. Draw with Fill.</summary>
    public static Geometry GridMosaic { get; } = StreamGeometry.Parse(
        "M4,4 H10 V10 H4 Z M14,4 H20 V10 H14 Z M4,14 H10 V20 H4 Z M14,14 H20 V20 H14 Z");

    /// <summary>
    /// The Now Playing equalizer mark, drawn procedurally so the device's
    /// <c>ICON.NOWPLAYING.FRAME01..10.PNG</c> animation set is not bundled.
    /// <paramref name="frame"/> cycles the bar heights; when
    /// <paramref name="playing"/> is false the bars are even ("idle").
    /// </summary>
    public static Geometry Equalizer(int frame, bool playing) => BuildEqualizer(frame, playing);

    private static Geometry BuildEqualizer(int frame, bool playing)
    {
        const double width = 3.0;
        const double gap = 1.6;
        const double startX = 1.0;
        const double baseline = 20.0;
        double[] weights = { 0.55, 1.0, 0.7, 0.9 };

        var path = new System.Text.StringBuilder();
        for (int i = 0; i < weights.Length; i++)
        {
            double height;
            if (playing)
            {
                double phase = System.Math.Sin((frame + (i * 2)) * System.Math.PI / 5.0);
                height = 4.0 + (weights[i] * (0.45 + (0.55 * System.Math.Abs(phase))) * 13.0);
            }
            else
            {
                height = 4.0 + (weights[i] * 4.0);
            }

            double x = startX + (i * (width + gap));
            double y = baseline - height;
            path.Append($"M{x.ToString(System.Globalization.CultureInfo.InvariantCulture)},{y.ToString(System.Globalization.CultureInfo.InvariantCulture)} ")
                .Append($"H{(x + width).ToString(System.Globalization.CultureInfo.InvariantCulture)} ")
                .Append($"V{baseline.ToString(System.Globalization.CultureInfo.InvariantCulture)} ")
                .Append($"H{x.ToString(System.Globalization.CultureInfo.InvariantCulture)} Z ");
        }

        return StreamGeometry.Parse(path.ToString());
    }

    private static Geometry BuildBrokenHeart()
    {
        // Two subpaths; Avalonia's parser fills them EvenOdd, so the bolt is a
        // hole in the heart rather than an overlapping fill.
        return StreamGeometry.Parse(
            "M12,21.35 L10.55,20.03 C5.4,15.36 2,12.28 2,8.5 C2,5.42 4.42,3 7.5,3 " +
            "C9.24,3 10.91,3.81 12,5.09 C13.09,3.81 14.76,3 16.5,3 C19.58,3 22,5.42 22,8.5 " +
            "C22,12.28 18.6,15.36 13.45,20.04 L12,21.35 Z " +
            "M12.9,5.4 L8.7,12.1 L11.9,12.1 L10.6,19.6 L16.4,9.4 L13.2,9.4 Z");
    }

    /// <summary>Clean-room pushpin vector glyph for Quickplay pins deck.</summary>
    public static Geometry Pin => StreamGeometry.Parse("M14,4 L15,5 L15,10 L18,13 L18,15 L13,15 L13,21 L12,22 L11,21 L11,15 L6,15 L6,13 L9,10 L9,5 L10,4 Z");
}

