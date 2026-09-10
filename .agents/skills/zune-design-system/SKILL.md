---
name: zune-design-system
description: >-
  Use this skill when designing, implementing, styling, or auditing user interface components,
  themes, layouts, typography, and motion in Dorado to ensure complete fidelity to the Microsoft
  Zune "Metro" and Iris design language.
---

# Zune "Metro" / Iris Design System Guide

This guide defines the immutable rules, tokens, typography scales, layout geometries, and motion
principles that govern all visual and interactive presentation in Dorado.

---

## 1. Core Principles

### Authentically Digital & Content Before Chrome
- **No Skeuomorphism:** No fake leather, drop shadows, glossy plastic buttons, or glass bevels.
- **Zero Corner Radius:** All elements (buttons, cards, album art, text boxes, flyouts, sliders) have `CornerRadius="0"`.
- **Borderless Geometry:** Visual structure is achieved through spatial layout, alignment, and typography weight rather than border lines.
- **Horizontal Bleed (Panorama):** Horizontal layouts bleed off the right edge of the viewport, visually inviting the user to explore further.

---

## 2. Color Palette & Tokens

### Base Surfaces
| Token Name | Hex Code | Purpose |
| :--- | :--- | :--- |
| `SurfaceBackground` | `#111111` | Primary matte black canvas |
| `SurfaceElevated` | `#181818` | Panels, drawers, navigation strips |
| `SurfaceTile` | `#202020` | Album art placeholder, media cards |
| `SurfaceTileHover` | `#2A2A2A` | Hovered card state |
| `SurfaceBorderSubtle`| `#2C2C2C` | Hairline dividers (0.5px - 1px) |

### Signature Accent Colors
The user can select an accent color in Settings. All accent-colored UI controls must bind to the dynamic accent resource:

| Accent Name | Primary Hex | Hover / Bright | Purpose |
| :--- | :--- | :--- | :--- |
| **Zune Magenta / Pink (Default)** | `#E51400` / `#FA2A55` | `#FF4D79` | Signature default brand accent |
| **Zune Orange** | `#F09609` | `#FFA726` | Classic Zune 30 / Warm accent |
| **Zune Cyan / Electric Blue** | `#1BA1E2` | `#33B5E5` | Fresh modern accent |
| **Zune Lime / Green** | `#339933` | `#4CAF50` | Eco / Vivid accent |
| **Zune Purple** | `#A200FF` | `#B388FF` | Deep vibrant accent |

### Typography Hierarchy & Opacities
Text color is always pure white (`#FFFFFF`) on dark theme, modulated by opacity:
- **Active / Primary Text:** `Opacity="1.0"` (`#FFFFFF`)
- **Hover Text:** `Opacity="0.85"`
- **Secondary / Metadata Text:** `Opacity="0.60"` (Artist name under track, duration, year)
- **Inactive / Dimmed Pivot:** `Opacity="0.40"` (Unselected pivot titles)
- **Watermark / Background Big Type:** `Opacity="0.08"` (Oversized subtle background numerals/letters)

---

## 3. Typographic Scales

Font family must be `Segoe UI`, `Segoe WP`, `Zegoe UI`, or bundled open-source metric-compatible `Selawik` / `Inter`.

| Role | Font Size | Weight | Case | Usage |
| :--- | :--- | :--- | :--- | :--- |
| **Hero Display** | `56pt - 72pt` | Light (300) | Normal | Now Playing track title, Quickplay clock/stats |
| **Pivot Main** | `34pt` | Light (300) | UPPERCASE | `QUICKPLAY`, `COLLECTION`, `DEVICE`, `SETTINGS` |
| **Sub-Pivot** | `20pt` | Light (300) | lowercase | `music`, `videos`, `podcasts`, `artists`, `albums` |
| **Section Header**| `16pt` | SemiBold (600)| Normal | Playlist title, Album header |
| **Body Primary** | `12pt` | Normal (400) | Normal | Track title in list, table row |
| **Body Secondary**| `10pt` | Normal (400) | Normal | Artist, Album subtitle |
| **Caption / Time** | `9pt` | Normal (400) | Normal | Elapsed/Total time, track numbers |

---

## 4. Key Component Blueprints

### A. The Pivot Header
```xml
<!-- Example Avalonia XAML structure for Zune Pivot -->
<StackPanel Orientation="Horizontal" Spacing="28" Margin="40,24,0,16">
    <TextBlock Text="QUICKPLAY" Classes="pivot-header active" />
    <TextBlock Text="COLLECTION" Classes="pivot-header" />
    <TextBlock Text="DEVICE" Classes="pivot-header" />
    <TextBlock Text="SETTINGS" Classes="pivot-header" />
</StackPanel>
```
- Inactive pivots must have a smooth fade to 100% opacity on hover, and clicking switches the active view with a horizontal content slide.

### B. The Tri-State Heart Rating
Zune used an iconic binary/tri-state heart rating rather than 5-star ratings:
1. **Heart (Favorite):** Solid accent-colored heart icon. Song is prioritized in Smart DJ and Quickplay Favorites.
2. **Broken Heart (Dislike):** Cracked/broken heart icon. Song is skipped during shuffle and Smart DJ generation.
3. **Unrated (Neutral):** Transparent or subtle outline shown on hover.

### C. The Quickplay Hub
- Split screen:
  - **Left Wing:** Smart DJ quick-mix seed panel.
  - **Right Wing:** 3-slot horizontal sliding carousel containing:
    - `Pins`: User-pinned artists, albums, or playlists.
    - `History`: Grid of recently played album covers.
    - `New`: Grid of newly added album covers.
- Clicking on one of the decks slides it into center stage with deceleration easing.

### D. Now Playing Modes
1. **Dynamic Artist Backdrop:**
   - Full-bleed artist photography slideshow.
   - Smooth, slow Ken-Burns effect (gentle pan and 1.0 -> 1.05 scale transition over 20 seconds).
   - Track metadata floats over subtle bottom/left linear gradient vignette.
2. **Album Art Mosaic Wall:**
   - Vast, scrolling tapestry of square album art from the entire collection.
   - Active album cover is scaled up and highlighted in the center.

### E. Player HUD (Bottom Bar)
- Height: `64px` - `72px`.
- Ultra-thin seek bar (2px normal, 4px on hover) spanning the top edge of the HUD with the active accent color.
- Transport controls: Previous, Play/Pause, Next, Shuffle, Repeat, Volume.
- Left side: Album thumbnail, track title, artist, heart rating.
- Right side: Now Playing toggle button, Mini-Player toggle.

---

## 5. Animation & Motion Rules

- **Deceleration Curve:** Use Cubic Ease Out or Exponential Ease Out for pan transitions. Movement should start fast and decelerate gently.
- **Staggered Entrance:** When loading lists or grids, stagger tile entrances by 15-25ms per item to create the classic Metro cascading entrance effect.
- **Depth Transitions:** When navigating deeper into an album or artist, the parent view slides slightly left and dims, while the child view slides in from the right.
