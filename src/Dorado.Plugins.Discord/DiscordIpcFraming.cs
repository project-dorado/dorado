using System.Text;

namespace Dorado.Plugins.Discord;

/// <summary>
/// Discord IPC frames: 8-byte little-endian header (opcode, payload length) followed by
/// a UTF-8 JSON payload.
/// </summary>
public static class DiscordIpcFraming
{
    public const int OpHandshake = 0;
    public const int OpFrame = 1;
    public const int OpClose = 2;
    public const int OpPing = 3;
    public const int OpPong = 4;

    private const int MaxPayloadBytes = 1_000_000;

    public static async Task WriteFrameAsync(Stream stream, int opcode, string json, CancellationToken cancellationToken = default)
    {
        var payload = Encoding.UTF8.GetBytes(json);
        var header = new byte[8];
        BitConverter.TryWriteBytes(header.AsSpan(0, 4), opcode);
        BitConverter.TryWriteBytes(header.AsSpan(4, 4), payload.Length);

        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<(int Opcode, string Json)?> ReadFrameAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var header = new byte[8];
        if (!await ReadExactAsync(stream, header, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var opcode = BitConverter.ToInt32(header, 0);
        var length = BitConverter.ToInt32(header, 4);
        if (length < 0 || length > MaxPayloadBytes)
        {
            return null;
        }

        var payload = new byte[length];
        if (!await ReadExactAsync(stream, payload, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return (opcode, Encoding.UTF8.GetString(payload));
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }
}
