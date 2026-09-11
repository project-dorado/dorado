# Dorado

<div align="center">

# 🎵 Dorado
**The authentic cross-platform spiritual successor to Microsoft Zune Desktop & Player**

[![Build & Release](https://github.com/project-dorado/dorado/actions/workflows/build.yml/badge.svg)](https://github.com/project-dorado/dorado/actions/workflows/build.yml)
[![Avalonia UI](https://img.shields.io/badge/Avalonia_UI-11.2-8C15E9?logo=avalonia&logoColor=white)](https://avaloniaui.net/)
[![Platforms](https://img.shields.io/badge/Platforms-Windows%20%7C%20Linux%20(x64%20%26%20arm64)-0078D7)]()
[![Design](https://img.shields.io/badge/Aesthetic-Zune%20Metro%20%2F%20Iris-FA2A55)]()
![Tests](https://img.shields.io/badge/tests-393%20passing-4c1?logo=xunit&logoColor=white)
![Parity](https://img.shields.io/badge/Zune%204.8%20parity-~88--brightgreen)

</div>

---

## ✨ Design Principles: The Zune "Metro" Experience

Dorado is built on the purest tenets of the original Microsoft Zune Desktop software:

- **Content Before Chrome:** Zero rounded corners (`CornerRadius = 0`), no drop shadows, no skeuomorphic gradients or faux-leather textures.
- **Typography as Art:** Sized and kerned with Segoe UI / Selawik metrics across Display, Pivot, Sub-pivot, and Caption hierarchies. Opacity communicates state (Active: 100%, Hover: 85%, Inactive: 40%).
- **Iconic Pivot Navigation:** Fluid deceleration panning across `QUICKPLAY`, `COLLECTION`, `DEVICE`, `DISC` (ephemeral — only while a disc session is loaded) and `SOCIAL`, plus `SETTINGS` docked at the right — with pannable right-edge bleed at the authentic 734×500 minimum window size.
- **Quickplay Hub:** Split layout featuring an interactive Smart DJ seed generator on the left, and an interactive sliding ribbon of `Pins`, `History`, and `New` on the right.
- **Dynamic Now Playing Canvas:**
  - *Dynamic Artist Canvas:* High-resolution artist photography with Ken-Burns drift, slow idle-screensaver Y-axis rotation, and bold typographic overlays that drift off-screen. Artwork comes from Fanart.tv, with a configurable **community-mirror fallback** (clean-room; no bundled assets).
  - *Album Art Mosaic Wall:* Continuous 2D/3D tapestry of collection tiles. Tiles currently render a typographic music-note fallback; binding per-album artwork into the grid is a tracked gap (see audit M-1).
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
├── Dorado.Infrastructure.Devices/       # device transports: SimulatedDeviceTransport, MTP seam (libusb + virtual), LAN sync server + mDNS advertiser (MTPZ hardware-N-A)
├── Dorado.Infrastructure.External/      # MusicBrainz, Cover Art Archive, Fanart.tv, LRCLIB, AcoustID, Wikipedia aggregators
├── Dorado.Infrastructure.Emulator/      # bridge to the Dorado-EMU CLI (JSON-RPC over stdio)
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
- **Compact mini-player** with drag-to-move, showlist toggle, volume slider (340×96)
- **8-zone edge resize handles** for window drag-resize
- **In-shell modal dialog service** (`IDialogService`) for confirmations/alerts, migrating destructive actions (playlist delete, clear library)
- **A–Z type-ahead jump** across the Collection, Podcasts, Videos, and Playlists lists
- **Long-press to pin** an album to Quickplay (press-and-hold, with tap/long-press disambiguation)

### Audio Playback (REAL)
- **BASS engine** with gapless transitions, equal-power crossfade, ReplayGain volume leveling
- **Seek**, play/pause/stop/next/previous, shuffle, repeat, volume/mute, rated-track skip
- **FFT spectrum visualizer** (33ms refresh; falls back to a procedural spectrum when no audio source is active)
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
- **LAN sync endpoint** (`SyncEndpointHost` + `SyncTcpServer`): the desktop is the server for the phone sync protocol, advertised over mDNS (`_dorado-sync._tcp`) for Dorado-HD pairing; Zune-native wireless sync remains hardware-N-A
- **MTPZ firmware update/restore/rollback** — documented as N-A (no hardware)

### Now Playing
- **Three modes:** Artist Canvas (Ken-Burns drift), Mosaic Wall (collection grid; placeholder glyph tiles pending artwork binding), Video clips (libVLC)
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
- **Runtime localization** (`ILocalizationService` + `LocalizationCatalog`): **20 locales** (en, fr, de, es, it, pt, nl, sv, da, nb, fi, pl, cs, hu, tr, ru, ja, ko, zh-Hans, zh-Hant) with per-locale English fallback, live locale switch from Settings → General, persisted language preference

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
- **Segoe-metric typography**: the Zune `Segoe Z` / `Zegoe` families resolve through the bundled OFL **Selawik** metrics (no Microsoft font is bundled)
- **Ken-Burns** pan/zoom on backdrop photo (8s drift)
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
- **414 tests** passing (14 Domain + 399 Application + 1 golden-image visual gate; XUnit + Avalonia headless/Skia)

---

## 🚧 What's Left To Do

Items that remain, in approximate priority order. **No gap is unplanned** — each is in the [`docs/parity/deferred_registry.md`](docs/parity/deferred_registry.md) with rationale.

### Medium-priority features (Tiers C / D)

### Smaller polish
- ✅ **MusicBrainz + AcoustID auto-metadata + dedup** at scan time (AcoustID via `fpcalc` + API key; recording-level acoustic dedup)
- ✅ **On-the-fly transcoding** during device sync (`FfmpegTranscodeService`, when FFmpeg is on PATH)
- **Direct device playback** from desktop (play tracks off the device)

### Hard / large (Tier C2 / D1)
- **Mixview authentic Iris mosaic** — the local mosaic, audio-similarity ranking and external MusicBrainz artist satellites ship; the authentic Iris mosaic art remains
- **Tier D1 — Real CD rip/burn pipeline** (capability-gated — needs optical-drive access)
- **Zune Card + Friends social layer** (the most-requested missing feature, but the Zune Social servers are dead; local-only substitute)
- **Wireless song squirt** (device-to-device peer-to-peer)

### Hardware-N-A (documented in `deferred_registry.md`)
- **Real MTPZ device sync** (`ZuneWmduDLL` parity) — needs physical Zune hardware
- **Windows shell integration** (explorer context menus, jump lists, taskbar previews)
- **i18n — remaining locales + full string extraction** (20 locales of the shell/pivot strings shipped; extracting every view string remains incremental)
- **UPnP media sharing** (`ZuneNSS` / `ZuneShareEXE`)

### Open findings (audited 2026-09-11; remediated M1–M3)
- See [`docs/parity/audit-2026-09-11.md`](docs/parity/audit-2026-09-11.md) for the full severity-ranked list. **Fixed since:** mosaic artwork binding, Artist-Canvas↔Mosaic-Wall crossfade, now-playing hover/pressed icon variants, Smart DJ timeout + Quick Mix progress, playlist search, and editable Zune Card profile.
- **Still open:** Now-Playing video mode remains an instant swap (no crossfade), authentic Iris mosaic art, and the deferred hardware paths (real CD rip/burn, MTPZ, UPnP, extra locales).
- The Settings pivot-gating bug was re-verified **fixed** in the 2026-09-11 audit (all sub-pivot notifications fire) — no longer a triage item.

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

Continuous integration (`.github/workflows/ci.yml`) builds the solution Release with a zero-warnings policy, runs the full test suite, and re-runs the design-invariants audit on every push. `.github/workflows/build.yml` publishes self-contained archives for linux-x64, linux-arm64, win-x64, and win-arm64: every push to `main` gets a `dorado-<sha>` prerelease tagged with the short commit hash, and pushing a `dorado-v*` tag produces a normal versioned release.

---

## 📊 Parity Status

Verified against the full Zune 4.8 decompiled corpus (821 C# files in `zuneshell/`, 1,030 in `zunedbapi/`, 241 `.UIX` resources, 1,857 binary assets). For IP reasons the corpus is **not** committed here; it is kept outside the repository at `../zune-disassembly/` for reference only.

> **Audit 2026-09-11:** the figures below were independently re-measured in
> [`docs/parity/audit-2026-09-11.md`](docs/parity/audit-2026-09-11.md). The
> claimed ~88% was overstated; the post-audit verified band was **~80–84%**.
> The remediation (honest CD/device surfaces, mosaic art, crossfade, icon
> variants, Smart DJ progress/timeout, playlist search, editable Zune Card)
> lifts the current verified band to **~85–87%**. Countable claims (13+4
> settings pages, FTS5, EQ, plugins, badges) were confirmed.

| Domain | Parity | Status |
|---|---|---|
| A. Shell & Navigation | **~88%** | Authentic chrome, cropped-header back, panoramic pivot, parallax + drag-inertia pan; DISC is ephemeral and hidden without a disc session |
| B. Quickplay | **~87%** | Smart DJ hearts-aware, deck panorama, hubs, dynamic Mixes; procedural hub map; Smart DJ 5s timeout + Quick Mix progress shipped |
| C. Collection (music) | **~89%** | Two-tier collection (media groups → sub-pivots), smart playlists, Find Album Info, FTS5 search; playlist search shipped |
| D. Now Playing | **~83%** | 3 modes + Ken-Burns + idle screensaver + drawers; real mosaic album art with glyph fallback; canvas↔mosaic crossfade; hover/pressed icon variants; video mode still an instant swap |
| E. Mixview | **~70%** | Local mosaic + audio-feature similarity engine + external MusicBrainz related-artist satellites |
| F. Audio engine | **~88%** | Real BASS engine, gapless, crossfade, ReplayGain, FFT, 10-band EQ, podcasts |
| G. CD Land | **~30%** | Full UI with an honest no-disc state; rip/burn are simulated and say so (no optical drive) |
| H. Device sync | **~62%** | Sync-group engine, dry-run, guest/reverse sync, FirstConnect, MTP transport seam, LAN sync + mDNS; device info projects a real device or a disconnected state |
| I. Podcasts | **~85%** | Subscribe + normalization + mark-all-played + stream playback |
| J. Social / Marketplace | **N-A** | Servers dead; local Zune Card substitute; tiered reputation badges + local reviews shipped |
| K. Settings & Management | **~85%** | 13 software pages + 4 device pages + plugins + language + dark/light theme |
| L. First-launch & onboarding | **~90%** | First-launch wizard + What's New + FirstConnect wizard |
| M. Platform services (ZMDB, sharing) | **~60%** | SQLite + FTS5 substitute, plugin host, analysis persistence; UPnP/share/MUI deferred |

**Weighted overall parity: ~85–87%** (audited 2026-09-11 at ~80–84%, then raised by the M1–M3 remediation; supersedes the earlier ~88% self-measure).

---

## 📜 Recent Notable Commits

```
6b2d800 feat(scan): AcoustID scan-time metadata enrichment and acoustic dedup
e4d4d58 feat(analysis): real DSP audio features over decoded PCM (STFT/RMS/ZCR/tempo)
6aab7f0 feat(mixview): external MusicBrainz related-artist satellites
9b8873f feat(device): LAN sync settings and mDNS advertising for Dorado-HD pairing
1b70888 feat(cloud): settings-aware client, silent token refresh, startup update check
51062fa feat(ui): settings selection ring & window chrome (Phase I)
00fca95 feat(ui): A-Z jump-list & collection library parity (Phase F)
7ec70e1 feat(ui): now-playing peak-hold visualizer & drawer (Phase E)
ca01a3e feat(ui): design system, PageStack, two-tier collection & kinetic quickplay (Phases A–C)
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
