using Dorado.Application.Events;
using Dorado.Application.Interfaces;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;
using Dorado.Plugins.Protocol.Dto;

namespace Dorado.Plugins.Host;

/// <summary>
/// Translates Dorado player events into plugin JSON-RPC notifications. This is the
/// seam that makes scrobbling (Last.fm) and presence (Discord) plugins possible.
/// </summary>
public sealed class PluginEventBridge : IDisposable
{
    private readonly IPlayerCoordinator _player;
    private readonly PluginManager _manager;
    private bool _attached;

    public PluginEventBridge(IPlayerCoordinator player, PluginManager manager)
    {
        _player = player;
        _manager = manager;
    }

    public void Attach()
    {
        if (_attached)
        {
            return;
        }

        _player.TrackChanged += OnTrackChanged;
        _player.StateChanged += OnStateChanged;
        _player.RatingChanged += OnRatingChanged;
        _attached = true;
    }

    public void Detach()
    {
        if (!_attached)
        {
            return;
        }

        _player.TrackChanged -= OnTrackChanged;
        _player.StateChanged -= OnStateChanged;
        _player.RatingChanged -= OnRatingChanged;
        _attached = false;
    }

    private void OnTrackChanged(object? sender, TrackChangedEventArgs e)
    {
        var dto = new TrackChangedDto
        {
            Track = Map(e.CurrentTrack),
            PositionMs = (long)e.Position.TotalMilliseconds,
            IsPlaying = _player.State == PlaybackState.Playing
        };

        _ = _manager.DispatchEventAsync("playback/trackChanged", dto);
    }

    private void OnStateChanged(object? sender, PlaybackStateChangedEventArgs e)
    {
        var dto = new PlaybackStateDto
        {
            State = e.State.ToString(),
            PositionMs = (long)e.Position.TotalMilliseconds
        };

        _ = _manager.DispatchEventAsync("playback/stateChanged", dto);
    }

    private void OnRatingChanged(object? sender, HeartRatingChangedEventArgs e)
    {
        var dto = new RatingChangedDto
        {
            TrackId = e.TrackId,
            Rating = RatingName(e.Rating)
        };

        _ = _manager.DispatchEventAsync("rating/changed", dto);
    }

    private static TrackDto? Map(Track? track)
    {
        if (track is null)
        {
            return null;
        }

        return new TrackDto
        {
            Id = track.Id,
            Title = track.Title,
            Artist = track.ArtistName,
            Album = track.AlbumTitle,
            DurationMs = (long)track.Duration.TotalMilliseconds,
            Rating = RatingName(track.Rating),
            ArtworkUri = track.ArtworkUri,
            MusicBrainzTrackId = track.MusicBrainzTrackId,
            MusicBrainzArtistId = track.MusicBrainzArtistId
        };
    }

    private static string RatingName(HeartRating rating) => rating switch
    {
        HeartRating.Favorite => "Favorite",
        HeartRating.Dislike => "Dislike",
        _ => "None"
    };

    public void Dispose() => Detach();
}
