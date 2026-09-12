# Deferred Registry (documented, not scheduled)

Items consciously deferred after the true-parity program (Phases 5–23) with rationale.
Each is a possible future work item; none blocks the Zune 4.8 experience Dorado delivers.

Items that **shipped** and were removed from this set: real DSP audio analysis
(`DspFeatureExtractor`), Mixview external related-artist satellites (MusicBrainz),
AcoustID scan-time metadata + acoustic dedup, and procedural chevron scroll
affordances.

| Item | Origin | Rationale for deferral |
|---|---|---|
| **i18n — full string extraction across every view** | Zune shipped localized UIs (ZuneShellResources `.UIX` per locale) | Phase 17 shipped the localization service + catalog and the 2026-09-11 follow-on expanded it to **20 locales** (en, fr, de, es, it, pt, nl, sv, da, nb, fi, pl, cs, hu, tr, ru, ja, ko, zh-Hans, zh-Hant) with a live language selector. Extracting the remaining view strings into the catalog is mechanical but touches every view; deferred as incremental follow-up. |
| **UPnP media sharing (ZuneNSS / `ZuneShareEXE` parity)** | Native component map: network sharing services | Zune's social sharing servers are dead; a local UPnP/DLNA renderer/server has no Zune-visible counterpart to validate against. |
| **Explorer / taskbar shell integration (`ZuneShellExt_Dll`, `ZuneTaskbar_Dll`, `ZuneLauncherEXE`)** | Native component map | Windows-only, shell-level (context menus, taskbar previews). Cross-platform app; value is cosmetic. |
| **MTPZ firmware update / restore / rollback (`ZuneWmduDLL` parity)** | Device lifecycle | Hardware N-A. Phase 15 landed the `MtpTransport`/`IMtpDeviceClient` seam, USB product-ID detection, and a virtual MTP contract harness; the MTPZ session layer and firmware flows remain deferred until hardware is available. |
| **Windows jump lists** | Shell integration | Windows-only convenience; documented alongside shell integration deferral. |
| **Mini-player video surface** | Phase 8 polish backlog | The mini-player video mode is text-only; real video needs a `VideoView` surface inside the compact overlay. Video remains fully playable in the main surface and full playback view. |
| **Notification-area tray icon** | Polish backlog | Avalonia tray support is platform-quirky; Zune itself only had a taskbar presence. |
| **CD Land real pipeline (Phase 10)** | Capability-gated phase | ✅ **Shipped capability-gated** (`ProcessOpticalDriveService`: `cdparanoia -Q` TOC, `cdparanoia`/`ffmpeg` rip+encode, `cdrdao` burn) behind drive/toolchain detection, verified via an injected process runner. Remaining: Windows IMAPI2 binding, and live validation on a machine with an optical drive (none on the dev host). |
| **A–Z type-ahead — remaining lists** | `SHORTCUTKEYS.UIX` KeyCommandA–Z + JumpInList | Phase 19c shipped the `TypeAheadBuffer`/`TypeAheadSearch` jump across Collection (artists/albums/songs/genres), Podcasts, Videos, and Playlists. Remaining secondary lists (e.g. Smart-playlist detail, device contents) can adopt the same helper incrementally. |
| **Real OS file associations (FILETYPES.UIX)** | `FileTypes` settings page | Zune's file-types page wired the Windows registry / Linux `mimeapps.list` for `.mp3/.m4a/.mp4` etc. Dorado's file-types page ships an in-app ingest-extension editor; OS-level registration is invasive (Windows assoc writes require elevation on some installs, Linux MIME registration is per-desktop-environment) and would need a per-platform installer hook. Left as a future cross-platform integration. |
| **Authentic chevron PNG assets on the pivot strip** | `PIVOTLIST.UIX` | Phase 16b shipped pointer drag-to-pan with friction inertia; Phase 22 shipped procedural vector chevron scroll affordances. The original decorative PNG chevron art remains absent post IP move.|
| **Settings "sharing" page** (UPnP / `ZuneNSS` / `ZuneShareEXE`) | Software settings list (Zune 4.8) | The UPnP/media-sharing server target is covered under the existing UPnP row above. The standalone settings page that toggled per-device "music/video/photos can be shared" can be added once a sharing transport exists; today the equivalent rule surface lives on the device sync-options page. |
| **OS media integration — SMTC (Windows) + hardware media keys** | Desktop player expectation (not Zune-specific) | ✅ **Linux MPRIS2 shipped** (`MprisMediaControls`, `org.mpris.MediaPlayer2.dorado`, on the patched `Tmds.DBus.Protocol 0.95.1`) behind the `ISystemMediaControls` seam; verified live on a session bus (metadata + media-key commands). Remaining: Windows SMTC requires a Windows-targeted TFM (WinRT), which the single cross-platform build does not emit. |

Last updated: 2026-09-10 (post Phase 23; program complete).
