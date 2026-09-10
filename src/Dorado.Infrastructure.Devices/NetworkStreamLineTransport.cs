using System.Net.Sockets;
using System.Text;
using Dorado.Plugins.Protocol.Rpc;

namespace Dorado.Infrastructure.Devices;

/// <summary>
/// Line-framed <see cref="ILineTransport"/> over a TCP <see cref="NetworkStream"/>.
/// One UTF-8 JSON object per line, matching the plugin/emulator framing so the
/// LAN sync protocol reuses <see cref="JsonRpcChannel"/> unchanged.
/// </summary>
public sealed class NetworkStreamLineTransport : ILineTransport
{
    private readonly NetworkStream _stream;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _running;

    public NetworkStreamLineTransport(NetworkStream stream)
    {
        _stream = stream;
    }

    public bool IsRunning => _running;

    public event EventHandler<int?>? Exited;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _running = true;
        return Task.CompletedTask;
    }

    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        var buffer = new MemoryStream();
        var single = new byte[1];
        try
        {
            while (true)
            {
                var read = await _stream.ReadAsync(single.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    // Peer closed the connection.
                    if (buffer.Length == 0 && _running)
                    {
                        await StopAsync().ConfigureAwait(false);
                    }
                    return buffer.Length == 0 ? null : Encoding.UTF8.GetString(buffer.ToArray());
                }

                if (single[0] == (byte)'\n')
                {
                    var line = Encoding.UTF8.GetString(buffer.ToArray());
                    return line.TrimEnd('\r');
                }

                buffer.WriteByte(single[0]);
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (IOException)
        {
            await StopAsync().ConfigureAwait(false);
            return null;
        }
    }

    public async Task WriteLineAsync(string line, CancellationToken cancellationToken = default)
    {
        var bytes = Encoding.UTF8.GetBytes(line + "\n");
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public Task StopAsync()
    {
        if (!_running)
        {
            return Task.CompletedTask;
        }

        _running = false;
        try
        {
            _stream.Close();
        }
        catch
        {
            // Already closed.
        }

        Exited?.Invoke(this, 0);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _running = false;
        try { _stream.Dispose(); } catch { /* observed */ }
        _writeLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
