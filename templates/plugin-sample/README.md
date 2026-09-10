# Dorado Plugin Sample

A minimal out-of-process plugin. Copy this folder, rename the `id` in `plugin.json`,
and start building.

## Build a `.znp` package

```bash
dotnet publish -c Release
# -> bin/Release/net8.0/Dorado.Plugin.Sample.znp
```

`dotnet publish` runs the `PackZnp` target, zipping the publish output (including
`plugin.json`) into a `.znp`.

## Install into Dorado

Settings → Software → **Plugins** → **INSTALL .ZNP…**, then enable it. Plugins live in:

- Linux: `~/.local/share/Dorado/plugins/<id>/`
- Windows: `%LOCALAPPDATA%\Dorado\plugins\<id>\`

## Host services

From `Context` (an `IPluginHostContext`) you can:

- `Logger` → `logger/log`
- `GetSecureStorageAsync` / `SetSecureStorageAsync` → `storage/get|set`
- `ShowToastAsync` → `ui/showToast`

See `.agents/skills/zune-plugins-protocol/SKILL.md` for the full event list and the
`player/*` and `library/queryTracks` host services.

## Events

Declare interest by subscribing in `OnStartAsync`:

```csharp
Subscribe<TrackChangedDto>("playback/trackChanged", dto => { /* ... */ return Task.CompletedTask; });
Subscribe<PlaybackStateDto>("playback/stateChanged", dto => { /* ... */ return Task.CompletedTask; });
Subscribe<RatingChangedDto>("rating/changed", dto => { /* ... */ return Task.CompletedTask; });
```
