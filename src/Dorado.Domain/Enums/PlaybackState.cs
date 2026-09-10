namespace Dorado.Domain.Enums;

public enum PlaybackState
{
    Stopped = 0,
    Playing = 1,
    Paused = 2
}

public enum DeviceSyncState
{
    Disconnected = 0,
    Connected = 1,
    Syncing = 2,
    SyncCompleted = 3,
    Error = 4
}
