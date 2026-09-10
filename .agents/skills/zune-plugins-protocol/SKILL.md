---
name: zune-plugins-protocol
description: >-
  Use this skill when developing, testing, or integrating out-of-process plugins for Dorado,
  including JSON-RPC messaging contracts, event handling (playback, scrobbling, rich presence),
  and packaging .znp extensions.
---

# Dorado Plugin Architecture & Wire Protocol

This guide defines the out-of-process plugin architecture, JSON-RPC communication specification,
event subscriptions, and packaging standards for Dorado.

---

## 1. Process Isolation Architecture

To ensure player stability, security, and cross-platform flexibility, Dorado executes all plugins
in **isolated out-of-process worker sandboxes**:

- Plugins do **not** run inside the main player UI process. A crashed or hung plugin cannot crash or freeze audio playback.
- Communication takes place over standard bidirectional **JSON-RPC 2.0** over Standard I/O pipes (stdin/stdout) or local domain sockets (`unix:` on Linux, named pipes `\\.\pipe\` on Windows).
- The host process monitors plugin health, handles automatic restarts, and regulates resource limits.

---

## 2. Plugin Manifest (`plugin.json`)

Every plugin package includes a `plugin.json` descriptor:

```json
{
  "$schema": "https://raw.githubusercontent.com/project-dorado/dorado/main/plugin-schema.json",
  "id": "com.dorado.discord",
  "name": "Discord Rich Presence",
  "version": "1.0.0",
  "author": "Dorado Team",
  "description": "Displays current track, artist, album art, and play status on your Discord profile.",
  "sdkVersion": "1.0",
  "entryPoint": "Dorado.Plugin.Discord.dll",
  "capabilities": [
    {
      "type": "event-listening",
      "scopes": ["playback", "metadata"]
    },
    {
      "type": "network-access",
      "scopes": ["ipc"]
    }
  ]
}
```

---

## 3. JSON-RPC Protocol & Methods

### Lifecycle Methods
- `initialize`: Host sends player version, OS info, and user preferences.
- `shutdown`: Graceful termination notification before process termination.

### Event Subscriptions
Plugins declare interest in specific event scopes and receive push notifications from the host:

#### `playback/trackChanged`
Fires when the active track changes or starts playing:
```json
{
  "jsonrpc": "2.0",
  "method": "playback/trackChanged",
  "params": {
    "track": {
      "id": "uuid-here",
      "title": "Subdivisions",
      "artist": "Rush",
      "album": "Signals",
      "albumArtist": "Rush",
      "durationMs": 334000,
      "trackNumber": 1,
      "discNumber": 1,
      "year": 1982,
      "genre": "Progressive Rock",
      "rating": "Favorite",
      "artworkUrl": "local://art/album-rush-signals.jpg",
      "musicBrainzTrackId": "...",
      "musicBrainzArtistId": "..."
    },
    "positionMs": 0,
    "isPlaying": true
  }
}
```

#### `playback/stateChanged`
Fires on play, pause, stop, or seek:
```json
{
  "jsonrpc": "2.0",
  "method": "playback/stateChanged",
  "params": {
    "state": "Playing",
    "positionMs": 45120
  }
}
```

#### `rating/changed`
Fires when the user updates the heart rating:
```json
{
  "jsonrpc": "2.0",
  "method": "rating/changed",
  "params": {
    "trackId": "uuid-here",
    "rating": "Favorite"
  }
}
```

---

## 4. Host Services Provided to Plugins

Plugins can invoke methods on the host via standard JSON-RPC requests:
- `logger/log`: Log info, warning, or error messages into the central player log.
- `storage/get` & `storage/set`: Encrypted key-value persistence for credentials (e.g. Last.fm session tokens).
- `library/queryTracks`: Query tracks by artist, album, or search text.
- `ui/showToast`: Display a native Zune-styled sliding toast notification.

---

## 5. Packaging & Distribution (`.znp`)

- Plugin packages use the `.znp` extension (Zune Native Plugin), which is a standard zip archive.
- Contents:
  - `plugin.json` (at root)
  - Compiled plugin binaries and dependent DLLs / native libraries
  - Optional assets (icons, licenses)
- Installation: Drag-and-drop into Settings > Software > Plugins, or placing into the `~/.local/share/dorado/plugins` (Linux) or `%LOCALAPPDATA%\Dorado\plugins` (Windows) folder.
