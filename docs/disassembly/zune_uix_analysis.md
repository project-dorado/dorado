# Microsoft Zune Desktop 4.8 Disassembly & Iris UI Analysis

## 1. Overview & Architecture

We disassembled the official Microsoft Zune Desktop 4.8 package (`ZunePackage.exe`), extracted the core installer payload `Zune-x86.msi`, and unpacked its primary presentation libraries:
- `Zune.exe` (Main entry point host)
- `ZuneShell.dll` (.NET Mono-compatible managed assembly hosting `ZuneUI` and `Microsoft.Zune.Shell`)
- `ZuneShellResources.dll` (16.9 MB Win32 PE Resource library holding compiled Iris UIB documents and 1,857 graphic assets)
- `Microsoft.Iris.dll` (The proprietary Microsoft Iris UI Declarative Direct3D rendering engine)

---

## 2. The Iris UI Declarative System

Zune's UI was engineered using Microsoft's **Iris UI Framework** (originally created for Windows Media Center and then dramatically evolved by Bill Flora and the Zune team for the "Metro" design language):
- UI definitions were authored in `.uix` XML markup files and pre-compiled into `.uib` (UI Binary) bytecode stored in the `RCDATA` table of `ZuneShellResources.dll`.
- Disassembled key UI documents:
  - `Shell.uix`, `NonClientControls.uix`, `TopToolbar.uix`: Window frame, minimal title controls, panoramic pivot list (`QUICKPLAY`, `COLLECTION`, `DEVICE`, `SETTINGS`), and docked bottom transport HUD.
  - `MusicLibrary.uix`, `TracksPanel.uix`, `TracksPanelColumns.uix`: Two-column artist discography browser, album artwork grid, and dense spreadsheet track data tables.
  - `Quickplay.uix`, `QuickplayStrip.uix`, `QuickplayModule.uix`: Split-screen Smart DJ mix generator on the left and horizontal sliding decks (`Pins`, `History`, `New`) on the right.
  - `NowPlayingLand.uix`, `NowPlayingMusicBackground.uix`, `NowPlayingStyles.uix`: Full-bleed dynamic artist canvas with Ken-Burns pan/zoom drift, typographic overlays, and the alternate album mosaic wall.
  - `TransportControls.uix`: Docked bottom player strip with hairline scrub line, time toggle (elapsed vs remaining), volume slider, and tri-state Heart / Broken Heart ratings.

---

## 3. Discovered Visual Tokens & Exact Metrics

### Window Constraints
- Minimum Window Width: **734 px** (`Shell.c_minimumWindowWidth`)
- Minimum Window Height: **500 px** (`Shell.c_minimumWindowHeight`)
- Default Window Canvas: Matte Pitch Black `#11090F` (RGB: 17, 9, 15) with subtle warmth for Quickplay / Collection, or `#111111` matte charcoal.
- Light Theme Background: `#F3EFF1` (RGB: 243, 239, 241).

### Typography Metrics (`SEGOEZ-LIGHT.TTC`)
- Authentic Font: **Segoe Zune Light** (`SEGOEZ-LIGHT.TTC`, TrueType Collection containing 4 font variations).
- Hero Titles: 42pt – 54pt Light, tight tracking.
- Primary Panoramic Pivots: 32pt Light, uppercase (`QUICKPLAY`, `COLLECTION`, `DEVICE`).
- Sub-Pivots: 18pt – 20pt Light, lowercase (`artists`, `albums`, `songs`, `genres`).
- Content Headers: 14pt – 16pt SemiBold.
- Body Text: 11pt Regular.
- Captions / Metadata / Scrub Timestamps: 8pt – 9pt Regular.
- Opacity Depth Invariant:
  - Active: 100% white (`#FFFFFF`)
  - Hover: 85% white (`#D0D0D0`)
  - Inactive: 40% white (`#666666`)
  - Ambient Watermark: 8% white (`#181818`)

### Extracted Authentic Graphics (`src/Dorado.UI/Assets/Zune/`)
- **Transport Bar**:
  - `TRANSPORT.PLAY.PNG` (35x35 32-bit RGBA)
  - `TRANSPORT.PAUSE.PNG` (35x35 32-bit RGBA)
  - `TRANSPORT.BACK.PNG` (Previous track)
  - `TRANSPORT.FORWARD.PNG` (Next track)
  - `TRANSPORT.SHUFFLE.ON.PNG`, `TRANSPORT.SHUFFLE.OFF.PNG`
  - `TRANSPORT.REPEAT.ON.PNG`, `TRANSPORT.REPEAT.OFF.PNG`
  - `TRANSPORT.MUTE.ON.PNG`, `TRANSPORT.MUTE.OFF.PNG`
- **Ratings**:
  - `RATING.LIKEIT.PNG` (17x15 Heart glyph)
  - `RATING.HATEIT.PNG` (17x15 Broken Heart glyph)
  - `RATING.NOTRATED.PNG`
- **Branding & Logos**:
  - `ZUNELOGO.PNG` (36x38)
  - `ZUNECOLORLOGO.PNG`
  - `ZUNELOGOTEXT.PNG`
  - `QUICKMIXICON.PNG`
  - `ZUNEHDDEVICES.PNG`
- **Window Controls**:
  - `WINDOW.CLOSE.PNG` (14x15)
  - `WINDOW.MINIMIZE.PNG`
  - `WINDOW.MAXIMIZE.PNG`
  - `WINDOW.RESTORE.PNG`
