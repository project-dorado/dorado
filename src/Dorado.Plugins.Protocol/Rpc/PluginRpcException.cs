using Dorado.Plugins.Protocol.Messages;

namespace Dorado.Plugins.Protocol.Rpc;

/// <summary>Raised when the remote peer returns a JSON-RPC error object.</summary>
public sealed class PluginRpcException : Exception
{
    public int Code { get; }

    public PluginRpcException(JsonRpcError error)
        : base(error?.Message ?? "Unknown JSON-RPC error")
    {
        Code = error?.Code ?? 0;
    }

    public PluginRpcException(int code, string message) : base(message)
    {
        Code = code;
    }
}
