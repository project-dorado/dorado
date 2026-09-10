using System.Text.Json;
using Dorado.Plugins.Protocol.Dto;
using Dorado.Plugins.Protocol.Rpc;

namespace Dorado.Plugins.Sdk;

/// <summary>
/// The plugin-side runtime. Drives an <see cref="IPlugin"/> over the JSON-RPC wire
/// contract: handles the host handshake, inbound event notifications, and proxies
/// host services (logger/storage/toast/library) back to the host.
/// </summary>
public static class PluginRuntime
{
    public static async Task RunAsync(IPlugin plugin, ILineTransport transport, CancellationToken cancellationToken = default)
    {
        await transport.StartAsync(cancellationToken).ConfigureAwait(false);

        await using var channel = new JsonRpcChannel(transport);
        var context = new HostContext(channel);
        channel.RequestHandler = async (method, parameters) =>
        {
            switch (method)
            {
                case "initialize":
                    await plugin.InitializeAsync(context, cancellationToken).ConfigureAwait(false);
                    return new { ok = true, id = plugin.Id, version = plugin.Version };
                case "shutdown":
                    await plugin.ShutdownAsync(cancellationToken).ConfigureAwait(false);
                    return new { ok = true };
                default:
                    throw new PluginRpcException(-32601, $"Method not found: {method}");
            }
        };

        channel.NotificationReceived += (method, parameters) =>
        {
            var payload = DeserializeEvent(method, parameters);
            _ = plugin.HandleEventAsync(method, payload);
        };

        channel.Start();
        try
        {
            // Run until the host closes the transport (read loop exits) or cancellation.
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private static object? DeserializeEvent(string method, JsonElement? parameters)
    {
        if (parameters is not { } element)
        {
            return null;
        }

        return method switch
        {
            "playback/trackChanged" => element.Deserialize<TrackChangedDto>(),
            "playback/stateChanged" => element.Deserialize<PlaybackStateDto>(),
            "rating/changed" => element.Deserialize<RatingChangedDto>(),
            _ => element.Clone()
        };
    }

    private sealed class HostContext : IPluginHostContext
    {
        private readonly JsonRpcChannel _channel;

        public HostContext(JsonRpcChannel channel)
        {
            _channel = channel;
            Logger = new ChannelLogger(channel);
        }

        public IPluginLogger Logger { get; }

        public async Task<string?> GetSecureStorageAsync(string key)
        {
            var result = await _channel.CallAsync("storage/get", new { key }).ConfigureAwait(false);
            if (result is { } element && element.ValueKind == JsonValueKind.Object && element.TryGetProperty("value", out var value))
            {
                return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
            }

            return null;
        }

        public async Task SetSecureStorageAsync(string key, string value)
        {
            await _channel.CallAsync("storage/set", new { key, value }).ConfigureAwait(false);
        }

        public async Task ShowToastAsync(string title, string message)
        {
            await _channel.CallAsync("ui/showToast", new { title, message }).ConfigureAwait(false);
        }
    }

    private sealed class ChannelLogger : IPluginLogger
    {
        private readonly JsonRpcChannel _channel;

        public ChannelLogger(JsonRpcChannel channel) => _channel = channel;

        public void LogInformation(string message, params object[] args)
            => Send("info", message, args);

        public void LogWarning(string message, params object[] args)
            => Send("warning", message, args);

        public void LogError(string message, Exception? exception, params object[] args)
            => Send("error", exception is null ? message : $"{message} :: {exception.Message}", args);

        private void Send(string level, string message, object[] args)
        {
            var text = args is { Length: > 0 } ? string.Format(message, args) : message;
            _ = _channel.NotifyAsync("logger/log", new { level, message = text });
        }
    }
}
