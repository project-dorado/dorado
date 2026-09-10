using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dorado.Plugins.Host;

/// <summary>Descriptor parsed from a plugin package's <c>plugin.json</c>.</summary>
public sealed class PluginManifest
{
    public const string FileName = "plugin.json";
    public const string SupportedSdkVersion = "1.0";

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("sdkVersion")]
    public string SdkVersion { get; set; } = SupportedSdkVersion;

    [JsonPropertyName("entryPoint")]
    public string EntryPoint { get; set; } = string.Empty;

    [JsonPropertyName("capabilities")]
    public List<PluginCapability> Capabilities { get; set; } = new();

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static PluginManifest Parse(string json)
    {
        var manifest = JsonSerializer.Deserialize<PluginManifest>(json, Options)
            ?? throw new InvalidDataException("plugin.json was empty or null.");
        return manifest;
    }

    public static PluginManifest Load(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("plugin.json not found.", manifestPath);
        }

        return Parse(File.ReadAllText(manifestPath));
    }

    /// <summary>Returns a list of human-readable validation errors; empty when valid.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Id))
        {
            errors.Add("'id' is required.");
        }
        else if (!IsSafeId(Id))
        {
            errors.Add("'id' may only contain letters, digits, '.', '-', and '_'.");
        }

        if (string.IsNullOrWhiteSpace(Name)) errors.Add("'name' is required.");
        if (string.IsNullOrWhiteSpace(Version)) errors.Add("'version' is required.");
        if (string.IsNullOrWhiteSpace(EntryPoint)) errors.Add("'entryPoint' is required.");

        if (!string.IsNullOrWhiteSpace(SdkVersion)
            && !string.Equals(SdkVersion, SupportedSdkVersion, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"'sdkVersion' '{SdkVersion}' is not supported (expected '{SupportedSdkVersion}').");
        }

        return errors;
    }

    public static bool IsSafeId(string id)
        => id.Length > 0 && id.All(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_');
}
