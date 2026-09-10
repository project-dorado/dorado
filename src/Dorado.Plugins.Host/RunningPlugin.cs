using System.Runtime.InteropServices;
using System.Text.Json;
using Dorado.Plugins.Protocol.Rpc;

namespace Dorado.Plugins.Host;

public enum PluginStatus
{
    Stopped,
    Starting,
    Running,
    Error
}

/// <summary>A single started plugin: transport, JSON-RPC channel, and lifecycle state.</summary>
public sealed class RunningPlugin : IAsyncDisposable
{
    private readonly ILineTransport _transport;
    private readonly JsonRpcChannel _channel;
    private readonly string _hostVersion;
    private readonly Func<string, JsonElement?, Task<object?>> _hostRequestHandler;
    private bool _disposed;

    public RunningPlugin(
        InstalledPlugin plugin,
        ILineTransport transport,
        string hostVersion,
        Func<string, JsonElement?, Task<object?>> hostRequestHandler,
        Action<string, string>? logSink = null)
    {
        Plugin = plugin;
        _transport = transport;
        _hostVersion = hostVersion;
        _hostRequestHandler = hostRequestHandler;
        _channel = new JsonRpcChannel(transport)
        {
            RequestHandler = (method, parameters) => _hostRequestHandler(method, parameters)
        };
        _channel.Faulted += ex => LastError = ex.Message;
        _channel.NotificationReceived += (method, parameters) =>
        {
            // Host services invoked as notifications (e.g. logger/log) carry no response.
            _ = _hostRequestHandler(method, parameters);
        };
        if (logSink is not null)
        {
            LogSink = logSink;
        }

        _transport.Exited += (_, code) =>
        {
            if (Status == PluginStatus.Running)
            {
                LastError = $"Plugin exited unexpectedly (code {code?.ToString() ?? "unknown"}).";
                SetStatus(PluginStatus.Error);
            }
        };
    }

    public InstalledPlugin Plugin { get; }

    public PluginStatus Status { get; private set; } = PluginStatus.Stopped;

    public string? LastError { get; private set; }

    private Action<string, string>? LogSink { get; }

    public event EventHandler? Changed;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        SetStatus(PluginStatus.Starting);
        await _transport.StartAsync(cancellationToken).ConfigureAwait(false);
        _channel.Start();

        var initParams = new
        {
            playerVersion = _hostVersion,
            os = RuntimeInformation.OSDescription,
            platform = RuntimeInformation.RuntimeIdentifier,
            preferences = new { theme = "zune" }
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await _channel.CallAsync("initialize", initParams, timeout.Token).ConfigureAwait(false);

        LastError = null;
        SetStatus(PluginStatus.Running);
        Log("info", "initialized");
    }

    public Task SendEventAsync(string method, object payload, CancellationToken cancellationToken = default)
        => Status == PluginStatus.Running
            ? _channel.NotifyAsync(method, payload, cancellationToken)
            : Task.CompletedTask;

    public void MarkError(string message)
    {
        LastError = message;
        SetStatus(PluginStatus.Error);
    }

    public async Task StopAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await _channel.CallAsync("shutdown", null, timeout.Token).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort graceful shutdown; fall through to transport teardown.
        }

        await _channel.DisposeAsync().ConfigureAwait(false);
        await _transport.StopAsync().ConfigureAwait(false);
        SetStatus(PluginStatus.Stopped);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _channel.DisposeAsync().ConfigureAwait(false);
        await _transport.DisposeAsync().ConfigureAwait(false);
    }

    private void Log(string level, string message)
        => LogSink?.Invoke(level, $"[{Plugin.Manifest.Id}] {message}");

    private void SetStatus(PluginStatus status)
    {
        Status = status;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
