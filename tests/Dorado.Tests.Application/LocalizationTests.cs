using Dorado.Application.Services;
using Dorado.UI.ViewModels;

namespace Dorado.Tests.Application;

public class LocalizationTests
{
    [Fact]
    public void English_is_the_default_and_localized_lookup_works()
    {
        var catalog = LocalizationCatalog.Default;

        Assert.Equal("SOFTWARE", catalog.Get("pivot.software", "en"));
        Assert.Equal("LOGICIEL", catalog.Get("pivot.software", "fr"));
        Assert.Equal("lecture", catalog.Get("sub.playback", "fr"));
    }

    [Fact]
    public void Missing_keys_fall_back_to_english_then_to_the_key()
    {
        var catalog = LocalizationCatalog.Default;

        // sub.collection is not translated to French; it must fall back to English.
        Assert.Equal("collection", catalog.Get("sub.collection", "fr"));
        Assert.Equal("does.not.exist", catalog.Get("does.not.exist", "fr"));
    }

    [Fact]
    public void Service_switches_locale_and_raises_change()
    {
        var service = new LocalizationService();
        var raised = 0;
        service.LocaleChanged += (_, _) => raised++;

        Assert.Equal("en", service.CurrentLocale);
        Assert.Equal("collection", service["sub.collection"]);

        service.SetLocale("fr");
        Assert.Equal("fr", service.CurrentLocale);
        Assert.Equal("gravure", service["sub.burn"]);
        Assert.Equal(1, raised);

        service.SetLocale("zz");
        Assert.Equal("fr", service.CurrentLocale); // unknown locale ignored
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Expanded_locales_are_available_and_translate()
    {
        var catalog = LocalizationCatalog.Default;

        // Expanded toward the Zune locale set (>= 20 locales including en/fr).
        Assert.True(catalog.Locales.Count >= 20, $"only {catalog.Locales.Count} locales");
        foreach (var code in new[] { "de", "es", "it", "pt", "nl", "sv", "pl", "ru", "ja", "ko", "zh-Hans", "zh-Hant" })
        {
            Assert.Contains(code, catalog.Locales);
            // Every shipped locale has a selector display name.
            Assert.NotEqual($"language.{code}", catalog.Get($"language.{code}", "en"));
        }

        Assert.Equal("GERÄT", catalog.Get("pivot.device", "de"));
        Assert.Equal("再生", catalog.Get("sub.playback", "ja"));
        Assert.Equal("播放", catalog.Get("sub.playback", "zh-Hans"));
        Assert.Equal("Русский", catalog.Get("language.ru", "en"));

        // Untranslated keys still fall back to English for a new locale.
        Assert.Equal("podcasts", catalog.Get("sub.podcasts", "de"));
    }

    [Fact]
    public void Service_switches_to_an_expanded_locale()
    {
        var service = new LocalizationService();
        service.SetLocale("de");
        Assert.Equal("de", service.CurrentLocale);
        Assert.Equal("GERÄT", service["pivot.device"]);
        Assert.Equal("sammlung", service["sub.collection"]);
    }

    [Fact]
    public void Settings_language_selection_applies_localization()
    {
        var localization = new LocalizationService();
        var settings = new SettingsViewModel(localization: localization);

        settings.SelectedLanguage = "fr";

        Assert.Equal("fr", localization.CurrentLocale);
        Assert.Equal("gravure", settings.Localization["sub.burn"]);
    }
}
