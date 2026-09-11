using System;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Dorado.Application.Services;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;
using Dorado.Tests.Application.TestFakes;
using Dorado.UI.Navigation;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

public class PageStackNavigationTests
{
    private static MainShellViewModel NewShell()
    {
        return new MainShellViewModel(
            new PlaybackQueueCoordinator(),
            new FakeMediaLibraryService(),
            new FakeDeviceSyncService(),
            new SmartDJEngine());
    }

    [Fact]
    public void PageStack_BasicPushPopPeek_BehavesCorrectly()
    {
        var stack = new PageStack();
        Assert.False(stack.CanNavigateBack);
        Assert.Equal(0, stack.Depth);
        Assert.Null(stack.CurrentEntry);

        var entry1 = new PageStackEntry(NavigationPivot.Quickplay, "QUICKPLAY");
        stack.Push(entry1);
        Assert.Equal(entry1, stack.CurrentEntry);
        Assert.False(stack.CanNavigateBack);
        Assert.Equal(0, stack.Depth);
        Assert.Equal(NavigationDirection.Forward, stack.LastDirection);

        var entry2 = new PageStackEntry(NavigationPivot.Collection, "COLLECTION");
        stack.Push(entry2);
        Assert.Equal(entry2, stack.CurrentEntry);
        Assert.True(stack.CanNavigateBack);
        Assert.Equal(1, stack.Depth);
        Assert.Equal(entry1, stack.Peek());

        var popped = stack.Pop();
        Assert.Equal(entry1, popped);
        Assert.Equal(entry1, stack.CurrentEntry);
        Assert.False(stack.CanNavigateBack);
        Assert.Equal(0, stack.Depth);
        Assert.Equal(NavigationDirection.Back, stack.LastDirection);

        var emptyPop = stack.Pop();
        Assert.Null(emptyPop);
    }

    [Fact]
    public void PageStack_MaximumSize_TrimsOldestEntries()
    {
        var stack = new PageStack { MaximumStackSize = 3 };

        stack.Push(new PageStackEntry(NavigationPivot.Quickplay, "0"));
        for (int i = 1; i <= 5; i++)
        {
            stack.Push(new PageStackEntry(NavigationPivot.Collection, i.ToString()));
        }

        Assert.True(stack.Depth <= 3);
        Assert.Equal("5", stack.CurrentEntry?.HeaderTitle);
    }

    [AvaloniaFact]
    public void TwoTierCollection_MediaGroupsAndSubPivots_ToggleCorrectly()
    {
        var shell = NewShell();
        var collection = shell.CollectionVM;

        Assert.True(collection.IsMusicActive);
        Assert.False(collection.IsVideosActive);
        Assert.False(collection.IsPicturesActive);
        Assert.False(collection.IsPodcastsActive);

        // Switch to Videos
        collection.SelectMediaGroupCommand.Execute(CollectionMediaGroup.Videos);
        Assert.False(collection.IsMusicActive);
        Assert.True(collection.IsVideosActive);

        // Switch to Pictures
        collection.SelectMediaGroupCommand.Execute(CollectionMediaGroup.Pictures);
        Assert.False(collection.IsVideosActive);
        Assert.True(collection.IsPicturesActive);

        // Switch to Podcasts
        collection.SelectMediaGroupCommand.Execute(CollectionMediaGroup.Podcasts);
        Assert.False(collection.IsPicturesActive);
        Assert.True(collection.IsPodcastsActive);

        // Switch back to Music
        collection.SelectMediaGroupCommand.Execute(CollectionMediaGroup.Music);
        Assert.True(collection.IsMusicActive);

        // Switch subpivots inside Music
        collection.SelectSubPivotCommand.Execute(CollectionSubPivot.Albums);
        Assert.True(collection.IsAlbumsActive);
        Assert.False(collection.IsArtistsActive);

        collection.SelectSubPivotCommand.Execute(CollectionSubPivot.Songs);
        Assert.True(collection.IsSongsActive);
        Assert.False(collection.IsAlbumsActive);
    }

    [AvaloniaFact]
    public void SongRatingCycle_CyclesThroughNoneFavoriteDislike()
    {
        var shell = NewShell();
        var collection = shell.CollectionVM;

        var track = new Track
        {
            Title = "Test Song",
            ArtistName = "Test Artist",
            Rating = HeartRating.None
        };
        collection.Songs.Add(track);

        Assert.Equal(HeartRating.None, track.Rating);
        Assert.True(track.IsNeutral);
        Assert.False(track.IsFavorite);
        Assert.False(track.IsDisliked);

        // Cycle 1: None -> Favorite
        collection.CycleRatingCommand.Execute(track);
        Assert.Equal(HeartRating.Favorite, track.Rating);
        Assert.True(track.IsFavorite);
        Assert.False(track.IsNeutral);
        Assert.False(track.IsDisliked);

        // Cycle 2: Favorite -> Dislike
        collection.CycleRatingCommand.Execute(track);
        Assert.Equal(HeartRating.Dislike, track.Rating);
        Assert.True(track.IsDisliked);
        Assert.False(track.IsFavorite);
        Assert.False(track.IsNeutral);

        // Cycle 3: Dislike -> None
        collection.CycleRatingCommand.Execute(track);
        Assert.Equal(HeartRating.None, track.Rating);
        Assert.True(track.IsNeutral);
        Assert.False(track.IsFavorite);
        Assert.False(track.IsDisliked);
    }

    [AvaloniaFact]
    public void CollectionDrillDown_ArtistNavigation_UpdatesCroppedHeaderAndGoesBack()
    {
        var shell = NewShell();
        shell.ActivePivot = NavigationPivot.Collection;

        Assert.Equal("COLLECTION", shell.CroppedHeaderTitle);
        Assert.False(shell.IsCroppedHeaderDetail);

        var artist = new Artist { Name = "Pink Floyd" };
        shell.CollectionVM.SelectedArtist = artist;

        // Drill-down should reflect artist name in cropped header and enable detail back affordance
        Assert.Equal("PINK FLOYD", shell.CroppedHeaderTitle);
        Assert.True(shell.IsCroppedHeaderDetail);
        Assert.True(shell.IsCroppedHeaderBack);
        Assert.True(shell.CanGoBack);

        // GoBack() should first retreat from artist detail to collection root
        shell.GoBack();
        Assert.Null(shell.CollectionVM.SelectedArtist);
        Assert.Equal("COLLECTION", shell.CroppedHeaderTitle);
        Assert.False(shell.IsCroppedHeaderDetail);
    }

    [AvaloniaFact]
    public void HasDisc_ReflectsDiscTracksAndPivotState()
    {
        var shell = NewShell();

        // Fresh: no disc is seeded (no optical-drive detection in this build), so the
        // DISC pivot stays ephemeral and is hidden.
        Assert.False(shell.HasDisc);

        // Loading a session -> true
        shell.CDVM.LoadSimulatedDisc();
        Assert.True(shell.HasDisc);
        Assert.True(shell.CDVM.DiscTracks.Count > 0);

        // Ejecting -> false
        shell.CDVM.EjectDisc();
        Assert.False(shell.HasDisc);

        // Activating Disc pivot forces HasDisc to true so UI doesn't lose context
        shell.ActivePivot = NavigationPivot.Disc;
        Assert.True(shell.HasDisc);
    }
}
