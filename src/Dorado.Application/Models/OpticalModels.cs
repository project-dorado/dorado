namespace Dorado.Application.Models;

/// <summary>One track in an Audio CD table of contents.</summary>
public sealed record OpticalTrack(
    int Number,
    int StartSector,
    int LengthSectors,
    TimeSpan Duration,
    string Title);

/// <summary>Result of a short-lived external process invocation.</summary>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}
