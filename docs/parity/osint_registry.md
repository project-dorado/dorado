# OSINT Registry — Community & Reference Sources for Dorado Parity

**Created:** 2026-09-10
**Scope:** Public, open-source Zune-adjacent projects mined for parity inspiration and
protocol/architecture reference. This registry records *what each source teaches*, the
Dorado phase it informs, and the licensing constraints on reuse.

> **Clean-room policy (mandatory).** Dorado is MIT-licensed. These sources are reference
> material only. Do **not** copy code, assets, or binaries from any source whose license is
> incompatible (notably GPL/AGPL) into `src/`. Re-implement ideas independently from public
> documentation and observable behavior. When in doubt, treat the source as read-only
> inspiration and record the derivation here.

---

## 1. Primary sources

| Source | License | Language | Informs | Why it matters |
|---|---|---|---|---|
| [`losses/rune`](https://github.com/losses/rune) | **MPL-2.0** | Dart/Flutter + Rust | Phase 13, 17 | The clearest "what if Zune kept evolving" execution: full-library **audio analysis** (Meyda), track/album/playlist **recommendations**, auto-updating **dynamic Mixes**, scrobbling, rich lyrics, and a mature **l10n** pipeline. Directly shapes the listening-intelligence and localization work. |
| [`ZuneDev/Xune`](https://github.com/ZuneDev/Xune) | verify | C# / SkiaSharp | Phase 16 | Cross-platform re-implementation of Microsoft's **Iris UI** library. Reference for declarative motion/layout semantics (pivot pan, drawer easing, art-frame animation) — informs Avalonia equivalents, not a dependency. |
| [`zunes/ZuneDiscordRPC`](https://github.com/zunes/ZuneDiscordRPC) | verify | C# | Phase 12 | Discord Rich Presence driven by Zune playback events. Canonical event/payload shape for Dorado's `Dorado.Plugins.Discord` reference plugin. |
| [`dumbie/ZuseMe`](https://github.com/dumbie/ZuseMe) | verify | C# | Phase 12 | Last.fm scrobbling from third-party players. Reference for the scrobble lifecycle (now-playing → threshold → scrobble → offline queue) implemented by `Dorado.Plugins.LastFm`. |
| [`CorySanin/zune-podcasts`](https://github.com/CorySanin/zune-podcasts) | verify | Node.js | Phase 14 | Proxy that normalizes modern podcast feeds so a legacy importer can consume them. Informs Dorado's feed-normalization service (redirects, HTML enclosures, Patreon/Anchor quirks). |
| [`whoozle/android-file-transfer-linux`](https://github.com/whoozle/android-file-transfer-linux) | **GPL-3.0** (verify) | C++ | Phase 15 | A real, cross-platform **libmtp/MTP** host implementation. Reference for MTP object-store traversal and transport behavior. **GPL — read-only reference; do not link or copy.** |
| [`zunes/Zune-Research`](https://github.com/zunes/Zune-Research) | verify | Markdown | Phase 0, 16 | Community OSINT vault documenting Zune internals and behavior. Useful for independent fidelity verification when the disassembly corpus is unavailable. |
| [`ZuneDev/ZuneNet`](https://github.com/ZuneDev/ZuneNet) | verify | TypeScript | Social (deferred) | Community recreation of `zune.net`/`social.zune.net`. Informs a future optional self-hosted/local social substitute; servers remain dead. |

## 2. Secondary / situational sources

| Source | Note |
|---|---|
| [`nomyfan/ZuneLike`](https://github.com/nomyfan/ZuneLike) | WPF/UWP control modeled on the Zune shell; useful for control-level layout details. |
| [`mediaexplorer74/Reborn-Zune`](https://github.com/mediaexplorer74/Reborn-Zune) | UWP "reborn" port; layout and shell-structure cross-check. |
| [`cdtinney/spune`](https://github.com/cdtinney/spune) | Browser-based Zune-inspired visualizer; ambient/visualizer ideas (Phase 16). |
| [`gweslab/cerf`](https://github.com/gweslab/cerf) | Universal Windows CE emulator; potential future Zune HD OS research (currently N-A). |
| [`ZuneDev/ZuneModdingHelper`](https://github.com/ZuneDev/ZuneModdingHelper) | Applies community mods to the Zune software; firmware/mod territory, out of scope (hardware N-A). |
| [`zunes/zunes.me`](https://github.com/zunes/zunes.me) | Community hub/site; provenance and docs reference. |
| [`syntax-tm/zunesoftware`](https://github.com/syntax-tm/zunesoftware) | Chocolatey package source; packaging/preservation reference. |
| [AcoustID](https://acoustid.org) / [Chromaprint](https://acoustid.org/chromaprint) (`fpcalc`) | Fingerprint service + tool used by `AcoustIdService`/`FpcalcFingerprintProvider` for scan-time metadata and acoustic dedup. Public API + MIT-style tooling; not a code source. |

---

## 3. Mapping to the parity program

| Dorado phase | Consumes | Key borrowed ideas (re-implemented) |
|---|---|---|
| **12 — Plugin host + reference plugins** | ZuneDiscordRPC, ZuseMe | Event→presence/scrobble pipelines over the existing `IPlayerCoordinator` event seam; offline scrobble queue; presence debounce. |
| **13 — Listening intelligence** | rune | Per-track audio-feature vectors (BPM/key/energy/spectral), similarity search, auto-updating dynamic Mixes ("similar to album", "top 100", "similar to favorites"). |
| **14 — Podcast modernization** | zune-podcasts | Feed normalization + redirect resolution + enclosure sanitation before library ingest. |
| **15 — MTP transport seam** | android-file-transfer-linux | MTP object enumeration/transfer semantics; validated against an in-repo virtual MTP target. |
| **16 — Fidelity polish** | Xune, Zune-Research, spune | Iris-style motion/easing, visualizer behavior (the authentic art-frame animation was replaced by the clean-room `IrisArtControl`). |
| **17 — i18n** | rune | Resource-extraction discipline, locale config, translation workflow. |

> **Status (2026-09-10):** Phases 12–23 have shipped. This table records *what each source
> informed*; the remaining open fidelity items live in `deferred_registry.md`.

---

## 4. License guardrails

- **MPL-2.0 (rune):** file-level copyleft; only safe as behavioral reference here. Do not copy files.
- **GPL-3.0 (android-file-transfer-linux):** strong copyleft; **must not** be linked or copied into Dorado. Ideas only, clean-room.
- **Unverified sources:** treat as all-rights-reserved until a license is confirmed; ideas only.
- **Assets:** no image/audio/binary assets from any community source are imported. Zune-derived
  assets already in-tree were ingested under the project's prior corpus policy (see `NOTICE.md`).

---

## 5. Maintenance

- Add a row when a new community project materially informs a phase.
- Update the **informs** column when a phase ships (applied through Phase 23).
- Re-verify licenses before any code-level consultation; rows marked *verify* are
  treated as all-rights-reserved (ideas only) until confirmed.
