using System.Diagnostics;
using Dorado.Plugins.Protocol.Rpc;

namespace Dorado.Infrastructure.Emulator;

/// <summary>
/// Options for launching the emulator CLI in IPC mode.
/// </summary>
public sealed class EmulatorProcessOptions
{
    /// <summary>
    /// Path to the emulator entry point. A <c>.dll</c> is launched via the
    /// <c>dotnet</c> host; anything else is launched directly. Defaults to
    /// <c>dorado</c> on PATH.
    /// </summary>
    public string EntryPointPath { get; set; } = "dorado";

    /// <summary>Arguments placed before the mandatory <c>--ipc</c> switch.</summary>
    public IReadOnlyList<string> PrefixArguments { get; set; } = Array.Empty<string>();

    public string? WorkingDirectory { get; set; }

    /// <summary>Per-request timeout applied to JSON-RPC calls.</summary>
    public TimeSpan CallTimeout { get; set; } = TimeSpan.FromSeconds(60);
}

/// <summary>
/// Spawns the Dorado emulator CLI with <c>--ipc</c> and frames JSON-RPC 2.0
/// lines over its stdin/stdout. The shape mirrors the plugin host's
/// <c>ProcessPluginTransport</c> so both use the same line transport contract.
/// </summary>
public sealed class EmulatorProcessTransport : ILineTransport
{
    private readonly EmulatorProcessOptions _options;
    private Process? _process;
    private bool _stopping;

    public EmulatorProcessTransport(EmulatorProcessOptions options)
    {
        _options = options;
    }

    public bool IsRunning => _process is { HasExited: false };

    public event EventHandler<int?>? Exited;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_process is not null)
        {
            throw new InvalidOperationException("Transport already started.");
        }

        var (fileName, arguments) = ResolveCommand(_options);
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = _options.WorkingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.Exited += (_, _) =>
        {
            if (!_stopping)
            {
                Exited?.Invoke(this, TryGetExitCode(process));
            }
        };

        process.Start();
        _process = process;
        return Task.CompletedTask;
    }

    public async Task WriteLineAsync(string line, CancellationToken cancellationToken = default)
    {
        var process = _process ?? throw new InvalidOperationException("Transport not started.");
        await process.StandardInput.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
        await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        var process = _process ?? throw new InvalidOperationException("Transport not started.");
        return await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync()
    {
        _stopping = true;
        var process = _process;
        if (process is not null)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Process already gone.
            }
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        var process = _process;
        _process = null;
        if (process is not null)
        {
            _stopping = true;
            try { process.Dispose(); } catch { /* observed */ }
        }

        return ValueTask.CompletedTask;
    }

    private static (string FileName, string Arguments) ResolveCommand(EmulatorProcessOptions options)
    {
        var entryPoint = options.EntryPointPath;
        var prefix = string.Join(' ', options.PrefixArguments.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
        var ipc = "--ipc";
        var arguments = string.IsNullOrWhiteSpace(prefix) ? ipc : $"{prefix} {ipc}";

        if (entryPoint.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return ("dotnet", $"\"{entryPoint}\" {arguments}");
        }

        return (entryPoint, arguments);
    }

    private static int? TryGetExitCode(Process process)
    {
        try
        {
            return process.ExitCode;
        }
        catch
        {
            return null;
        }
    }
}
