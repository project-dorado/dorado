using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dorado.Plugins.Protocol.Messages;

namespace Dorado.Plugins.Protocol.Rpc;

/// <summary>
/// Bidirectional JSON-RPC 2.0 channel over a line transport. Supports host→plugin
/// requests/notifications and plugin→host requests (handled via <see cref="RequestHandler"/>).
/// </summary>
public sealed class JsonRpcChannel : IAsyncDisposable
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ILineTransport _transport;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement?>> _pending = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private int _nextId;
    private Task? _readLoop;
    private bool _disposed;

    public JsonRpcChannel(ILineTransport transport)
    {
        _transport = transport;
    }

    /// <summary>Invoked for requests originating from the plugin. Return the result payload.</summary>
    public Func<string, JsonElement?, Task<object?>>? RequestHandler { get; set; }

    /// <summary>Raised for notifications originating from the plugin.</summary>
    public event Action<string, JsonElement?>? NotificationReceived;

    /// <summary>Raised if the read loop faults unexpectedly.</summary>
    public event Action<Exception>? Faulted;

    public void Start()
    {
        _readLoop ??= Task.Run(ReadLoopAsync);
    }

    public async Task<JsonElement?> CallAsync(string method, object? parameters, CancellationToken cancellationToken = default)
    {
        var id = Interlocked.Increment(ref _nextId).ToString();
        var key = JsonSerializer.Serialize(id);
        var tcs = new TaskCompletionSource<JsonElement?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[key] = tcs;

        var request = new JsonRpcRequest { Id = id, Method = method, Params = parameters };
        await WriteLineAsync(JsonSerializer.Serialize(request, Options), cancellationToken).ConfigureAwait(false);

        using var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        return await tcs.Task.ConfigureAwait(false);
    }

    public Task NotifyAsync(string method, object? parameters, CancellationToken cancellationToken = default)
    {
        var notification = new JsonRpcNotification { Method = method, Params = parameters };
        return WriteLineAsync(JsonSerializer.Serialize(notification, Options), cancellationToken);
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var line = await _transport.ReadLineAsync(_cts.Token).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                if (!string.IsNullOrWhiteSpace(line))
                {
                    HandleLine(line);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        catch (Exception ex)
        {
            Faulted?.Invoke(ex);
        }
        finally
        {
            FailPending(new PluginRpcException(-32000, "Plugin transport closed."));
        }
    }

    private void HandleLine(string line)
    {
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(line);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return;
        }

        var hasId = root.TryGetProperty("id", out var idElement) && idElement.ValueKind != JsonValueKind.Null;
        var hasMethod = root.TryGetProperty("method", out var methodElement);

        if (hasId && hasMethod)
        {
            // Dispatch concurrently: a request handler may itself await a response
            // (e.g. initialize -> storage/set), which the read loop must be free to read.
            _ = HandleInboundRequestAsync(root, idElement, methodElement);
            return;
        }

        if (hasId)
        {
            HandleInboundResponse(root, idElement);
            return;
        }

        if (hasMethod)
        {
            var parameters = root.TryGetProperty("params", out var p) ? p.Clone() : (JsonElement?)null;
            NotificationReceived?.Invoke(methodElement.GetString() ?? string.Empty, parameters);
        }
    }

    private async Task HandleInboundRequestAsync(JsonElement root, JsonElement idElement, JsonElement methodElement)
    {
        var method = methodElement.GetString() ?? string.Empty;
        var parameters = root.TryGetProperty("params", out var p) ? p.Clone() : (JsonElement?)null;
        object? result = null;
        JsonRpcError? error = null;
        try
        {
            if (RequestHandler is not null)
            {
                result = await RequestHandler(method, parameters).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            error = new JsonRpcError { Code = -32603, Message = ex.Message };
        }

        var response = new JsonRpcResponse { Id = idElement.Clone(), Result = result, Error = error };
        await WriteLineAsync(JsonSerializer.Serialize(response, Options), _cts.Token).ConfigureAwait(false);
    }

    private void HandleInboundResponse(JsonElement root, JsonElement idElement)
    {
        var key = idElement.GetRawText();
        if (!_pending.TryRemove(key, out var tcs))
        {
            return;
        }

        if (root.TryGetProperty("error", out var errorElement) && errorElement.ValueKind == JsonValueKind.Object)
        {
            var error = errorElement.Deserialize<JsonRpcError>() ?? new JsonRpcError { Code = -32603, Message = "Unknown error" };
            tcs.TrySetException(new PluginRpcException(error));
            return;
        }

        JsonElement? result = root.TryGetProperty("result", out var r) ? r.Clone() : null;
        tcs.TrySetResult(result);
    }

    private async Task WriteLineAsync(string line, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _transport.WriteLineAsync(line, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private void FailPending(Exception exception)
    {
        foreach (var pair in _pending)
        {
            pair.Value.TrySetException(exception);
        }

        _pending.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        if (_readLoop is not null)
        {
            try { await _readLoop.ConfigureAwait(false); } catch { /* observed */ }
        }

        _cts.Dispose();
        _writeLock.Dispose();
    }
}
