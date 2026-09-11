using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Dorado.Tests.Visual;

/// <summary>
/// Golden-image gate for the desktop UI. Renders every screen in
/// <see cref="ScreenCatalog"/> headlessly with Skia and compares it to a committed
/// golden PNG. Regenerate with <c>DORADO_UPDATE_GOLDEN=1 dotnet test ...</c>.
/// </summary>
public class VisualRegressionTests
{
    private static string GoldenDir { get; } = ResolveGoldenDir();

    [AvaloniaFact]
    public void AllScreens_MatchGolden()
    {
        bool update = Environment.GetEnvironmentVariable("DORADO_UPDATE_GOLDEN") == "1";
        Directory.CreateDirectory(GoldenDir);

        var failures = new List<string>();
        int updated = 0, compared = 0;

        foreach (var spec in ScreenCatalog.All)
        {
            string actualPath = Path.Combine(Path.GetTempPath(), $"dorado-visual-{spec.Name}.png");
            try
            {
                var vm = ScreenCatalog.NewShell();
                var content = spec.Build(vm);
                var window = new Window
                {
                    Width = spec.Width,
                    Height = spec.Height,
                    Background = Brushes.Black,
                    SystemDecorations = SystemDecorations.None,
                    Content = content
                };
                window.Show();
                Pump();
                var frame = window.CaptureRenderedFrame()
                    ?? throw new InvalidOperationException("CaptureRenderedFrame returned null");
                Pump();
                frame.Save(actualPath);
                window.Close();
                Pump();
            }
            catch (Exception ex)
            {
                failures.Add($"{spec.Name}: render failed — {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            var goldenPath = Path.Combine(GoldenDir, spec.Name + ".png");
            if (update)
            {
                File.Copy(actualPath, goldenPath, overwrite: true);
                updated++;
                continue;
            }

            if (!File.Exists(goldenPath))
            {
                failures.Add($"{spec.Name}: missing golden '{goldenPath}'. Run with DORADO_UPDATE_GOLDEN=1 to create it.");
                continue;
            }

            var (same, ratio) = ImageDiff.Compare(actualPath, goldenPath);
            compared++;
            if (!same)
            {
                failures.Add($"{spec.Name}: {ratio:P2} of pixels differ (tolerance {ImageDiff.ChangedRatioTolerance:P0}). Actual: {actualPath}");
            }
        }

        if (update)
        {
            Assert.True(true, $"Golden images updated: {updated}.");
            return;
        }

        Assert.True(failures.Count == 0,
            $"Visual regression failures ({failures.Count}) across {compared} screens:\n" + string.Join("\n", failures));
    }

    private static void Pump()
    {
        for (int i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static string ResolveGoldenDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Dorado.sln")))
            dir = dir.Parent;
        var root = dir?.FullName ?? AppContext.BaseDirectory;
        return Path.Combine(root, "tests", "Dorado.Tests.Visual", "Golden");
    }
}
