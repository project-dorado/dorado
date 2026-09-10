using Avalonia.Headless.XUnit;
using Dorado.Application.Services;
using Dorado.Domain.Models;
using Dorado.Tests.Application.TestFakes;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Phase F (JUMPLIST / JUMPINLIST parity): the A-Z alphabet jump-list exposed by
/// <see cref="CollectionViewModel"/> and its command wiring.
/// </summary>
public class CollectionJumpListTests
{
    private static MainShellViewModel NewShell()
    {
        return new MainShellViewModel(
            new PlaybackQueueCoordinator(),
            new FakeMediaLibraryService(),
            new FakeDeviceSyncService(),
            new SmartDJEngine());
    }

    [AvaloniaFact]
    public void AlphabetLetters_ContainsHashThenAtoZ()
    {
        var vm = NewShell().CollectionVM;

        Assert.Equal(27, vm.AlphabetLetters.Count);
        Assert.Equal("#", vm.AlphabetLetters[0]);
        Assert.Equal("A", vm.AlphabetLetters[1]);
        Assert.Equal("Z", vm.AlphabetLetters[26]);
    }

    [AvaloniaFact]
    public void JumpToLetterCommand_SelectsFirstMatchingArtist()
    {
        var vm = NewShell().CollectionVM;
        vm.ActiveSubPivot = CollectionSubPivot.Artists;
        vm.Artists.Add(new Artist { Name = "ABBA" });
        vm.Artists.Add(new Artist { Name = "Radiohead" });

        vm.JumpToLetterCommand.Execute("R");

        Assert.Equal("Radiohead", vm.SelectedArtist?.Name);
    }

    [AvaloniaFact]
    public void JumpToLetterCommand_SelectsFirstMatchingGenre()
    {
        var vm = NewShell().CollectionVM;
        vm.ActiveSubPivot = CollectionSubPivot.Genres;
        vm.Genres.Add("Ambient");
        vm.Genres.Add("Rock");

        vm.JumpToLetterCommand.Execute("R");

        Assert.Equal("Rock", vm.SelectedGenre);
    }

    [AvaloniaFact]
    public void JumpToLetterCommand_RaisesJumpRequestedWithLetter()
    {
        var vm = NewShell().CollectionVM;
        string? raised = null;
        vm.JumpRequested += (_, letter) => raised = letter;

        vm.JumpToLetterCommand.Execute("M");

        Assert.Equal("M", raised);
    }

    [AvaloniaFact]
    public void JumpToLetterCommand_IgnoresBlankLetter()
    {
        var vm = NewShell().CollectionVM;
        vm.ActiveSubPivot = CollectionSubPivot.Artists;
        vm.Artists.Add(new Artist { Name = "Radiohead" });

        vm.JumpToLetterCommand.Execute("  ");

        Assert.Null(vm.SelectedArtist);
    }
}
