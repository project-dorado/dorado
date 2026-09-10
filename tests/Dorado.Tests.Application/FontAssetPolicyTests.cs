using System.Text.RegularExpressions;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Policy guard: the desktop must never bundle Microsoft font binaries
/// (`AGENTS.md`: "Never bundle Microsoft fonts or firmware"). Only the OFL
/// Selawik family is permitted under the UI assets, and the theme must resolve
/// its primary face through it.
/// </summary>
public sealed class FontAssetPolicyTests
{
    private static readonly string[] FontExtensions = { ".ttf", ".ttc", ".otf", ".woff", ".woff2" };

    [Fact]
    public void DesktopBundlesOnlyOpenFonts()
    {
        var assets = Path.Combine(RepoRoot(), "src", "Dorado.UI", "Assets");
        if (!Directory.Exists(assets))
        {
            return; // sibling layout not present
        }

        var fonts = Directory.EnumerateFiles(assets, "*", SearchOption.AllDirectories)
            .Where(path => FontExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(fonts);
        Assert.All(fonts, font =>
            Assert.Contains("selawk", Path.GetFileName(font).ToLowerInvariant()));
    }

    [Fact]
    public void ThemeResolvesThroughSelawikNotSegoeZ()
    {
        var themePath = Path.Combine(RepoRoot(), "src", "Dorado.UI", "Styles", "ZuneTheme.axaml");
        if (!File.Exists(themePath))
        {
            return;
        }

        var theme = File.ReadAllText(themePath);

        // No resource font reference may point at a Segoe Z binary.
        Assert.DoesNotMatch(new Regex(@"avares://[^\r\n]*#Segoe\s*Z"), theme);

        // The three Zune families all resolve through the shared Selawik folder.
        var families = Regex.Matches(theme, "<FontFamily x:Key=\"Zune\\w*FontFamily\">([^<]+)</FontFamily>")
            .Select(m => m.Groups[1].Value)
            .ToList();
        Assert.Equal(3, families.Count);
        Assert.All(families, value => Assert.Contains("Assets/Selawik#Selawik", value));
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Dorado.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return AppContext.BaseDirectory;
    }
}
