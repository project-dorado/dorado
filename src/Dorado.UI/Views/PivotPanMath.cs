namespace Dorado.UI.Views;

/// <summary>
/// Pure math for the drag-with-inertia panoramic pivot strip, separated so the
/// deceleration behavior is unit-testable without a UI session.
/// </summary>
public static class PivotPanMath
{
    public const double Friction = 0.92;
    public const double StopThreshold = 0.05;

    public static double ClampOffset(double value, double extent, double viewport)
        => Math.Clamp(value, 0, Math.Max(0, extent - viewport));

    public static double NextVelocity(double velocity) => velocity * Friction;

    public static bool ShouldContinue(double velocity) => Math.Abs(velocity) >= StopThreshold;
}
