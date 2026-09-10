using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Dorado.UI.Design;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Guards the Vector 4 clean-room rating glyphs: the Zune HD's
/// <c>RATING.*.PNG</c> artwork was a Microsoft asset and must not be bundled or
/// referenced — the vector hearts in <see cref="ZuneGlyphs"/> replace it.
/// </summary>
public sealed class ZuneGlyphsTests
{
    [AvaloniaFact]
    public void HeartAndBrokenHeart_AreDefined()
    {
        Assert.NotNull(ZuneGlyphs.Heart);
        Assert.NotNull(ZuneGlyphs.BrokenHeart);
        Assert.IsAssignableFrom<Geometry>(ZuneGlyphs.Heart);
        Assert.IsAssignableFrom<Geometry>(ZuneGlyphs.BrokenHeart);
    }

    [Theory]
    [InlineData("Rating")]
    [InlineData("Transport")]
    [InlineData("Window")]
    [InlineData("Slideshow")]
    [InlineData("Mixview")]
    [InlineData("Sync")]
    [InlineData("Social")]
    [InlineData("Branding")]
    [InlineData("CD")]
    public void RecreatedMicrosoftAssetFolders_AreRemoved(string folder)
    {
        var path = Path.Combine(RepoRoot(), "src", "Dorado.UI", "Assets", "Zune", folder);
        Assert.False(Directory.Exists(path), $"Microsoft '{folder}' artwork must not be bundled.");
    }

    [Theory]
    [InlineData("Assets/Zune/Rating")]
    [InlineData("Assets/Zune/Transport")]
    [InlineData("Assets/Zune/Window")]
    [InlineData("Assets/Zune/Slideshow")]
    [InlineData("Assets/Zune/Mixview")]
    [InlineData("Assets/Zune/Sync/")]
    [InlineData("Assets/Zune/Social")]
    [InlineData("Assets/Zune/Branding")]
    [InlineData("Assets/Zune/CD/")]
    public void ViewsDoNotReferenceRecreatedMicrosoftArtwork(string prefix)
    {
        var uiFolder = Path.Combine(RepoRoot(), "src", "Dorado.UI");
        if (!Directory.Exists(uiFolder))
        {
            return;
        }

        var offenders = Directory.EnumerateFiles(uiFolder, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains(prefix, StringComparison.Ordinal))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void NoMicrosoftNamedAssetsRemainUnderZune()
    {
        var zuneFolder = Path.Combine(RepoRoot(), "src", "Dorado.UI", "Assets", "Zune");
        if (!Directory.Exists(zuneFolder))
        {
            return;
        }

        // Name fragments unique to the extracted Microsoft asset set.
        string[] microsoftNames =
        {
            "USERBACKGROUND", "RATING.", "TRANSPORT.", "ICON.NOWPLAYING", "WINDOW.",
            "SLIDESHOW.", "MIX.", "PROFILE.", "ZUNELOGO", "ZUNECOLORLOGO",
            "ZUNEHDDEVICES", "ZUNEUSER", "QUICKMIXICON", "CDART", "CDLAND", "CDRIP",
            "SEGOEZ", "PODCASTS.EMPTY",
        };

        var offenders = Directory.EnumerateFiles(zuneFolder, "*", SearchOption.AllDirectories)
            .Where(path => microsoftNames.Any(fragment =>
                Path.GetFileName(path).Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void BackgroundsAreGeneratedArt()
    {
        var backgrounds = Path.Combine(RepoRoot(), "src", "Dorado.UI", "Assets", "Zune", "Backgrounds");
        if (!Directory.Exists(backgrounds))
        {
            return;
        }

        var files = Directory.EnumerateFiles(backgrounds, "*", SearchOption.AllDirectories).ToList();
        Assert.NotEmpty(files);
        Assert.All(files, path =>
            Assert.StartsWith("DORADO-BACKGROUND-", Path.GetFileName(path)));
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
