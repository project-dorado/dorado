using Dorado.Domain.Enums;
using Dorado.Domain.Models;

namespace Dorado.Application.Events;

public class TrackChangedEventArgs : EventArgs
{
    public Track? CurrentTrack { get; }
    public TimeSpan Position { get; }

    public TrackChangedEventArgs(Track? currentTrack, TimeSpan position)
    {
        CurrentTrack = currentTrack;
        Position = position;
    }
}

public class PlaybackStateChangedEventArgs : EventArgs
{
    public PlaybackState State { get; }
    public TimeSpan Position { get; }

    public PlaybackStateChangedEventArgs(PlaybackState state, TimeSpan position)
    {
        State = state;
        Position = position;
    }
}

public class HeartRatingChangedEventArgs : EventArgs
{
    public Guid TrackId { get; }
    public HeartRating Rating { get; }

    public HeartRatingChangedEventArgs(Guid trackId, HeartRating rating)
    {
        TrackId = trackId;
        Rating = rating;
    }
}
