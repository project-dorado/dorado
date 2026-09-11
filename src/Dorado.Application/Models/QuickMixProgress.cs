namespace Dorado.Application.Models;

/// <summary>
/// Progress notification for Smart DJ / Quick Mix generation. Mirrors the Zune
/// 4.8 <c>QuickMixProgress</c> experience: a named stage plus a 0..1 fraction.
/// </summary>
public sealed record QuickMixProgress(string Stage, double Fraction);
