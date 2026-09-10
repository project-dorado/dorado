using System.Diagnostics;
using System.Runtime.InteropServices;
using Dorado.Plugins.Protocol.Rpc;

namespace Dorado.Plugins.Host;

/// <summary>
/// Spawns a plugin executable and frames JSON-RPC lines over its stdin/stdout.
/// A <c>.dll</c> entry point is launched via the <c>dotnet</c> host; other entry
/// points are launched directly.
/// </summary>
public sealed class ProcessPluginTransport : ILineTransport
{
    private readonly InstalledPlugin _plugin;
    private Process? _process;
    private bool _stopping;

    public ProcessPluginTransport(InstalledPlugin plugin)
    {
        _plugin = plugin;
    }

    public bool IsRunning => _process is { HasExited: false };

    public event EventHandler<int?>? Exited;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_process is not null)
        {
            throw new InvalidOperationException("Transport already started.");
        }

        var (fileName, arguments) = ResolveCommand(_plugin);

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = _plugin.InstallDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
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
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    else
                    {
                        process.Kill(entireProcessTree: true);
                    }
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

    private static (string FileName, string Arguments) ResolveCommand(InstalledPlugin plugin)
    {
        var entryPoint = plugin.EntryPointPath;
        if (entryPoint.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return ("dotnet", $"\"{entryPoint}\"");
        }

        return (entryPoint, string.Empty);
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
