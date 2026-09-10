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
    public void Settings_language_selection_applies_localization()
    {
        var localization = new LocalizationService();
        var settings = new SettingsViewModel(localization: localization);

        settings.SelectedLanguage = "fr";

        Assert.Equal("fr", localization.CurrentLocale);
        Assert.Equal("gravure", settings.Localization["sub.burn"]);
    }
}
