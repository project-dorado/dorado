using System.Text.Json;
using Dorado.Plugins.Protocol.Rpc;

namespace Dorado.Infrastructure.Emulator;

/// <summary>
/// JSON-RPC 2.0 bridge to a running <c>dorado --ipc</c> emulator process. The
/// transport is injected so tests can drive an <see cref="InMemoryLineTransport"/>
/// pair without spawning a sub-process.
/// </summary>
public sealed class JsonRpcEmulatorBridge : IEmulatorBridge
{
    private readonly JsonRpcChannel _channel;
    private readonly ILineTransport _transport;
    private readonly TimeSpan _callTimeout;
    private readonly Lazy<Task> _startTask;

    public JsonRpcEmulatorBridge(ILineTransport transport, TimeSpan? callTimeout = null)
    {
        _transport = transport;
        _callTimeout = callTimeout ?? TimeSpan.FromSeconds(60);
        _channel = new JsonRpcChannel(transport);
        _startTask = new Lazy<Task>(StartCoreAsync);
    }

    /// <summary>Starts the transport (spawns the process) and the read loop. Idempotent.</summary>
    public Task StartAsync() => _startTask.Value;

    private async Task StartCoreAsync()
    {
        await _transport.StartAsync().ConfigureAwait(false);
        _channel.Start();
    }

    public async Task<EmulatorPackageInfo> InspectAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        var result = await CallAsync("inspect", new { package = packagePath }, cancellationToken).ConfigureAwait(false);
        var files = new List<EmulatorPackageFile>();
        if (result.TryGetProperty("files", out var filesElement) && filesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var file in filesElement.EnumerateArray())
            {
                files.Add(new EmulatorPackageFile(
                    EmulatorJson.GetString(file, "path"),
                    EmulatorJson.GetStringOrNull(file, "container"),
                    file.TryGetProperty("size", out var size) && size.ValueKind == JsonValueKind.Number ? size.GetInt64() : 0));
            }
        }

        return new EmulatorPackageInfo(
            EmulatorJson.GetString(result, "kind"),
            EmulatorJson.GetStringOrNull(result, "title"),
            EmulatorJson.GetStringOrNull(result, "description"),
            EmulatorJson.GetStringOrNull(result, "executable"),
            EmulatorJson.GetStringOrNull(result, "startupAssembly"),
            EmulatorJson.GetStringOrNull(result, "platform"),
            EmulatorJson.GetStringOrNull(result, "guid"),
            EmulatorJson.GetStringOrNull(result, "runtimeProfile"),
            EmulatorJson.GetStringOrNull(result, "ccgameVersion"),
            EmulatorJson.GetBool(result, "encrypted"),
            EmulatorJson.GetInt(result, "entryCount"),
            EmulatorJson.GetInt(result, "fileCount"),
            files);
    }

    public async Task<EmulatorUnpackResult> UnpackAsync(string packagePath, string outputDirectory, CancellationToken cancellationToken = default)
    {
        var result = await CallAsync("unpack", new { package = packagePath, outputDir = outputDirectory }, cancellationToken).ConfigureAwait(false);
        return new EmulatorUnpackResult(
            EmulatorJson.GetInt(result, "written"),
            EmulatorJson.GetString(result, "outputDir"),
            EmulatorJson.GetStringOrNull(result, "error"));
    }

    public async Task<EmulatorAssemblyReport> RefsAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        var result = await CallAsync("refs", new { assembly = assemblyPath }, cancellationToken).ConfigureAwait(false);

        var members = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (result.TryGetProperty("memberReferences", out var memberElement) && memberElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in memberElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    members[property.Name] = property.Value.EnumerateArray()
                        .Where(v => v.ValueKind == JsonValueKind.String)
                        .Select(v => v.GetString()!)
                        .ToList();
                }
            }
        }

        return new EmulatorAssemblyReport(
            ReadStringArray(result, "assemblyReferences"),
            ReadStringArray(result, "typeReferences"),
            ReadStringArray(result, "xnaDerivedTypes"),
            members);
    }

    public async Task<EmulatorRunResult> RunAsync(string packagePath, int frames = 60, string? frameOutputPath = null, bool hash = false, CancellationToken cancellationToken = default)
    {
        var result = await CallAsync("run", new
        {
            package = packagePath,
            frames,
            @out = frameOutputPath,
            hash,
        }, cancellationToken).ConfigureAwait(false);

        return new EmulatorRunResult(
            EmulatorJson.GetString(result, "entryPoint"),
            EmulatorJson.GetInt(result, "framesRendered"),
            EmulatorJson.GetStringOrNull(result, "frameSha256"));
    }

    private async Task<JsonElement> CallAsync(string method, object parameters, CancellationToken cancellationToken)
    {
        await StartAsync().ConfigureAwait(false);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_callTimeout);
        try
        {
            var result = await _channel.CallAsync(method, parameters, timeout.Token).ConfigureAwait(false);
            if (result is null)
            {
                throw new EmulatorBridgeException($"Emulator method '{method}' returned no result.");
            }

            return result.Value;
        }
        catch (PluginRpcException ex)
        {
            throw new EmulatorBridgeException($"Emulator method '{method}' failed: {ex.Message}", ex);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EmulatorBridgeException($"Emulator method '{method}' timed out after {_callTimeout}.");
        }
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return array.EnumerateArray()
            .Where(v => v.ValueKind == JsonValueKind.String)
            .Select(v => v.GetString()!)
            .ToList();
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.DisposeAsync().ConfigureAwait(false);
        await _transport.DisposeAsync().ConfigureAwait(false);
    }
}
