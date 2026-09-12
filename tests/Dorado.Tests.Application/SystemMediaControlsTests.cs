using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dorado.Application.Interfaces;
using Dorado.Application.Services;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// OS media-controls seam + coordinator (2026-09-11 audit: MPRIS/SMTC were the
/// last non-hardware fidelity leftover). These tests cover the platform-agnostic
/// bridge; the native MPRIS2 binding is exercised separately when a session bus
/// is present.
/// </summary>
public class SystemMediaControlsTests
{
    private sealed class FakeControls : ISystemMediaControls
    {
        public List<(Track? Track, bool IsPlaying, TimeSpan Position, TimeSpan Duration)> Updates { get; } = new();
        public bool IsAvailable => true;
        public void Update(Track? track, bool isPlaying, TimeSpan position, TimeSpan duration)
            => Updates.Add((track, isPlaying, position, duration));

#pragma warning disable CS0067 // events are part of the contract; only some are raised in tests
        public event EventHandler? PlayPauseRequested;
        public event EventHandler? NextRequested;
        public event EventHandler? PreviousRequested;
        public event EventHandler? StopRequested;
        public event EventHandler<TimeSpan>? SeekRequested;
#pragma warning restore CS0067

        public void RaisePlayPause() => PlayPauseRequested?.Invoke(this, EventArgs.Empty);
        public void RaiseNext() => NextRequested?.Invoke(this, EventArgs.Empty);
        public void RaiseSeek(TimeSpan t) => SeekRequested?.Invoke(this, t);
    }

    private static Track Track(string title) => new()
    {
        Title = title,
        ArtistName = "Artist",
        AlbumTitle = "Album",
        Duration = TimeSpan.FromMinutes(3),
        FilePath = string.Empty
    };

    [Fact]
    public async Task Coordinator_PushesNowPlayingToControls()
    {
        var player = new PlaybackQueueCoordinator();
        var controls = new FakeControls();
        using var coordinator = new SystemMediaControlsCoordinator(player, controls);

        await player.PlayTrackAsync(Track("Subdivisions"));

        Assert.NotEmpty(controls.Updates);
        var last = controls.Updates[^1];
        Assert.Equal("Subdivisions", last.Track?.Title);
        Assert.True(last.IsPlaying);
    }

    [Fact]
    public async Task Coordinator_RoutesInboundCommandsToPlayer()
    {
        var player = new PlaybackQueueCoordinator();
        var controls = new FakeControls();
        using var coordinator = new SystemMediaControlsCoordinator(player, controls);

        var t1 = Track("One");
        var t2 = Track("Two");
        await player.PlayTrackAsync(t1, new[] { t1, t2 });
        Assert.Equal("One", player.CurrentTrack?.Title);

        controls.RaiseNext();
        await WaitUntilAsync(() => player.CurrentTrack?.Title == "Two");
        Assert.Equal("Two", player.CurrentTrack?.Title);

        controls.RaisePlayPause();
        await WaitUntilAsync(() => player.State == PlaybackState.Paused);
        Assert.Equal(PlaybackState.Paused, player.State);
    }

    [Fact]
    public void NullControls_AreInert()
    {
        var controls = NullSystemMediaControls.Instance;
        Assert.False(controls.IsAvailable);

        // Must not throw.
        controls.Update(Track("x"), true, TimeSpan.Zero, TimeSpan.Zero);
    }

    [Fact]
    public void Coordinator_ReportsAvailability()
    {
        var player = new PlaybackQueueCoordinator();
        using var available = new SystemMediaControlsCoordinator(player, new FakeControls());
        using var unavailable = new SystemMediaControlsCoordinator(player, NullSystemMediaControls.Instance);

        Assert.True(available.IsAvailable);
        Assert.False(unavailable.IsAvailable);
    }

    [Fact]
    public async Task MprisControls_BadAddress_IsSafeAndReportsFailure()
    {
        // A non-existent bus must never throw; registration simply fails.
        using var connection = new Tmds.DBus.Protocol.DBusConnection("unix:path=/tmp/dorado-no-such-bus");
        using var mpris = new Dorado.Infrastructure.Audio.MprisMediaControls(connection);

        Assert.True(mpris.IsAvailable); // a connection object was created; registration is best-effort

        for (int i = 0; i < 200 && mpris.InitializationError is null; i++)
        {
            await Task.Delay(10);
        }

        Assert.NotNull(mpris.InitializationError);
        Assert.False(mpris.IsRegistered);
    }

    [Fact]
    public void Factory_SelectsBySessionBusCapability()
    {
        var controls = Dorado.Infrastructure.Audio.SystemMediaControlsFactory.Create();
        var hasBus = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS"));

        if (hasBus && OperatingSystem.IsLinux())
        {
            Assert.IsType<Dorado.Infrastructure.Audio.MprisMediaControls>(controls);
        }
        else
        {
            Assert.IsType<NullSystemMediaControls>(controls);
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }
}
