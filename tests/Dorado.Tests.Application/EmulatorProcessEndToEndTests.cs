using Dorado.Infrastructure.Emulator;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// End-to-end proof that the desktop can invoke the emulator CLI as a real
/// sub-process over the <c>--ipc</c> JSON-RPC bridge. Locates the sibling
/// <c>dorado-emu</c> build output and no-ops when it is absent, so the desktop
/// suite stays green in isolation while giving full coverage when both repos
/// are present (as in the integration CI).
/// </summary>
public sealed class EmulatorProcessEndToEndTests
{
    [Fact]
    public async Task RefsAsync_OverRealSubprocess_ReturnsAssemblyReport()
    {
        var cliPath = LocateEmulatorCli();
        if (cliPath is null)
        {
            return;
        }

        var options = new EmulatorProcessOptions { EntryPointPath = cliPath };
        await using var bridge = new JsonRpcEmulatorBridge(new EmulatorProcessTransport(options));

        var report = await bridge.RefsAsync(typeof(EmulatorProcessEndToEndTests).Assembly.Location);

        Assert.NotEmpty(report.AssemblyReferences);
        Assert.Contains(report.AssemblyReferences, r => r.Contains("Dorado.Infrastructure.Emulator", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InspectAsync_OverRealSubprocess_ReportsInvalidPackage()
    {
        var cliPath = LocateEmulatorCli();
        if (cliPath is null)
        {
            return;
        }

        var options = new EmulatorProcessOptions { EntryPointPath = cliPath };
        await using var bridge = new JsonRpcEmulatorBridge(new EmulatorProcessTransport(options));

        await Assert.ThrowsAsync<EmulatorBridgeException>(() => bridge.InspectAsync("/nonexistent-package.zcp"));
    }

    /// <summary>
    /// Looks for the emulator CLI next to a sibling <c>dorado-emu</c> checkout,
    /// preferring the same configuration as this test binary.
    /// </summary>
    private static string? LocateEmulatorCli()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var emuSolution = Path.Combine(directory.FullName, "dorado-emu", "Dorado.sln");
            if (File.Exists(emuSolution))
            {
                foreach (var candidate in new[] { "Release", "Debug" })
                {
                    var path = Path.Combine(
                        directory.FullName, "dorado-emu", "src", "Dorado.Cli", "bin", candidate, "net8.0", "Dorado.Cli.dll");
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }
            }

            directory = directory.Parent;
        }

        return null;
    }
}
