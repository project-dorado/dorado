using System.Text.Json;

namespace Dorado.Plugins.Host;

/// <summary>
/// Encrypted-at-rest is out of scope for the reference host; values are isolated per
/// plugin in a JSON file under the config directory. Credentials never leave the host
/// except through <c>storage/get</c> calls the owning plugin makes.
/// </summary>
public sealed class PluginStorage
{
    private readonly string _directory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public PluginStorage(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
    }

    public async Task<string?> GetAsync(string pluginId, string key, CancellationToken cancellationToken = default)
    {
        var store = await ReadAsync(pluginId, cancellationToken).ConfigureAwait(false);
        return store.TryGetValue(key, out var value) ? value : null;
    }

    public async Task SetAsync(string pluginId, string key, string value, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var store = await ReadUnlockedAsync(pluginId, cancellationToken).ConfigureAwait(false);
            store[key] = value;
            await WriteUnlockedAsync(pluginId, store, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Dictionary<string, string>> ReadAsync(string pluginId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadUnlockedAsync(pluginId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Dictionary<string, string>> ReadUnlockedAsync(string pluginId, CancellationToken cancellationToken)
    {
        var path = PathFor(pluginId);
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private async Task WriteUnlockedAsync(string pluginId, Dictionary<string, string> store, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(PathFor(pluginId), json, cancellationToken).ConfigureAwait(false);
    }

    private string PathFor(string pluginId)
    {
        var safe = new string(pluginId.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_').ToArray());
        return Path.Combine(_directory, $"{safe}.json");
    }
}
