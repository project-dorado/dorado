using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.Infrastructure.Audio;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Capability-gated optical drive pipeline (2026-09-11 audit: real CD rip/burn).
/// No drive is present on the dev host, so the toolchain is exercised through the
/// injected process runner.
/// </summary>
public class OpticalDriveTests
{
    private const string SampleToc = "TOC:\n  1. 0:02.00 ( 150)\n  2. 4:12.00 (18900)\nTOTAL 8:20.00\n";

    private sealed class FakeRunner : IProcessRunner
    {
        public HashSet<string> Tools { get; } = new();
        public Func<string, ProcessResult> Handler { get; set; } = _ => new ProcessResult(0, string.Empty, string.Empty);
        public List<(string File, IReadOnlyList<string> Args)> Calls { get; } = new();

        public bool Exists(string fileName) => Tools.Contains(fileName);

        public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default, Action<string>? onStandardOutputLine = null)
        {
            Calls.Add((fileName, arguments));
            return Task.FromResult(Handler(fileName));
        }
    }

    [Fact]
    public void Unavailable_WithoutDrive()
    {
        var svc = new ProcessOpticalDriveService(new FakeRunner(), drivePresent: () => false);
        Assert.False(svc.IsAvailable);
    }

    [Fact]
    public void Unavailable_WithoutCdparanoia()
    {
        var svc = new ProcessOpticalDriveService(new FakeRunner(), drivePresent: () => true);
        Assert.False(svc.IsAvailable);
    }

    [Fact]
    public void Available_WithDriveAndTool()
    {
        var runner = new FakeRunner();
        runner.Tools.Add("cdparanoia");
        var svc = new ProcessOpticalDriveService(runner, drivePresent: () => true);
        Assert.True(svc.IsAvailable);
        Assert.Contains("drive=present", svc.CapabilitySummary);
    }

    [Fact]
    public async Task ReadToc_ParsesTracksAndDurations()
    {
        var runner = new FakeRunner();
        runner.Tools.Add("cdparanoia");
        runner.Handler = _ => new ProcessResult(0, SampleToc, string.Empty);
        var svc = new ProcessOpticalDriveService(runner, drivePresent: () => true);

        var tracks = await svc.ReadTocAsync();

        Assert.Equal(2, tracks.Count);
        Assert.Equal(1, tracks[0].Number);
        Assert.Equal(TimeSpan.FromSeconds(250), tracks[0].Duration);
        Assert.Equal(TimeSpan.FromSeconds(248), tracks[1].Duration);
    }

    [Fact]
    public async Task ReadToc_Throws_WhenUnavailable()
    {
        var svc = new ProcessOpticalDriveService(new FakeRunner(), drivePresent: () => false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ReadTocAsync());
    }

    [Fact]
    public async Task RipTrack_Flac_RunsCdparanoiaThenFfmpeg()
    {
        var runner = new FakeRunner();
        runner.Tools.Add("cdparanoia");
        runner.Tools.Add("ffmpeg");
        runner.Handler = _ => new ProcessResult(0, string.Empty, string.Empty);
        var svc = new ProcessOpticalDriveService(runner, drivePresent: () => true);
        var track = new OpticalTrack(3, 150, 18750, TimeSpan.FromSeconds(250), "Neon Transit");

        var output = await svc.RipTrackAsync(track, "/tmp/opencode/rip-test", "FLAC (Lossless Free Audio)");

        Assert.EndsWith(".flac", output);
        Assert.Contains("03 - Neon Transit", output);
        Assert.Equal(2, runner.Calls.Count);
        Assert.Equal("cdparanoia", runner.Calls[0].File);
        Assert.Equal("ffmpeg", runner.Calls[1].File);
    }

    [Fact]
    public async Task RipTrack_Wav_SkipsEncoder()
    {
        var runner = new FakeRunner();
        runner.Tools.Add("cdparanoia");
        var svc = new ProcessOpticalDriveService(runner, drivePresent: () => true);

        var output = await svc.RipTrackAsync(new OpticalTrack(1, 150, 1000, TimeSpan.FromSeconds(13), "Prologue"), "/tmp/opencode/rip-test", "WAV");

        Assert.EndsWith(".wav", output);
        Assert.Single(runner.Calls);
    }

    [Fact]
    public async Task Burn_RunsCdrdaoWithGeneratedToc()
    {
        var runner = new FakeRunner();
        runner.Tools.Add("cdparanoia");
        runner.Tools.Add("cdrdao");
        var svc = new ProcessOpticalDriveService(runner, drivePresent: () => true);

        await svc.BurnAsync(new[] { "/tmp/a.wav", "/tmp/b.wav" });

        var call = Assert.Single(runner.Calls);
        Assert.Equal("cdrdao", call.File);
        Assert.Contains("write", call.Args);
        Assert.Contains(call.Args, a => a.EndsWith(".toc"));
    }
}
