namespace Dorado.Application.Interfaces;

/// <summary>Runtime UI string localization with per-locale fallback.</summary>
public interface ILocalizationService
{
    string CurrentLocale { get; }

    IReadOnlyList<string> AvailableLocales { get; }

    string this[string key] { get; }

    void SetLocale(string locale);

    event EventHandler? LocaleChanged;
}
