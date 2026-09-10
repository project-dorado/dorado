using Avalonia;
using Avalonia.Headless;
using Dorado.Tests.Application;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Dorado.Tests.Application;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<Avalonia.Application>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
