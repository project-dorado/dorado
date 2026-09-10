using System.Text;
using Dorado.Plugins.Protocol.Rpc;

namespace Dorado.Plugins.Sdk;

/// <summary>
/// Plugin-side line transport over process stdin/stdout. Reference plugin executables
/// start here; the host connects to the other end of the pipe.
/// </summary>
public sealed class StdioLineTransport : ILineTransport
{
    private readonly Stream _input;
    private readonly Stream _output;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public StdioLineTransport() : this(Console.OpenStandardInput(), Console.OpenStandardOutput())
    {
    }

    public StdioLineTransport(Stream input, Stream output)
    {
        _input = input;
        _output = output;
    }

    public bool IsRunning { get; private set; }

    public event EventHandler<int?>? Exited;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _reader = new StreamReader(_input, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        _writer = new StreamWriter(_output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
        IsRunning = true;
        return Task.CompletedTask;
    }

    public async Task WriteLineAsync(string line, CancellationToken cancellationToken = default)
    {
        var writer = _writer ?? throw new InvalidOperationException("Transport not started.");
        await writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        var reader = _reader ?? throw new InvalidOperationException("Transport not started.");
        return await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync()
    {
        IsRunning = false;
        Exited?.Invoke(this, 0);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            _writer?.Flush();
        }
        catch
        {
            // Stream already closed.
        }

        return ValueTask.CompletedTask;
    }
}
