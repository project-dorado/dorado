using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.Application.Services;
using Dorado.Tests.Application.TestFakes;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// CDViewModel ↔ real optical-drive integration (capability-gated): loads a real
/// TOC and rips when the drive is available; keeps the honest simulated path when not.
/// </summary>
public class CDOpticalIntegrationTests
{
    private sealed class FakeDrive : IOpticalDriveService
    {
        public bool IsAvailable { get; set; } = true;
        public string CapabilitySummary => "fake drive";
        public List<OpticalTrack> Toc { get; } = new();
        public List<OpticalTrack> Ripped { get; } = new();
        public List<IReadOnlyList<string>> Burns { get; } = new();

        public Task<IReadOnlyList<OpticalTrack>> ReadTocAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OpticalTrack>>(Toc);

        public Task<string> RipTrackAsync(OpticalTrack track, string destinationFolder, string format, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            Ripped.Add(track);
            progress?.Report(1.0);
            return Task.FromResult(Path.Combine(destinationFolder, $"{track.Number:00}.flac"));
        }

        public Task BurnAsync(IReadOnlyList<string> trackFilePaths, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            Burns.Add(trackFilePaths);
            progress?.Report(1.0);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task RealDrive_LoadsTocAndRips()
    {
        var drive = new FakeDrive();
        drive.Toc.Add(new OpticalTrack(1, 150, 1000, TimeSpan.FromSeconds(13), "Prologue"));
        drive.Toc.Add(new OpticalTrack(2, 1150, 1000, TimeSpan.FromSeconds(13), "Horizon"));

        var vm = new CDViewModel(new FakeMediaLibraryService(), new PlaybackQueueCoordinator(), null, drive);
        Assert.True(vm.IsRealDriveAvailable);

        await ((AsyncRelayCommand)vm.LoadDiscCommand).ExecuteAsync(null);
        Assert.Equal(2, vm.DiscTracks.Count);
        Assert.False(vm.IsSimulatedDisc);
        Assert.True(vm.HasDisc);

        await ((AsyncRelayCommand)vm.RipCdCommand).ExecuteAsync(null);
        Assert.Equal(2, drive.Ripped.Count);
        Assert.Equal(1.0, vm.RipProgress);
        Assert.Contains("Rip complete", vm.RipStatusText);
    }

    [Fact]
    public async Task NoDrive_KeepsSimulatedPath()
    {
        var vm = new CDViewModel(new FakeMediaLibraryService(), new PlaybackQueueCoordinator());
        Assert.False(vm.IsRealDriveAvailable);

        await ((AsyncRelayCommand)vm.LoadDiscCommand).ExecuteAsync(null);
        Assert.True(vm.IsSimulatedDisc);
        Assert.True(vm.HasDisc);

        await ((AsyncRelayCommand)vm.RipCdCommand).ExecuteAsync(null);
        Assert.Equal(1.0, vm.RipProgress);
        Assert.Contains("Simulation complete", vm.RipStatusText);
    }
}
