using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Dorado.UI;

[assembly: AvaloniaTestApplication(typeof(Dorado.Tests.Visual.VisualAppBuilder))]

namespace Dorado.Tests.Visual;

/// <summary>
/// Headless application configured with real Skia drawing (unlike the structural
/// headless tests) so rendered frames contain actual pixels for golden comparison.
/// Mirrors src/Dorado.Desktop/App.axaml.
/// </summary>
public class VisualAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<VisualApp>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public class VisualApp : Avalonia.Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;

        DataTemplates.Add(new ViewLocator());
        Resources["SystemAccentColor"] = Avalonia.Media.Color.Parse("#F10DA2");

        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://Dorado.UI/Styles/"))
        {
            Source = new Uri("avares://Dorado.UI/Styles/ZuneTheme.axaml")
        });
    }
}
