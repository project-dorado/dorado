namespace Dorado.Plugins.Protocol.Rpc;

/// <summary>
/// Duplex line-framed transport used by the JSON-RPC channel. One UTF-8 JSON object
/// per line, mirroring the stdin/stdout contract of an out-of-process plugin.
/// </summary>
public interface ILineTransport : IAsyncDisposable
{
    bool IsRunning { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task WriteLineAsync(string line, CancellationToken cancellationToken = default);

    /// <summary>Returns the next line, or null when the peer closed the transport.</summary>
    Task<string?> ReadLineAsync(CancellationToken cancellationToken = default);

    /// <summary>Raised when the peer process/transport ends; the value is the exit code if known.</summary>
    event EventHandler<int?>? Exited;

    Task StopAsync();
}
