using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Dorado.Application.Interfaces;
using Dorado.Domain.Models;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

public class PodcastMarkPlayedTests
{
    [AvaloniaFact]
    public async Task PodcastsViewModel_MarkAllPlayedCommand_SetsEveryEpisodePlayed()
    {
        var vm = new PodcastsViewModel(NewService());
        await vm.LoadPodcastsAsync();

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
        var vm = new PodcastsViewModel(NewService());
        await vm.LoadPodcastsAsync();

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

    private static FakePodcastService NewService()
    {
        var service = new FakePodcastService();
        var series = new PodcastSeries { Title = "Test Series", UnplayedCount = 2 };
        series.Episodes.Add(new PodcastEpisode { SeriesTitle = "Test Series", Title = "Episode 1", IsPlayed = true });
        series.Episodes.Add(new PodcastEpisode { SeriesTitle = "Test Series", Title = "Episode 2", IsPlayed = false });
        series.Episodes.Add(new PodcastEpisode { SeriesTitle = "Test Series", Title = "Episode 3", IsPlayed = false });
        service.Series.Add(series);
        return service;
    }

    private sealed class FakePodcastService : IPodcastService
    {
        public List<PodcastSeries> Series { get; } = new();

        public Task<IReadOnlyList<PodcastSeries>> GetAllPodcastsAsync()
            => Task.FromResult<IReadOnlyList<PodcastSeries>>(Series);

        public Task<PodcastSeries?> GetPodcastByIdAsync(Guid id)
            => Task.FromResult(Series.FirstOrDefault(s => s.Id == id));

        public Task<PodcastSeries> SubscribeAsync(string feedUrl)
        {
            var series = new PodcastSeries { FeedUrl = feedUrl, Title = "Subscribed" };
            Series.Add(series);
            return Task.FromResult(series);
        }

        public Task UnsubscribeAsync(Guid seriesId)
        {
            Series.RemoveAll(s => s.Id == seriesId);
            return Task.CompletedTask;
        }

        public Task MarkEpisodePlayedAsync(Guid episodeId, bool isPlayed = true)
        {
            foreach (var series in Series)
            {
                var episode = series.Episodes.FirstOrDefault(e => e.Id == episodeId);
                if (episode != null)
                {
                    episode.IsPlayed = isPlayed;
                    series.UnplayedCount = series.Episodes.Count(e => !e.IsPlayed);
                    break;
                }
            }

            return Task.CompletedTask;
        }

        public Task PlayEpisodeAsync(PodcastEpisode episode) => Task.CompletedTask;

        public Task<IReadOnlyList<PodcastDirectoryEntry>> SearchDirectoryAsync(
            string query, int limit = 20, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PodcastDirectoryEntry>>(Array.Empty<PodcastDirectoryEntry>());
    }
}
