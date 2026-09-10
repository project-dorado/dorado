using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Services;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

public class PodcastMarkPlayedTests
{
    [AvaloniaFact]
    public async Task PodcastsViewModel_MarkAllPlayedCommand_SetsEveryEpisodePlayed()
    {
        var vm = new PodcastsViewModel(new PodcastService(new PlaybackQueueCoordinator()));
        await ((IAsyncRelayCommand)vm.RefreshCommand).ExecuteAsync(null);

        Assert.NotEmpty(vm.Podcasts);
        Assert.NotNull(vm.SelectedPodcast);
        Assert.True(vm.SelectedPodcast!.Episodes.Count > 0);

        Assert.Contains(vm.SelectedPodcast.Episodes, e => !e.IsPlayed);

        vm.MarkAllPlayedCommand.Execute(null);
        await Task.Yield();

        Assert.All(vm.SelectedPodcast.Episodes, e => Assert.True(e.IsPlayed));
        Assert.Equal(0, vm.SelectedPodcast.UnplayedCount);
    }

    [AvaloniaFact]
    public async Task PodcastsViewModel_MarkAllUnplayedCommand_ResetsEveryEpisode()
    {
        var vm = new PodcastsViewModel(new PodcastService(new PlaybackQueueCoordinator()));
        await ((IAsyncRelayCommand)vm.RefreshCommand).ExecuteAsync(null);

        vm.MarkAllPlayedCommand.Execute(null);
        await Task.Yield();
        Assert.All(vm.SelectedPodcast!.Episodes, e => Assert.True(e.IsPlayed));

        vm.MarkAllUnplayedCommand.Execute(null);
        await Task.Yield();
        Assert.All(vm.SelectedPodcast.Episodes, e => Assert.False(e.IsPlayed));
        Assert.Equal(vm.SelectedPodcast.Episodes.Count, vm.SelectedPodcast.UnplayedCount);
    }

    [AvaloniaFact]
    public void SettingsViewModel_PodcastsSubPivot_ComposesWithTopLevelPivot()
    {
        var vm = new SettingsViewModel();
        Assert.False(vm.IsPodcastsSubPivotActive);

        vm.SoftwarePivot = SoftwareSubPivot.Podcasts;
        Assert.True(vm.IsPodcastsSubPivotActive);

        // Top-level Device pivot must gate the software sub-pivot off
        vm.TopLevelPivot = SettingsTopLevelPivot.Device;
        Assert.False(vm.IsPodcastsSubPivotActive);
    }

    [AvaloniaFact]
    public void SettingsViewModel_PodcastsOptions_RoundTripsThroughAppSettings()
    {
        var vm = new SettingsViewModel();
        Assert.Contains("Everything", vm.PodcastKeepEpisodesOptions);
        Assert.Contains("Nothing", vm.PodcastKeepEpisodesOptions);

        vm.SelectedPodcastKeepEpisodes = "Nothing";
        Assert.Equal("Nothing", vm.SelectedPodcastKeepEpisodes);

        vm.PodcastAutoDownload = false;
        Assert.False(vm.PodcastAutoDownload);
    }
}
