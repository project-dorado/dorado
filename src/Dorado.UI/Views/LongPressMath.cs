namespace Dorado.UI.Views;

/// <summary>Pure timing for the press-and-hold "pin to Quickplay" gesture (B3).</summary>
public static class LongPressMath
{
    public const int HoldMilliseconds = 700;
    public const double MoveTolerancePixels = 8;

    public static bool ShouldTrigger(long elapsedMilliseconds, double movedPixels)
        => elapsedMilliseconds >= HoldMilliseconds && movedPixels <= MoveTolerancePixels;
}
