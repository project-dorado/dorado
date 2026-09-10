using System.Threading.Channels;

namespace Dorado.Plugins.Protocol.Rpc;

/// <summary>
/// A pair of connected in-memory line transports. Used to host an in-process plugin
/// over the exact same JSON-RPC contract as a real out-of-process plugin (test harness
/// and built-in plugin support).
/// </summary>
public sealed class InMemoryLineTransport : ILineTransport
{
    private readonly ChannelWriter<string> _outbound;
    private readonly ChannelReader<string> _inbound;
    private bool _running;

    private InMemoryLineTransport(ChannelWriter<string> outbound, ChannelReader<string> inbound)
    {
        _outbound = outbound;
        _inbound = inbound;
    }

    public bool IsRunning => _running;

    public event EventHandler<int?>? Exited;

    public static (ILineTransport Host, ILineTransport Plugin) CreatePair()
    {
        var hostToPlugin = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        var pluginToHost = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

        var host = new InMemoryLineTransport(hostToPlugin.Writer, pluginToHost.Reader);
        var plugin = new InMemoryLineTransport(pluginToHost.Writer, hostToPlugin.Reader);
        return (host, plugin);
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _running = true;
        return Task.CompletedTask;
    }

    public Task WriteLineAsync(string line, CancellationToken cancellationToken = default)
        => _outbound.WriteAsync(line, cancellationToken).AsTask();

    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _inbound.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }

    public Task StopAsync()
    {
        _running = false;
        _outbound.TryComplete();
        Exited?.Invoke(this, 0);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _outbound.TryComplete();
        return ValueTask.CompletedTask;
    }
}
