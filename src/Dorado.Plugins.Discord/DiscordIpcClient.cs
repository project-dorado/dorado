using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Dorado.Plugins.Discord;

/// <summary>
/// Minimal Discord local RPC client. Connects to the first available
/// <c>discord-ipc-N</c> endpoint, performs the handshake, and sends SET_ACTIVITY frames.
/// Absence of Discord is a normal, non-fatal condition.
/// </summary>
public sealed class DiscordIpcClient : IAsyncDisposable
{
    private readonly string _clientId;
    private Stream? _stream;

    public DiscordIpcClient(string clientId)
    {
        _clientId = clientId;
    }

    public bool IsConnected => _stream is not null;

    public async Task<bool> TryConnectAsync(CancellationToken cancellationToken = default)
    {
        foreach (var candidate in CandidateEndpoints())
        {
            try
            {
                _stream = await ConnectAsync(candidate, cancellationToken).ConfigureAwait(false);
                var handshake = JsonSerializer.Serialize(new { v = 1, client_id = _clientId });
                await DiscordIpcFraming.WriteFrameAsync(_stream, DiscordIpcFraming.OpHandshake, handshake, cancellationToken).ConfigureAwait(false);

                var reply = await DiscordIpcFraming.ReadFrameAsync(_stream, cancellationToken).ConfigureAwait(false);
                if (reply is null)
                {
                    await ResetAsync().ConfigureAwait(false);
                    continue;
                }

                return true;
            }
            catch
            {
                await ResetAsync().ConfigureAwait(false);
            }
        }

        return false;
    }

    public async Task SendActivityAsync(DiscordSetActivity activity, CancellationToken cancellationToken = default)
    {
        var stream = _stream;
        if (stream is null)
        {
            return;
        }

        try
        {
            await DiscordIpcFraming.WriteFrameAsync(stream, DiscordIpcFraming.OpFrame, JsonSerializer.Serialize(activity), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await ResetAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync() => await ResetAsync().ConfigureAwait(false);

    private static async Task<Stream> ConnectAsync(string endpoint, CancellationToken cancellationToken)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var pipe = new NamedPipeClientStream(".", endpoint, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(250, cancellationToken).ConfigureAwait(false);
            return pipe;
        }

        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(endpoint), cancellationToken).ConfigureAwait(false);
        return new NetworkStream(socket, ownsSocket: true);
    }

    private static IEnumerable<string> CandidateEndpoints()
    {
        var explicitPath = Environment.GetEnvironmentVariable("DISCORD_IPC_PATH");
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            yield return explicitPath;
            yield break;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            for (var i = 0; i < 10; i++)
            {
                yield return $"discord-ipc-{i}";
            }

            yield break;
        }

        var directories = new List<string>();
        AddIfPresent(directories, Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR"));
        AddIfPresent(directories, Environment.GetEnvironmentVariable("TMPDIR"));

        var xdg = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (!string.IsNullOrWhiteSpace(xdg))
        {
            AddIfPresent(directories, Path.Combine(xdg, "app", "com.discordapp.Discord"));
            AddIfPresent(directories, Path.Combine(xdg, "snap.discord"));
        }

        AddIfPresent(directories, "/tmp");

        foreach (var directory in directories.Distinct())
        {
            for (var i = 0; i < 10; i++)
            {
                yield return Path.Combine(directory, $"discord-ipc-{i}");
            }
        }
    }

    private static void AddIfPresent(ICollection<string> list, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            list.Add(value);
        }
    }

    private async Task ResetAsync()
    {
        var stream = _stream;
        _stream = null;
        if (stream is not null)
        {
            try { await stream.DisposeAsync().ConfigureAwait(false); } catch { /* observed */ }
        }
    }
}
