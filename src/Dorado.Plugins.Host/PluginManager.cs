using System.Text.Json;
using System.Text.Json.Serialization;
using Dorado.Plugins.Protocol.Rpc;

namespace Dorado.Plugins.Host;

public sealed record PluginInfo(
    string Id,
    string Name,
    string Version,
    string Author,
    string Description,
    bool Enabled,
    PluginStatus Status,
    string? LastError,
    string? LastLog);

/// <summary>
/// Discovers, starts, stops, and supervises plugins, and fans player events out to
/// running plugins. This is the single host-side entry point the desktop app uses.
/// </summary>
public sealed class PluginManager : IAsyncDisposable
{
    private readonly PluginManagerOptions _options;
    private readonly Func<InstalledPlugin, ILineTransport> _transportFactory;
    private readonly PluginHostServices _hostServices;
    private readonly PluginPackageInstaller _installer;
    private readonly Dictionary<string, InstalledPlugin> _installed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RunningPlugin> _running = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _restarts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (string Level, string Message)> _logs = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _enabled = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _loadErrors = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _statePath;
    private bool _shuttingDown;

    public PluginManager(
        PluginManagerOptions options,
        Func<InstalledPlugin, ILineTransport> transportFactory,
        PluginHostServices hostServices)
    {
        _options = options;
        _transportFactory = transportFactory;
        _hostServices = hostServices;
        _installer = new PluginPackageInstaller(options.PluginsDirectory);
        _statePath = Path.Combine(options.ConfigDirectory, "plugins-state.json");
        Directory.CreateDirectory(options.PluginsDirectory);
        Directory.CreateDirectory(options.ConfigDirectory);
    }

    public event EventHandler? Changed;

    public string PluginsDirectory => _options.PluginsDirectory;

    public IReadOnlyList<PluginInfo> Plugins => _installed.Values
        .Select(installed =>
        {
            _running.TryGetValue(installed.Manifest.Id, out var running);
            _logs.TryGetValue(installed.Manifest.Id, out var log);
            return new PluginInfo(
                installed.Manifest.Id,
                installed.Manifest.Name,
                installed.Manifest.Version,
                installed.Manifest.Author,
                installed.Manifest.Description,
                _enabled.Contains(installed.Manifest.Id),
                running?.Status ?? PluginStatus.Stopped,
                running?.LastError,
                log.Message);
        })
        .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public IReadOnlyList<string> LoadErrors => _loadErrors;

    public void LoadInstalled()
    {
        _installed.Clear();
        _loadErrors.Clear();
        Directory.CreateDirectory(_options.PluginsDirectory);

        foreach (var directory in Directory.EnumerateDirectories(_options.PluginsDirectory))
        {
            var manifestPath = Path.Combine(directory, PluginManifest.FileName);
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                var manifest = PluginManifest.Load(manifestPath);
                var errors = manifest.Validate();
                if (errors.Count > 0)
                {
                    _loadErrors.Add($"{Path.GetFileName(directory)}: {string.Join("; ", errors)}");
                    continue;
                }

                _installed[manifest.Id] = new InstalledPlugin { Manifest = manifest, InstallDirectory = directory };
            }
            catch (Exception ex)
            {
                _loadErrors.Add($"{Path.GetFileName(directory)}: {ex.Message}");
            }
        }

        LoadState();
    }

    public async Task StartEnabledAsync(CancellationToken cancellationToken = default)
    {
        LoadInstalled();
        foreach (var id in _enabled.ToArray())
        {
            if (_installed.ContainsKey(id))
            {
                await StartPluginAsync(id, cancellationToken).ConfigureAwait(false);
            }
        }

        RaiseChanged();
    }

    public async Task DispatchEventAsync(string method, object payload, CancellationToken cancellationToken = default)
    {
        var targets = _running.Values.Where(r => r.Status == PluginStatus.Running).ToArray();
        foreach (var running in targets)
        {
            try
            {
                await running.SendEventAsync(method, payload, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                RecordLog(running.Plugin.Manifest.Id, "error", ex.Message);
            }
        }
    }

    public async Task SetEnabledAsync(string pluginId, bool enabled, CancellationToken cancellationToken = default)
    {
        if (enabled)
        {
            _enabled.Add(pluginId);
            _restarts.Remove(pluginId);
            await StartPluginAsync(pluginId, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _enabled.Remove(pluginId);
            await StopPluginAsync(pluginId).ConfigureAwait(false);
        }

        SaveState();
        RaiseChanged();
    }

    public async Task<InstalledPlugin> InstallAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        var installed = await _installer.InstallAsync(packagePath, cancellationToken).ConfigureAwait(false);
        _installed[installed.Manifest.Id] = installed;
        RaiseChanged();
        return installed;
    }

    public async Task UninstallAsync(string pluginId)
    {
        await StopPluginAsync(pluginId).ConfigureAwait(false);
        _installer.Uninstall(pluginId);
        _installed.Remove(pluginId);
        _enabled.Remove(pluginId);
        _restarts.Remove(pluginId);
        _logs.Remove(pluginId);
        SaveState();
        RaiseChanged();
    }

    private async Task StartPluginAsync(string pluginId, CancellationToken cancellationToken)
    {
        if (_running.ContainsKey(pluginId) || !_installed.TryGetValue(pluginId, out var installed))
        {
            return;
        }

        var transport = _transportFactory(installed);
        var running = new RunningPlugin(
            installed,
            transport,
            _options.HostVersion,
            (method, parameters) => _hostServices.HandleAsync(pluginId, method, parameters),
            (level, message) => RecordLog(pluginId, level, message));
        running.Changed += (_, _) => OnRunningChanged(pluginId);
        _running[pluginId] = running;

        try
        {
            await running.StartAsync(cancellationToken).ConfigureAwait(false);
            RecordLog(pluginId, "info", "started");
        }
        catch (Exception ex)
        {
            running.MarkError(ex.Message);
            RecordLog(pluginId, "error", ex.Message);
        }
    }

    private async Task StopPluginAsync(string pluginId)
    {
        if (_running.TryGetValue(pluginId, out var running))
        {
            _running.Remove(pluginId);
            try
            {
                await running.StopAsync().ConfigureAwait(false);
            }
            finally
            {
                await running.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private void OnRunningChanged(string pluginId)
    {
        if (_shuttingDown || !_running.TryGetValue(pluginId, out var running))
        {
            RaiseChanged();
            return;
        }

        if (running.Status == PluginStatus.Error && _enabled.Contains(pluginId))
        {
            var attempts = _restarts.TryGetValue(pluginId, out var count) ? count : 0;
            if (attempts < _options.MaxRestarts)
            {
                _restarts[pluginId] = attempts + 1;
                _ = RestartAsync(pluginId);
            }
        }

        RaiseChanged();
    }

    private async Task RestartAsync(string pluginId)
    {
        await Task.Delay(500).ConfigureAwait(false);
        if (_shuttingDown || !_enabled.Contains(pluginId))
        {
            return;
        }

        await StopPluginAsync(pluginId).ConfigureAwait(false);
        await StartPluginAsync(pluginId, CancellationToken.None).ConfigureAwait(false);
        RaiseChanged();
    }

    private void RecordLog(string pluginId, string level, string message)
    {
        _logs[pluginId] = (level, message);
    }

    private void LoadState()
    {
        _enabled.Clear();
        if (!File.Exists(_statePath))
        {
            return;
        }

        try
        {
            var state = JsonSerializer.Deserialize<PluginState>(File.ReadAllText(_statePath));
            if (state?.Enabled is not null)
            {
                foreach (var id in state.Enabled)
                {
                    _enabled.Add(id);
                }
            }
        }
        catch (JsonException)
        {
            // Ignore corrupt state; plugins default to disabled.
        }
    }

    private void SaveState()
    {
        var state = new PluginState { Enabled = _enabled.ToList() };
        File.WriteAllText(_statePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public async ValueTask DisposeAsync()
    {
        _shuttingDown = true;
        foreach (var id in _running.Keys.ToArray())
        {
            await StopPluginAsync(id).ConfigureAwait(false);
        }

        _gate.Dispose();
    }

    private sealed class PluginState
    {
        [JsonPropertyName("enabled")]
        public List<string> Enabled { get; set; } = new();
    }
}
