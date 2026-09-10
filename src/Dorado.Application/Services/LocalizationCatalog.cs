namespace Dorado.Application.Services;

/// <summary>
/// In-memory string catalog. Dorado is English-first, so any key missing from a
/// locale falls back to English and then to the key itself (never throws).
/// </summary>
public sealed class LocalizationCatalog
{
    public const string DefaultLocale = "en";

    private readonly Dictionary<string, Dictionary<string, string>> _byLocale;

    public LocalizationCatalog(Dictionary<string, Dictionary<string, string>> byLocale, IReadOnlyList<string> locales)
    {
        _byLocale = byLocale;
        Locales = locales;
    }

    public static LocalizationCatalog Default { get; } = CreateDefault();

    public IReadOnlyList<string> Locales { get; }

    public string Get(string key, string locale)
    {
        if (_byLocale.TryGetValue(locale, out var strings) && strings.TryGetValue(key, out var value))
        {
            return value;
        }

        if (_byLocale.TryGetValue(DefaultLocale, out var fallback) && fallback.TryGetValue(key, out var english))
        {
            return english;
        }

        return key;
    }

    public IReadOnlyDictionary<string, string> StringsFor(string locale)
        => _byLocale.TryGetValue(locale, out var strings) ? strings : new Dictionary<string, string>();

    private static LocalizationCatalog CreateDefault()
    {
        var en = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pivot.software"] = "SOFTWARE",
            ["pivot.device"] = "DEVICE",
            ["sub.collection"] = "collection",
            ["sub.playback"] = "playback",
            ["sub.podcasts"] = "podcasts",
            ["sub.filetypes"] = "file types",
            ["sub.privacy"] = "privacy",
            ["sub.photos"] = "photos",
            ["sub.rip"] = "rip",
            ["sub.burn"] = "burn",
            ["sub.metadata"] = "metadata",
            ["sub.display"] = "display",
            ["sub.general"] = "general",
            ["sub.about"] = "about",
            ["sub.plugins"] = "plugins",
            ["general.language"] = "LANGUAGE",
            ["language.en"] = "English",
            ["language.fr"] = "Français"
        };

        var fr = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pivot.software"] = "LOGICIEL",
            ["pivot.device"] = "APPAREIL",
            ["sub.playback"] = "lecture",
            ["sub.filetypes"] = "types de fichiers",
            ["sub.privacy"] = "confidentialité",
            ["sub.rip"] = "extraction",
            ["sub.burn"] = "gravure",
            ["sub.metadata"] = "métadonnées",
            ["sub.display"] = "affichage",
            ["sub.general"] = "général",
            ["sub.about"] = "à propos",
            ["sub.plugins"] = "modules",
            ["general.language"] = "LANGUE",
            ["language.en"] = "Anglais",
            ["language.fr"] = "Français"
        };

        return new LocalizationCatalog(
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = en,
                ["fr"] = fr
            },
            new[] { "en", "fr" });
    }
}
