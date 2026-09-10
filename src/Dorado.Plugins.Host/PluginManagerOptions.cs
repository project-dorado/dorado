namespace Dorado.Plugins.Host;

public sealed class PluginManagerOptions
{
    /// <summary>Directory scanned for installed plugin folders.</summary>
    public string PluginsDirectory { get; init; } = DefaultPluginsDirectory();

    /// <summary>Directory holding enable-state and per-plugin secure storage.</summary>
    public string ConfigDirectory { get; init; } = DefaultConfigDirectory();

    public string HostVersion { get; init; } = "1.0.0";

    /// <summary>Maximum automatic restarts after an unexpected plugin exit.</summary>
    public int MaxRestarts { get; init; } = 3;

    public static string DefaultPluginsDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "Dorado", "plugins");
    }

    public static string DefaultConfigDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "Dorado", "plugin-data");
    }
}
