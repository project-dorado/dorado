# Dorado: Repository Guidelines & Directives

Welcome to **Dorado**, the modern cross-platform spiritual successor to Microsoft Zune Desktop and player.
This repository is self-contained and adheres to strict design, architecture, and engineering principles.

---

## 1. Design Invariants: The Zune "Metro" / Iris UI/UX System

All user interfaces in this project MUST strictly follow the authentic Zune design language:

1. **Content Before Chrome:**
   - Eliminate unnecessary borders, frames, and drop shadows.
   - **Zero Corner Radius:** All surfaces, cards, buttons, and tiles must have `CornerRadius = 0`. No rounded corners.
   - Avoid skeuomorphic bevels, glossy highlights, or synthetic depth.
2. **Typography as Art:**
   - Typography is the primary visual element. Use **Segoe UI**, **Segoe WP**, **Zegoe UI**, or bundled **Selawik** metrics.
   - Strict hierarchical scale:
     - Hero / Display: 48pt – 72pt Light (`FontWeight.Light`), tight tracking.
     - Primary Pivot: 34pt Light, uppercase (`QUICKPLAY`, `COLLECTION`, `DEVICE`, `SETTINGS`).
     - Sub-Pivot: 20pt Light, lowercase (`music`, `videos`, `podcasts`, `artists`, `albums`).
     - Content Header: 16pt SemiBold.
     - Body: 11pt Regular.
     - Subtitle / Caption: 9pt Regular, 60% opacity.
   - Opacity establishes depth: Active text is 100% white (`#FFFFFF`), hover is 85%, inactive/unselected is 35–40%.
3. **Canvas & Surfaces:**
   - Background Canvas: Matte Pitch Black (`#111111`) or Dark Charcoal (`#161616`).
   - Card/Tile Surface: `#202020` default, `#282828` hover.
   - Light Theme alternate: `#ECECEC` background with `#1A1A1A` text.
4. **Signature Accent Colors:**
   - Zune Pink / Magenta (`#FA2A55` / `#E51400`) — signature default.
   - Zune Orange (`#F09609`).
   - Zune Cyan (`#1BA1E2`).
   - Zune Lime / Green (`#339933`).
   - Zune Purple (`#A200FF`).
5. **Iconic Navigation & Hubs:**
   - **Pivot Header:** Horizontal strip of section titles with smooth deceleration pan.
   - **Quickplay Hub:** Split layout with Smart DJ seed generator on the left, and an interactive horizontal deck of `Pins`, `History`, and `New` on the right.
   - **Now Playing:** Dual modes:
     - Dynamic Artist Canvas (Ken-Burns slow pan/zoom on high-res artist photography).
     - Album Art Mosaic Wall (3D/2D grid of collection album covers with active track centered).
   - **Heart Rating:** Tri-state rating: Heart (favorite), Broken Heart (dislike/skip), Neutral (unrated).
   - **Player HUD:** Minimalist bottom bar with hairline seek line, transport controls, and volume.

---

## 2. Platform Targets & Compatibility

- **Desktop (Tier 1):** Windows (x64, arm64) and Linux (x64, arm64).
- **Mobile (Long-Term Milestone):** Android.
- All core business logic, domain models, persistence, and application use cases must reside in platform-agnostic .NET 8 class libraries so they can be consumed across Desktop and Android targets.
- GUI is built on **Avalonia UI** with hardware-accelerated SkiaSharp rendering.

---

## 3. Architecture & Code Organization

The codebase follows Clean Architecture with strict separation of concerns:

- `src/Dorado.Domain`: Entities, value objects, domain events, business invariants. (Zero external GUI/audio dependencies).
- `src/Dorado.Application`: Application use cases, playback coordinators, library services, sync orchestrators, plugin interfaces.
- `src/Dorado.Infrastructure.Persistence`: SQLite database (EF Core, WAL journaling). Full-text search is currently in-memory prefix matching; FTS5 indexing is a planned Phase 16 item, not yet implemented.
- `src/Dorado.Infrastructure.Audio`: Audio playback pipeline, gapless voice transitions, ReplayGain normalization, FFT spectrum analyzer, system media controls (Linux MPRIS, Windows SMTC).
- `src/Dorado.Infrastructure.Devices`: Physical Zune USB synchronization:
  - Transport backends: `libusb` on Linux, `WinUSB` on Windows.
  - MTP / MTPZ security handshake.
  - ZMDB fast binary parser (F-marker record extractor).
  - USB PPP / TCP / DNS / HTTP reverse interceptor (`192.168.55.100`) for streaming artist biography XML and JPEG artwork directly to connected Zunes.
  - SSDP / PTP/IP wireless synchronization listener.
- `src/Dorado.Infrastructure.External`: Metadata aggregators (MusicBrainz, Fanart.tv, Last.fm, ZuneNetApi).
- `src/Dorado.Plugins.Protocol` & `src/Dorado.Plugins.Sdk`: Out-of-process JSON-RPC sandboxed plugin architecture.
- `src/Dorado.UI`: Shared Avalonia XAML views, view models, controls, animations, and theme resources.
- `src/Dorado.Desktop`: Desktop host executable for Linux and Windows.

---

## 4. Skills & Agent Tools

Specialized domain knowledge is codified in `.agents/skills/`:
- [`zune-design-system`](.agents/skills/zune-design-system/SKILL.md): Comprehensive UI design tokens, XAML templates, and layout specifications.
- [`zune-hardware-sync`](.agents/skills/zune-hardware-sync/SKILL.md): Complete protocol guide for Zune USB MTP/MTPZ, ZMDB parsing, and USB-PPP network stack.
- [`zune-plugins-protocol`](.agents/skills/zune-plugins-protocol/SKILL.md): Out-of-process plugin specification and JSON-RPC wire contracts.

Local developer tools are declared in `.agents/mcp_config.json`.
