using Dorado.Application.Interfaces;

namespace Dorado.Application.Services;

public sealed class LocalizationService : ILocalizationService
{
    public static LocalizationService Default { get; } = new();

    private readonly LocalizationCatalog _catalog;
    private string _currentLocale = LocalizationCatalog.DefaultLocale;

    public LocalizationService(LocalizationCatalog? catalog = null)
    {
        _catalog = catalog ?? LocalizationCatalog.Default;
    }

    public string CurrentLocale => _currentLocale;

    public IReadOnlyList<string> AvailableLocales => _catalog.Locales;

    public string this[string key] => _catalog.Get(key, _currentLocale);

    public event EventHandler? LocaleChanged;

    public void SetLocale(string locale)
    {
        if (string.Equals(locale, _currentLocale, StringComparison.OrdinalIgnoreCase)
            || !_catalog.Locales.Contains(locale, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _currentLocale = locale;
        LocaleChanged?.Invoke(this, EventArgs.Empty);
    }
}
