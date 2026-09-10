using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dorado.Application.Interfaces;
using Dorado.Application.Services;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;
using Dorado.Infrastructure.Audio;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Phase 5 parity: real audio engine integration with the playback queue coordinator.
/// </summary>
public class AudioEngineParityTests
{
    private sealed class FakeAudioOutputEngine : IAudioOutputEngine
    {
        public bool IsAvailable { get; set; } = true;
        public bool HasActiveSource { get; set; }
        public bool LoadSucceeds { get; set; } = true;
        public List<string> LoadedUris { get; } = new();
        public string? LastTransitionUri { get; private set; }
        public double LastTransitionSeconds { get; private set; } = -1;
        public double LastVolume { get; private set; } = -1;
        public bool LastMuted { get; private set; }
        public int PlayCalls { get; private set; }
        public int PauseCalls { get; private set; }
        public int StopCalls { get; private set; }

        public event EventHandler? TrackEnded;
        public event EventHandler? TrackTransitioned;

        public void LoadAndPlay(string sourceUri)
        {
            LoadedUris.Add(sourceUri);
            HasActiveSource = IsAvailable && LoadSucceeds;
        }
        public void Play() => PlayCalls++;
        public void Pause() => PauseCalls++;
        public void Stop() => StopCalls++;
        public void SetVolume(double volume, bool muted)
        {
            LastVolume = volume;
            LastMuted = muted;
        }

        public void Seek(TimeSpan position) { }
        public TimeSpan GetPosition() => TimeSpan.Zero;
        public TimeSpan GetDuration() => TimeSpan.Zero;
        public float[] GetFftData() => Array.Empty<float>();
        public void SetAutoTransition(string? nextSourceUri, double crossfadeSeconds)
        {
            LastTransitionUri = nextSourceUri;
            LastTransitionSeconds = crossfadeSeconds;
        }

        public void RaiseTrackEnded() => TrackEnded?.Invoke(this, EventArgs.Empty);
        public void RaiseTrackTransitioned() => TrackTransitioned?.Invoke(this, EventArgs.Empty);
        public void Dispose() { }
    }

    private sealed class FakeReplayGainService : IReplayGainService
    {
        public double? GainDb { get; set; }

        public double? ReadTrackGainDb(string filePath) => GainDb;
    }

    private static Track MakeTrack(string title, string filePath = "") => new()
    {
        Title = title,
        ArtistName = "Artist",
        AlbumTitle = "Album",
        Duration = TimeSpan.FromSeconds(30),
        FilePath = filePath
    };

    [Fact]
    public void PlayTrackAsync_WithRealTrack_LoadsEngineAndSchedulesTransition()
    {
        var engine = new FakeAudioOutputEngine();
        var coordinator = new PlaybackQueueCoordinator(engine, new FakeReplayGainService());
        var first = MakeTrack("One", "/music/one.mp3");
        var second = MakeTrack("Two", "/music/two.mp3");

        coordinator.PlayTrackAsync(first, new[] { first, second });

        Assert.Equal(new[] { "/music/one.mp3" }, engine.LoadedUris);
        Assert.Equal("/music/two.mp3", engine.LastTransitionUri);
        Assert.Equal(2.0, engine.LastTransitionSeconds); // default crossfade
        Assert.Equal(PlaybackState.Playing, coordinator.State);
    }

    [Fact]
    public void EngineTrackEnded_AutoAdvancesQueue()
    {
        var engine = new FakeAudioOutputEngine();
        var coordinator = new PlaybackQueueCoordinator(engine, new FakeReplayGainService());
        var first = MakeTrack("One", "/music/one.mp3");
        var second = MakeTrack("Two", "/music/two.mp3");
        coordinator.PlayTrackAsync(first, new[] { first, second });

        engine.RaiseTrackEnded();

        Assert.Equal("Two", coordinator.CurrentTrack!.Title);
        Assert.Equal(PlaybackState.Playing, coordinator.State);
        Assert.Equal(2, engine.LoadedUris.Count);
        Assert.Equal("/music/two.mp3", engine.LoadedUris.Last());
    }

    [Fact]
    public void EngineTrackTransitioned_PromotesPendingTrack()
    {
        var engine = new FakeAudioOutputEngine();
        var coordinator = new PlaybackQueueCoordinator(engine, new FakeReplayGainService());
        var first = MakeTrack("One", "/music/one.mp3");
        var second = MakeTrack("Two", "/music/two.mp3");
        coordinator.PlayTrackAsync(first, new[] { first, second });

        Track? promoted = null;
        coordinator.TrackChanged += (_, e) => promoted = e.CurrentTrack;
        engine.RaiseTrackTransitioned();

        Assert.Equal("Two", coordinator.CurrentTrack!.Title);
        Assert.Equal("Two", promoted!.Title);
    }

    [Fact]
    public void ReplayGain_AppliesLinearFactorToVolume()
    {
        var engine = new FakeAudioOutputEngine();
        var rg = new FakeReplayGainService { GainDb = -6.0 };
        var coordinator = new PlaybackQueueCoordinator(engine, rg);
        var track = MakeTrack("One", "/music/one.mp3");
        coordinator.PlayTrackAsync(track, new[] { track });

        coordinator.VolumeLevelingEnabled = true;
        coordinator.Volume = 1.0;

        // -6 dB => 10^(-6/20) ≈ 0.5012
        Assert.Equal(0.5012, engine.LastVolume, 3);
        Assert.False(engine.LastMuted);
    }

    [Fact]
    public void VolumeLevelingDisabled_KeepsUnityVolume()
    {
        var engine = new FakeAudioOutputEngine();
        var rg = new FakeReplayGainService { GainDb = -6.0 };
        var coordinator = new PlaybackQueueCoordinator(engine, rg);
        var track = MakeTrack("One", "/music/one.mp3");
        coordinator.PlayTrackAsync(track, new[] { track });

        coordinator.Volume = 0.8;

        Assert.Equal(0.8, engine.LastVolume, 3);
    }

    [Fact]
    public void DemoTracksWithoutSources_StaySimulated()
    {
        var engine = new FakeAudioOutputEngine();
        var coordinator = new PlaybackQueueCoordinator(engine, new FakeReplayGainService());
        var demo = MakeTrack("Demo"); // FilePath empty

        coordinator.PlayTrackAsync(demo, new[] { demo });

        Assert.True(coordinator.IsSimulatedPlayback);
        Assert.Empty(engine.LoadedUris);
    }

    [Fact]
    public void ZeroCrossfadeWithGapless_SchedulesGaplessChain()
    {
        var engine = new FakeAudioOutputEngine();
        var coordinator = new PlaybackQueueCoordinator(engine, new FakeReplayGainService())
        {
            CrossfadeDurationSeconds = 0.0,
            GaplessEnabled = true
        };
        var first = MakeTrack("One", "/music/one.mp3");
        var second = MakeTrack("Two", "/music/two.mp3");

        coordinator.PlayTrackAsync(first, new[] { first, second });

        Assert.Equal("/music/two.mp3", engine.LastTransitionUri);
        Assert.Equal(0.0, engine.LastTransitionSeconds);
    }

    [Fact]
    public void ZeroCrossfadeWithoutGapless_CancelsAutoTransition()
    {
        var engine = new FakeAudioOutputEngine();
        var coordinator = new PlaybackQueueCoordinator(engine, new FakeReplayGainService())
        {
            CrossfadeDurationSeconds = 0.0,
            GaplessEnabled = false
        };
        var first = MakeTrack("One", "/music/one.mp3");
        var second = MakeTrack("Two", "/music/two.mp3");

        coordinator.PlayTrackAsync(first, new[] { first, second });

        Assert.Null(engine.LastTransitionUri);
    }

    [Fact]
    public void MissingReplayGainFile_ReturnsNullGracefully()
    {
        var service = new TagLibReplayGainService();

        Assert.Null(service.ReadTrackGainDb("/nonexistent/file.mp3"));
        Assert.Null(service.ReadTrackGainDb(string.Empty));
    }

    [Fact]
    public void BassEngine_InitializesOrDegradesGracefully()
    {
        // Exercises the full native-load path (BASS + add-ons) on every platform:
        // either a real device initializes, or the engine degrades to unavailable (headless/CI).
        using var engine = new BassAudioOutputEngine();

        engine.LoadAndPlay("/nonexistent/audio.mp3");

        if (engine.IsAvailable)
        {
            Assert.Equal(TimeSpan.Zero, engine.GetPosition());
        }

        Assert.Empty(engine.GetFftData());
        engine.SetVolume(0.5, muted: false);
        engine.Seek(TimeSpan.FromSeconds(1));
        engine.Pause();
        engine.Play();
        engine.Stop();
    }

    [Fact]
    public void PlayPause_TogglesEnginePlayAndPause()
    {
        var engine = new FakeAudioOutputEngine();
        var coordinator = new PlaybackQueueCoordinator(engine, new FakeReplayGainService());
        var track = MakeTrack("One", "/music/one.mp3");
        coordinator.PlayTrackAsync(track, new[] { track });

        coordinator.PlayPauseAsync();
        coordinator.PlayPauseAsync();

        Assert.Equal(1, engine.PauseCalls);
        Assert.Equal(1, engine.PlayCalls);
    }

    /// <summary>
    /// Regression: the engine must be initialized on the first play even though it reports
    /// IsAvailable=false before its first LoadAndPlay (the old code gated LoadAndPlay on the
    /// not-yet-initialized IsAvailable, so a real engine was never started and produced no sound).
    /// </summary>
    [Fact]
    public void PlayTrackAsync_WithUnavailableEngine_StillAttemptsToLoad()
    {
        var engine = new FakeAudioOutputEngine { IsAvailable = false };
        var coordinator = new PlaybackQueueCoordinator(engine, new FakeReplayGainService());
        var track = MakeTrack("One", "/music/one.mp3");

        coordinator.PlayTrackAsync(track, new[] { track });

        Assert.Contains("/music/one.mp3", engine.LoadedUris);
        Assert.True(coordinator.IsSimulatedPlayback);
    }

    /// <summary>
    /// When the device initializes but the source cannot be opened, position must fall back to
    /// the simulated clock rather than freezing at 0:00.
    /// </summary>
    [Fact]
    public void PlayTrackAsync_WhenSourceFailsToOpen_FallsBackToSimulated()
    {
        var engine = new FakeAudioOutputEngine { IsAvailable = true, LoadSucceeds = false };
        var coordinator = new PlaybackQueueCoordinator(engine, new FakeReplayGainService());
        var track = MakeTrack("One", "/music/one.mp3");

        coordinator.PlayTrackAsync(track, new[] { track });

        Assert.Contains("/music/one.mp3", engine.LoadedUris);
        Assert.True(coordinator.IsSimulatedPlayback);
    }

    /// <summary>
    /// The silent-fallback clock must actually advance the coordinator's position while a
    /// simulated track is playing (previously the clock service was never instantiated by the
    /// app, so the HUD stayed at 0:00).
    /// </summary>
    [Fact]
    public async Task AudioEngine_AdvancesSimulatedPlaybackClock()
    {
        var engine = new FakeAudioOutputEngine { IsAvailable = false };
        var coordinator = new PlaybackQueueCoordinator(engine, new FakeReplayGainService());
        using var clock = new AudioEngine(coordinator);

        var track = MakeTrack("Demo"); // No file path → simulated playback.
        await coordinator.PlayTrackAsync(track, new[] { track });
        Assert.True(coordinator.IsSimulatedPlayback);

        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (coordinator.CurrentPosition <= TimeSpan.Zero && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        Assert.True(coordinator.CurrentPosition > TimeSpan.Zero);
    }
}
