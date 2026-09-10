using System.Text.Json;
using System.Text.Json.Serialization;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;

namespace Dorado.Infrastructure.Persistence;

/// <summary>
/// Persists application settings as atomic JSON at %LocalAppData%/Dorado/settings.json.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private readonly string _settingsFilePath;

    public JsonSettingsStore(string? settingsFilePath = null)
    {
        _settingsFilePath = settingsFilePath
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dorado", "settings.json");
        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
            return settings ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var tempPath = _settingsFilePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, SerializerOptions));
            File.Move(tempPath, _settingsFilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings persistence is best-effort; never crash the app over it.
        }
    }
}
