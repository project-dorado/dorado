using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.Application.Services;
using Dorado.Domain.Models;
using Dorado.Tests.Application.TestFakes;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Quick Mix progress + 5s timeout parity (2026-09-11 audit M-4 / Top-15 #7/#11).
/// </summary>
public class SmartDJProgressTests
{
    private sealed class RecordingProgress : IProgress<QuickMixProgress>
    {
        public List<QuickMixProgress> Reports { get; } = new();
        public void Report(QuickMixProgress value) => Reports.Add(value);
    }

    private static List<Track> Tracks(int n)
    {
        var list = new List<Track>();
        for (int i = 0; i < n; i++)
        {
            list.Add(new Track { Title = $"T{i}", ArtistName = "A", AlbumTitle = "Al", Genre = "Rock" });
        }
        return list;
    }

    [Fact]
    public async Task SmartDJEngine_ReportsProgressAndCompletes()
    {
        var engine = new SmartDJEngine();
        var progress = new RecordingProgress();
        var seed = new SmartDJSeed { TargetTrackCount = 5 };

        var mix = await engine.GenerateMixAsync(seed, Tracks(10), progress, CancellationToken.None);

        Assert.NotEmpty(mix);
        Assert.True(progress.Reports.Count >= 3);
        Assert.Equal(1.0, progress.Reports[^1].Fraction);
        Assert.Equal("Ready", progress.Reports[^1].Stage);
    }

    [Fact]
    public async Task SmartDJEngine_HonorsCancellation()
    {
        var engine = new SmartDJEngine();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            engine.GenerateMixAsync(new SmartDJSeed(), Tracks(5), null, cts.Token));
    }

    [Fact]
    public async Task QuickplayViewModel_QuickMix_ReadyStatus()
    {
        var lib = new FakeMediaLibraryService();
        lib.Tracks.AddRange(Tracks(6));
        var vm = new QuickplayViewModel(new PlaybackQueueCoordinator(), lib, new SmartDJEngine());

        await ((AsyncRelayCommand)vm.PlayFavoritesMixCommand).ExecuteAsync(null);

        Assert.NotNull(vm.QuickMixStatusText);
        Assert.Contains("ready", vm.QuickMixStatusText);
        Assert.Equal(1.0, vm.QuickMixProgress);
        Assert.False(vm.IsQuickMixBusy);
    }

    [Fact]
    public async Task QuickplayViewModel_QuickMix_TimeoutSetsStatus()
    {
        var original = QuickplayViewModel.QuickMixTimeout;
        QuickplayViewModel.QuickMixTimeout = TimeSpan.FromMilliseconds(80);
        try
        {
            var lib = new FakeMediaLibraryService();
            lib.Tracks.AddRange(Tracks(6));
            var vm = new QuickplayViewModel(new PlaybackQueueCoordinator(), lib, new SlowSmartDJService());

            await ((AsyncRelayCommand)vm.PlayFavoritesMixCommand).ExecuteAsync(null);

            Assert.Equal("Quick Mix timed out.", vm.QuickMixStatusText);
            Assert.False(vm.IsQuickMixBusy);
        }
        finally
        {
            QuickplayViewModel.QuickMixTimeout = original;
        }
    }

    private sealed class SlowSmartDJService : ISmartDJService
    {
        public async Task<IReadOnlyList<Track>> GenerateMixAsync(SmartDJSeed seed, IReadOnlyList<Track> libraryTracks)
        {
            // Ignores cancellation and never completes within the timeout window.
            await Task.Delay(TimeSpan.FromSeconds(10));
            return Array.Empty<Track>();
        }
    }
}
