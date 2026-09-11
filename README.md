# Dorado

<div align="center">

# 🎵 Dorado
**The authentic cross-platform spiritual successor to Microsoft Zune Desktop & Player**

[![Build & Release](https://github.com/project-dorado/dorado/actions/workflows/build.yml/badge.svg)](https://github.com/project-dorado/dorado/actions/workflows/build.yml)
[![Avalonia UI](https://img.shields.io/badge/Avalonia_UI-11.2-8C15E9?logo=avalonia&logoColor=white)](https://avaloniaui.net/)
[![Platforms](https://img.shields.io/badge/Platforms-Windows%20%7C%20Linux%20(x64%20%26%20arm64)-0078D7)]()
[![Design](https://img.shields.io/badge/Aesthetic-Zune%20Metro%20%2F%20Iris-FA2A55)]()
![Tests](https://img.shields.io/badge/tests-393%20passing-4c1?logo=xunit&logoColor=white)
![Parity](https://img.shields.io/badge/Zune%204.8%20parity-~85--brightgreen)

</div>

---

## ✨ Design Principles: The Zune "Metro" Experience

Dorado is built on the purest tenets of the original Microsoft Zune Desktop software:

- **Content Before Chrome:** Zero rounded corners (`CornerRadius = 0`), no drop shadows, no skeuomorphic gradients or faux-leather textures.
- **Typography as Art:** Sized and kerned with Segoe UI / Selawik metrics across Display, Pivot, Sub-pivot, and Caption hierarchies. Opacity communicates state (Active: 100%, Hover: 85%, Inactive: 40%).
- **Iconic Pivot Navigation:** Fluid deceleration panning across `QUICKPLAY`, `COLLECTION`, `DEVICE`, and `SETTINGS` — with pannable right-edge bleed at the authentic 734×500 minimum window size.
- **Quickplay Hub:** Split layout featuring an interactive Smart DJ seed generator on the left, and an interactive sliding ribbon of `Pins`, `History`, and `New` on the right.
- **Dynamic Now Playing Canvas:**
  - *Dynamic Artist Canvas:* High-resolution artist photography with Ken-Burns drift, slow idle-screensaver Y-axis rotation, and bold typographic overlays that drift off-screen. Artwork comes from Fanart.tv, with a configurable **community-mirror fallback** (clean-room; no bundled assets).
  - *Album Art Mosaic Wall:* Continuous 2D/3D tapestry of album art tiles from your collection.
- **Tri-State Heart Rating:** Favorite (❤️ / Heart), Disliked/Skip (💔 / Broken Heart), and Neutral. Hearts are **prioritized** in Smart DJ shuffles; broken hearts are **always excluded**.
- **Signature Accent Colors:** Authentic Zune 4.8 magenta family — transport ON `#F10DA2`, accent hover `#FA6EC9`, accent pressed `#B9077B`. Plus Orange/Cyan/Lime/Purple user-selectable accents.

---

## 🚀 Architecture

Built with Clean Architecture in .NET 8 / C# 12:

```
src/
├── Dorado.Domain/                       # Entities (Track, Album, Artist, Playlist, Device, SyncModels)
├── Dorado.Application/                  # Player coordinator, Smart DJ engine, sync orchestrator, settings
├── Dorado.Infrastructure.Persistence/   # SQLite database & EF Core (WAL journaling)
├── Dorado.Infrastructure.Audio/         # BASS engine: gapless chaining, equal-power crossfade, ReplayGain, FFT visualizer
├── Dorado.Infrastructure.Video/         # libVLCSharp (playback + now-playing clips)
├── Dorado.Infrastructure.Devices/       # IDeviceTransport abstraction + SimulatedDeviceTransport (real MTPZ hardware-N/A)
├── Dorado.Infrastructure.External/      # MusicBrainz, Fanart.tv, Last.fm, LRCLIB metadata aggregators
├── Dorado.Plugins.Protocol/             # Shared JSON-RPC message contracts + RPC channel
├── Dorado.Plugins.Sdk/                  # Plugin SDK + runtime (stdio transport, host context)
├── Dorado.Plugins.Host/                 # Out-of-process plugin host, .znp loader, event bridge
├── Dorado.Plugins.LastFm/               # Reference plugin: Last.fm scrobbler
├── Dorado.Plugins.Discord/              # Reference plugin: Discord Rich Presence
├── Dorado.UI/                           # Shared Avalonia XAML views, ViewModels, styles, animations
└── Dorado.Desktop/                      # Desktop executable for Linux and Windows
```

---

## ✅ What's Achieved

The complete Zune 4.8 desktop software, restructured around the original experience with extensive decompiled-corpus verification.

### Shell, Navigation & Chrome
- **Custom borderless chrome** (`SystemDecorations="None"`, draggable title bar, custom minimize/maximize/close)
- **Panoramic pivot strip** with wheel-pan + pointer drag-to-pan with friction inertia + pannable right-edge bleed (left pivots slide in from `QUIC…`, right pivots bleed `…ING`)
- **Tap-the-cut-off-header-to-go-back** (Tier A1 — the Zune 4.8 fan-loved navigation signature)
- **Parallax 3D pivot slide** (`PivotParallaxTransition`, 420ms cubic ease-out, scale 0.92; Quickplay variant 320ms/0.85)
- **Compact mini-player** with drag-to-move, showlist toggle, volume slider (480×110)
- **8-zone edge resize handles** for window drag-resize
- **In-shell modal dialog service** (`IDialogService`) for confirmations/alerts, migrating destructive actions (playlist delete, clear library)
- **A–Z type-ahead jump** across the Collection, Podcasts, Videos, and Playlists lists
- **Long-press to pin** an album to Quickplay (press-and-hold, with tap/long-press disambiguation)

### Audio Playback (REAL)
- **BASS engine** with gapless transitions, equal-power crossfade, ReplayGain volume leveling
- **Seek**, play/pause/stop/next/previous, shuffle, repeat, volume/mute, rated-track skip
- **FFT spectrum visualizer** (75ms refresh)
- **10-band equalizer** (managed RBJ peaking biquads via a BASS DSP pass; 8 presets, applied live from Settings)
- **Podcast streams** with episode playback
- **Smart DJ** that **prioritizes hearts, skips broken hearts** (Tier B1 — fan-favorite Zune differentiator)
- **Tri-state heart rating** (Favorite / Dislike / Neutral) integrated across playback + Smart DJ

### Collection & Library
- **Music library** with Artist / Album / Song / Genre / Playlist / Podcast / Video / Pictures sub-pivots
- **Smart / auto playlists** with rule-based editor (`SmartPlaylistEditorView`)
- **Metadata editor** (`MetadataEditView` + TagLibSharp writeback)
- **Find Album Info** with per-track matching review (`TrackMatchReviewView`)
- **Search autocomplete** across collections, podcasts, videos (async with cancellation)
- **FTS5 full-text search** (external-content index over title/artist/album/genre, trigger-synced, bm25-ranked, LIKE fallback)
- **Folder watching** with debounced `FileSystemWatcher`
- **Back-stack navigation** (Escape / back arrow)

### Device Sync (N-A on real hardware)
- **Sync-group engine** with rule builder, dry-run plan, guest sessions, capacity-aware transport
- **Reverse sync** (device → PC) manifest
- **Simulated device transport** (`SimulatedDeviceTransport`) with gas gauge, sync instructions toast, slide-in animation
- **Real MTP transport seam** (`MtpTransport` + `IMtpDeviceClient`): a libusb-backed client (`LibUsbMtpDeviceClient`, USB product-ID detection) and an in-memory `VirtualMtpDeviceClient` share one contract; the sync engine is transport-agnostic (real MTPZ session layer remains hardware-N-A)
- **FirstConnect wizard** (per-serial device arrival: name → media-type sync → privacy → done; Tier 4)
- **Per-device sync rules** (music/podcasts/video/pictures)
- **Wireless sync** stub (real wireless is hardware-N-A)
- **MTPZ firmware update/restore/rollback** — documented as N-A (no hardware)

### Now Playing
- **Three modes:** Artist Canvas (Ken-Burns drift), Album Art Mosaic Wall, Video clips (libVLC)
- **Bio + lyrics + showlist drawers** with slide-in animations (Tier A4)
- **Idle screensaver:** controls fade, text drifts left, artist watermark rotates Y-axis slowly (Tier A3)
- **Transport overlay** with hairline seek line, tri-state heart, showlist toggle
- **Auto-hiding HUD** (3.5s idle fade)
- **Transport button hover/pressed states** with per-frame ENTER/HOVER/PRESSED icon variants

### Playlists
- **Standard playlists** with create/delete/rename, .ZPL export, replay
- **Smart playlists** with rule-based auto-playlists
- **Drag-and-drop with hover-swap-icon** (Tier B2 — Zune 4.8 fan-quoted "Best playlist functionality of a desktop based software I've ever used"). 300ms hover dwell reveals alternative playlist drop targets.

### Podcasts
- **Series + episodes** with RSS subscribe
- **Feed normalization** (`PodcastFeedParser` / `PodcastFeedClient`): namespace-agnostic RSS/Atom parsing, iTunes durations (`HH:MM:SS` / seconds), `<media:content>` + Atom enclosure fallbacks, relative-URL resolution, HTML description stripping, dedupe, and HTML-page → `rel="alternate"` feed discovery for Patreon/Anchor-style landing URLs
- **Mark all played/unplayed** per series
- **Episode playback** via streams

### Localization
- **Runtime localization** (`ILocalizationService` + `LocalizationCatalog`): English + French catalog with per-locale fallback, live locale switch from Settings → General, persisted language preference

### Settings
- **13 software pages** (Collection, Playback, Podcasts, File Types, Privacy, Photos, Rip, Burn, Metadata, Display, General, About, Plugins) + 4 device pages (Sync Options, Space Reservation, Wireless Sync, Device Info)
- **Dark + light theme** with runtime swap (authentic `#11090F` dark / `#F3EFF1` light per `Shell.WindowColorFromRGB`); all tokens swap correctly including accent/text/border/surface
- **Real About sub-pivot:** product name, tagline, version, runtime identifier, build date, copyright, MIT license, EULA link
- **First-launch wizard** (welcome → monitored folders → library scan → done) + **What's New** dialog on version change

### Listening Intelligence (rune-inspired)
- **Audio-feature analysis**: deterministic per-track feature vector (BPM/energy/valence/acousticness/danceability/spectral-centroid) computed by a **real DSP extractor** (short-time FFT spectral centroid, RMS energy, zero-crossing rate, onset-envelope autocorrelation tempo) over decoded PCM, with a metadata prior as the offline fallback; persisted in SQLite and cached in memory behind a replaceable `IAudioAnalysisService` seam
- **Cosine-similarity recommendations** (per-track, per-album, and favorites-centroid) feeding Mixview and Smart DJ
- **Dynamic Mixes** (`DynamicMixService`): auto-updating rules — Most Played, Favorites Mix, Similar to Track/Album, Playlists Including Artist — surfaced as one-click mixes on Quickplay

### Social & Reputation
- **Tiered reputation badges** (Album/Artist Power Listener, Milestone, Marathon, Reviewer, Curator) with Bronze/Silver/Gold tiers, progress toward the next tier, and Zune's non-expiring unlock rule (derived from monotonic history)
- **Local reviews** (a substitute for the dead Zune social layer): album reviews captured via the in-shell prompt dialog and persisted
- **Curator / Forums substitute** reputation from playlists created and albums pinned

### Plugins
- **Out-of-process plugin host** (`Dorado.Plugins.Host`): `.znp` (zip) installer, `plugin.json` manifest validation, stdio JSON-RPC with health/restart supervision, and a host-service bridge (`logger/log`, `storage/get|set`, `library/queryTracks`, `ui/showToast`)
- **Player-event bridge** mapping `IPlayerCoordinator` events to `playback/trackChanged`, `playback/stateChanged`, `rating/changed`
- **Settings → Software → Plugins** page with install / enable / disable / open-folder
- **Reference plugins:** Last.fm Scrobbler (now-playing + scrobble threshold rules, api_sig signing) and Discord Rich Presence (IPC handshake + activity payload)
- **Plugin tooling:** `dotnet publish` emits a distributable `.znp` (`PackZnp` target), an author template at `templates/plugin-sample/`, expanded host services (`player/getState|play|pause|next|previous|seek`), and a real process-boundary E2E test spawning the reference plugins over stdio

### Visual & Motion
- **Authentic Zune 4.8 color palette** extracted from shipped PNG pixels + decompiled UIX corpus
- **Segoe Z Light / ZUC Light / ZLC Light** font family bundle (real `SEGOEZ-LIGHT.TTC`) with Selawik/Inter fallbacks
- **Ken-Burns** pan/zoom on backdrop photo (20s drift)
- **Parallax 3D pivot slide** with Quickplay-specific variant
- **Drawer slide-in / fade animations** for Bio, Showlist, SyncToast
- **Idle screensaver** with 3D Y-axis album rotation + text drift
- **Procedural clean-room visuals**: golden-angle hub map constellation on Quickplay, dashed iris-art reveal behind Now Playing (offline `IrisArtControl`), and vector chevron scroll affordances on the pivot strip
- **Now Playing ENTER button** with hover/pressed per-frame icon variants

### Design System & Verification
- **Zune design system skill** (`zune-design-system`) with full token spec
- **Hardware-sync skill** (`zune-hardware-sync`) — MTP/MTPZ protocol reference
- **Plugin protocol skill** (`zune-plugins-protocol`) — JSON-RPC contracts
- **Design-invariants audit** (`scripts/mcp_tools.py`) — automated `CornerRadius=0`, no drop shadows check on every CI run
- **393 unit tests** passing (XUnit + Avalonia headless harness)

---

## 🚧 What's Left To Do

Items that remain, in approximate priority order. **No gap is unplanned** — each is in the [`docs/parity/deferred_registry.md`](docs/parity/deferred_registry.md) with rationale.

### Medium-priority features (Tiers C / D)

### Smaller polish
- ✅ **MusicBrainz + AcoustID auto-metadata + dedup** at scan time (AcoustID via `fpcalc` + API key; recording-level acoustic dedup)
- **Direct device playback** from desktop (play tracks off the device)
- **On-the-fly WMA Lossless transcoding** during sync

### Hard / large (Tier C2 / D1)
- **Tier C2 — Mixview visual mosaic** (the unique discovery UI fans repeatedly cite)
- **Tier D1 — Real CD rip/burn pipeline** (capability-gated — needs optical-drive access)
- **Zune Card + Friends social layer** (the most-requested missing feature, but the Zune Social servers are dead; local-only substitute)
- **Wireless song squirt** (device-to-device peer-to-peer)
- **Chevron scroll-arrow overlay** on the pivot strip (drag-inertia pan already shipped)

### Hardware-N-A (documented in `deferred_registry.md`)
- **Real MTPZ device sync** (`ZuneWmduDLL` parity) — needs physical Zune hardware
- **Windows shell integration** (explorer context menus, jump lists, taskbar previews)
- **i18n — 26 Zune locales**
- **UPnP media sharing** (`ZuneNSS` / `ZuneShareEXE`)

### Known internal bugs being triaged
- The Settings pivot-gating bug (5 missing `OnPropertyChanged` notifications on the Phase 3 sub-pivots) was fixed in `c3c530e` — re-verify on every release

---

## 🛠️ Building & Running

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Running Desktop
```bash
dotnet run --project src/Dorado.Desktop/Dorado.Desktop.csproj
```

### Running Tests
```bash
dotnet test Dorado.sln
```

### Design Audit
```bash
python3 -c "import sys; sys.path.insert(0,'scripts'); from mcp_tools import audit_design_invariants; print(audit_design_invariants('.'))"
```

### Multi-Platform Publishing
```bash
# Linux x64
dotnet publish src/Dorado.Desktop -r linux-x64 -c Release

# Linux arm64
dotnet publish src/Dorado.Desktop -r linux-arm64 -c Release

# Windows x64
dotnet publish src/Dorado.Desktop -r win-x64 -c Release

# Windows arm64
dotnet publish src/Dorado.Desktop -r win-arm64 -c Release
```

### Platform Notes
- **Linux:** video playback loads system VLC at runtime — install `vlc` (or `libvlc5`/`libvlccore9`) for the video surface. BASS audio natives are vendored per-RID and ship with every build.
- **Windows x64:** full audio + video out of the box (VLC natives bundled via `VideoLAN.LibVLC.Windows`).
- **Windows arm64:** BASS publishes no ARM64 natives, so audio playback runs in simulated (silent) mode; video is unaffected.

Continuous integration (`.github/workflows/ci.yml`) builds the solution Release with a zero-warnings policy, runs the full test suite, and re-runs the design-invariants audit on every push. Tagging `dorado-v*` (or `.github/workflows/release.yml` → Run workflow) publishes self-contained archives for linux-x64, linux-arm64, win-x64, and win-arm64.

---

## 📊 Parity Status

Verified against the full Zune 4.8 decompiled corpus (821 C# files in `zuneshell/`, 1,030 in `zunedbapi/`, 241 `.UIX` resources, 1,857 binary assets). For IP reasons the corpus is **not** committed here; it is kept outside the repository at `../zune-disassembly/` for reference only.

| Domain | Parity | Status |
|---|---|---|
| A. Shell & Navigation | **~92%** | Authentic chrome, cropped-header back, panoramic pivot, parallax + drag-inertia pan |
| B. Quickplay | **~85%** | Smart DJ hearts-aware, deck panorama, hubs, dynamic Mixes; artwork maps pending |
| C. Collection (music) | **~92%** | Artists/Albums/Songs/Genres/Playlists/Smart Playlists + Find Album Info + FTS5 search |
| D. Now Playing | **~90%** | 3 modes + Ken-Burns + idle screensaver + bio/lyrics/showlist drawers |
| E. Mixview | **~72%** | Local mosaic + audio-feature similarity engine + external MusicBrainz related-artist satellites |
| F. Audio engine | **~88%** | Real BASS engine, gapless, crossfade, ReplayGain, FFT, 10-band EQ, podcasts |
| G. CD Land | **~40%** | Full UI, simulated rip/burn (no optical drive) |
| H. Device sync | **~70%** | Sync-group engine, dry-run, guest/reverse sync, FirstConnect, MTP transport seam |
| I. Podcasts | **~85%** | Subscribe + normalization + mark-all-played + stream playback |
| J. Social / Marketplace | **N-A** | Servers dead; local Zune Card substitute; reputation-badge taxonomy pending |
| K. Settings & Management | **~88%** | 13 software pages + 4 device pages + plugins + language + dark/light theme |
| L. First-launch & onboarding | **~90%** | First-launch wizard + What's New + FirstConnect wizard |
| M. Platform services (ZMDB, sharing) | **~60%** | SQLite + FTS5 substitute, plugin host, analysis persistence; UPnP/share/MUI deferred |

**Weighted overall parity: ~88%** (UI presentation strongly, hardware-dependent features neutrally; measured 2026-09-10 after Phases 12–23).

---

## 📜 Recent Notable Commits

```
52c630e feat(ui/playlists): drag-and-drop + hover-swap-icon (Tier B2) — Zune 4.8 fan-loved playlist flow
351d756 feat(smartdj): heart-aware Smart DJ shuffle (Tier B1) — hearts prioritized, broken hearts always excluded
d612da8 feat(ui/motion): Tier A motion batch — Zune 4.8 cropped-header back, parallax pivot slide, idle screensaver, drawer slide-in animations
3a4318a docs(parity): add comprehensive gap inventory vs disassembly (input to next-batch roadmap)
481c363 fix(ui/shell): Settings pivot-gating bug + Now Playing button hover/pressed states + CDView disc art + mini-player showlist/volume + cross-collection search + real About panel
9476c0b feat(ui/theme): Zune 4.8 light theme parity — runtime dark/light swap (default light is #F3EFF1 per Shell.cs:668)
1993588 feat(ui/device): FirstConnect wizard parity — per-serial device-arrival onboarding (FIRSTCONNECT.UIX)
429e322 feat(ui/settings): Zune 4.8 settings parity — file types, privacy, photos, general pages (and FirstConnect serials persisted)
1e935ee feat(ui/podcasts): mark-all-played/unplayed commands + podcasts settings page (keep episodes, auto-download)
c3c530e feat(ui/playback): Zune 4.8 keyboard-shortcut parity — Ctrl+S stop, Ctrl+Left/Right seek, F1 about, / search-focus; gas-gauge category colors bound to dynamic tokens
6da7bb8 feat(ui/theme): authentic Zune 4.8 visual parity verified against disassembly corpus
```

---

## 🧰 Self-Contained Repository Skills & MCP Tools

This repository contains built-in agent customizations and tools:
- **`GEMINI.md`**: Master repository rules enforcing Zune Metro design invariants and multi-platform boundaries.
- **`.agents/skills/zune-design-system`**: Comprehensive design tokens, layout specifications, and XAML templates.
- **`.agents/skills/zune-hardware-sync`**: Guide to USB MTP/MTPZ, ZMDB binary parsing, and USB-PPP reverse interception.
- **`.agents/skills/zune-plugins-protocol`**: Out-of-process plugin wire contracts and packaging specifications.
- **`.agents/mcp_config.json`**: Local MCP development tools (`probe_zune_devices`, `audit_zune_design_invariants`).
- **`docs/parity/osint_registry.md`**: Community/OSINT source registry (rune, Xune, ZuneDiscordRPC, ZuseMe, zune-podcasts, android-file-transfer-linux) with license and clean-room notes.
- **`docs/parity/true_parity_task_plan.md`**: Phased parity program (Phases 12–23: plugin host, listening intelligence, podcast normalization, MTP transport, i18n, interaction fidelity, badges, clean-room visuals, plugin tooling).

---

## 💖 Special Thanks & Acknowledgements

A heartfelt **thank you to [cmoserror1](https://github.com/cmoserror1)** for inspiring the creation of this project. Your passion and vision for the enduring beauty of the Zune experience made Dorado possible!

Additional gratitude to the vibrant Zune preservation, modding, and development community across [zunes.me](https://zunes.me), [ZuneDev](https://github.com/ZuneDev), and everyone keeping the spirit of authentic digital design alive.

---

## 📄 License

Licensed under the MIT License.
