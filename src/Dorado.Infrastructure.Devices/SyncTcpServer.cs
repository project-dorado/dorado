using System.Net;
using System.Net.Sockets;
using Dorado.Plugins.Protocol.Rpc;

namespace Dorado.Infrastructure.Devices;

/// <summary>
/// Hosts the LAN sync endpoint over TCP. Accepts phone connections, frames each
/// one as a <see cref="JsonRpcChannel"/>, and dispatches <c>sync.*</c> methods to
/// a shared <see cref="SyncEndpointHost"/>.
///
/// Discovery is mDNS (<see cref="SyncProtocol.ServiceType"/>); this server owns
/// the socket. A phone on the same network connects to <see cref="Port"/> and
/// speaks the JSON-RPC sync protocol.
/// </summary>
public sealed class SyncTcpServer : IAsyncDisposable
{
    private readonly SyncEndpointHost _host;
    private readonly IPAddress _address;
    private readonly List<Task> _connections = new();
    private readonly object _connectionsLock = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public SyncTcpServer(SyncEndpointHost host, IPAddress? address = null, int port = SyncProtocol.DefaultPort)
    {
        _host = host;
        _address = address ?? IPAddress.Any;
        Port = port;
    }

    public int Port { get; }

    /// <summary>True while the accept loop is running.</summary>
    public bool IsListening => _listener is not null;

    public void Start()
    {
        if (_listener is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _listener = new TcpListener(_address, Port);
        _listener.Start();
        _ = AcceptLoopAsync(_cts.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        var listener = _listener;
        if (listener is null)
        {
            return;
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                client.NoDelay = true;
                var task = HandleClientAsync(client, cancellationToken);
                lock (_connectionsLock)
                {
                    _connections.Add(task);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        catch (ObjectDisposedException)
        {
            // listener stopped
        }
        catch (SocketException)
        {
            // listener stopped
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            var transport = new NetworkStreamLineTransport(client.GetStream());
            await using var channel = new JsonRpcChannel(transport);
            channel.RequestHandler = _host.HandleAsync;
            await transport.StartAsync(cancellationToken).ConfigureAwait(false);
            channel.Start();

            // Keep the connection open until the peer disconnects or the server
            // shuts down; JsonRpcChannel owns the read loop.
            var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnExited(object? sender, int? code) => closed.TrySetResult();
            transport.Exited += OnExited;
            try
            {
                using var registration = cancellationToken.Register(() => closed.TrySetResult());
                await closed.Task.ConfigureAwait(false);
            }
            finally
            {
                transport.Exited -= OnExited;
            }
        }
    }

    public Task StopAsync()
    {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { /* observed */ }
        _listener = null;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _cts?.Dispose();
        _cts = null;
    }
}
