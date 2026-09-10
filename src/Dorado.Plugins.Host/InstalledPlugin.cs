using System.Text.Json.Serialization;

namespace Dorado.Plugins.Host;

public sealed class PluginCapability
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("scopes")]
    public List<string> Scopes { get; set; } = new();
}

/// <summary>A plugin discovered on disk under the plugins directory.</summary>
public sealed class InstalledPlugin
{
    public required PluginManifest Manifest { get; init; }

    public required string InstallDirectory { get; init; }

    public string ManifestPath => Path.Combine(InstallDirectory, PluginManifest.FileName);

    public string EntryPointPath
    {
        get
        {
            var entry = Manifest.EntryPoint;
            return Path.IsPathRooted(entry) ? entry : Path.Combine(InstallDirectory, entry);
        }
    }
}
