using System.Text.Json;

namespace Dorado.Infrastructure.Emulator;

/// <summary>
/// Desktop-side client for the Dorado emulator's JSON-RPC bridge
/// (<c>dorado-emu/src/Dorado.Cli.Ipc/EmulatorRpcServer.cs</c>). The desktop
/// spawns <c>dorado --ipc</c> as a sub-process and drives <c>inspect</c>,
/// <c>unpack</c>, <c>refs</c>, and <c>run</c> over the same line-framed
/// JSON-RPC 2.0 contract used by the plugin host and the LAN sync protocol.
/// </summary>
public interface IEmulatorBridge : IAsyncDisposable
{
    /// <summary>Parses a <c>.ccgame</c> / <c>.zcp</c> package and returns its manifest metadata.</summary>
    Task<EmulatorPackageInfo> InspectAsync(string packagePath, CancellationToken cancellationToken = default);

    /// <summary>Extracts an unencrypted package to <paramref name="outputDirectory"/>.</summary>
    Task<EmulatorUnpackResult> UnpackAsync(string packagePath, string outputDirectory, CancellationToken cancellationToken = default);

    /// <summary>Lists the assembly/member references of a managed assembly.</summary>
    Task<EmulatorAssemblyReport> RefsAsync(string assemblyPath, CancellationToken cancellationToken = default);

    /// <summary>Runs a package headlessly and optionally returns a deterministic frame hash.</summary>
    Task<EmulatorRunResult> RunAsync(string packagePath, int frames = 60, string? frameOutputPath = null, bool hash = false, CancellationToken cancellationToken = default);
}

public sealed record EmulatorPackageInfo(
    string Kind,
    string? Title,
    string? Description,
    string? Executable,
    string? StartupAssembly,
    string? Platform,
    string? Guid,
    string? RuntimeProfile,
    string? CcgameVersion,
    bool Encrypted,
    int EntryCount,
    int FileCount,
    IReadOnlyList<EmulatorPackageFile> Files);

public sealed record EmulatorPackageFile(string Path, string? Container, long Size);

public sealed record EmulatorUnpackResult(int Written, string OutputDirectory, string? Error);

public sealed record EmulatorAssemblyReport(
    IReadOnlyList<string> AssemblyReferences,
    IReadOnlyList<string> TypeReferences,
    IReadOnlyList<string> XnaDerivedTypes,
    IReadOnlyDictionary<string, IReadOnlyList<string>> MemberReferences);

public sealed record EmulatorRunResult(string EntryPoint, int FramesRendered, string? FrameSha256);

/// <summary>Thrown when the emulator bridge returns a JSON-RPC error or the process dies.</summary>
public sealed class EmulatorBridgeException : Exception
{
    public EmulatorBridgeException(string message) : base(message) { }
    public EmulatorBridgeException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Small helpers for defensively reading the emulator's JSON payloads.</summary>
internal static class EmulatorJson
{
    public static string? GetStringOrNull(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static string GetString(JsonElement element, string property)
        => GetStringOrNull(element, property) ?? string.Empty;

    public static int GetInt(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;

    public static bool GetBool(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
}
