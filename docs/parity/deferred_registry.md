# Deferred Registry (documented, not scheduled)

Items consciously deferred after the true-parity program (Phases 5–9) with rationale.
Each is a possible future work item; none blocks the Zune 4.8 experience Dorado delivers.

| Item | Origin | Rationale for deferral |
|---|---|---|
| **i18n — remaining 24 Zune locales + full string extraction** | Zune shipped localized UIs (ZuneShellResources `.UIX` per locale) | Phase 17 shipped the localization service + catalog (`en`/`fr`) and a live language selector. Extracting every remaining view string and translating the other 24 Zune locales is mechanical but touches every view; deferred as incremental follow-up. |
| **UPnP media sharing (ZuneNSS / `ZuneShareEXE` parity)** | Native component map: network sharing services | Zune's social sharing servers are dead; a local UPnP/DLNA renderer/server has no Zune-visible counterpart to validate against. |
| **Explorer / taskbar shell integration (`ZuneShellExt_Dll`, `ZuneTaskbar_Dll`, `ZuneLauncherEXE`)** | Native component map | Windows-only, shell-level (context menus, taskbar previews). Cross-platform app; value is cosmetic. |
| **MTPZ firmware update / restore / rollback (`ZuneWmduDLL` parity)** | Device lifecycle | Hardware N-A. Phase 15 landed the `MtpTransport`/`IMtpDeviceClient` seam, USB product-ID detection, and a virtual MTP contract harness; the MTPZ session layer and firmware flows remain deferred until hardware is available. |
| **Windows jump lists** | Shell integration | Windows-only convenience; documented alongside shell integration deferral. |
| **Mini-player video surface** | Phase 8 polish backlog | The mini-player video mode is text-only; real video needs a `VideoView` surface inside the compact overlay. Video remains fully playable in the main surface and full playback view. |
| **Mixview external related-artist satellites** | Phase 6 backlog | Mixview satellites currently use the local library (genre/mood/related); external MusicBrainz related-artist fetch would add network latency and rate-limit pressure to a purely visual surface. |
| **Notification-area tray icon** | Polish backlog | Avalonia tray support is platform-quirky; Zune itself only had a taskbar presence. |
| **CD Land real pipeline (Phase 10)** | Capability-gated phase | No optical drive is available on the development machine; the DISC view stays in its manual/simulated mode. Implementation should be done blind against platform tooling (`cdparanoia`/`cdrdao`/IMAPI2) only if explicitly requested. |
| **A–Z type-ahead jump-in-list** | `SHORTCUTKEYS.UIX` KeyCommandA–Z + JumpInList | The Zune-published buffer-as-prefix semantics are not fully decodable from the compiled table without an Iris UIB parser, and the existing per-key shortcuts (Ctrl+P/F/B/H/T/M/E, F1, /) cover the common cases. A future pass can re-decode the letter-table and wire the jump buffer into the library/collection/search lists. |
| **Real OS file associations (FILETYPES.UIX)** | `FileTypes` settings page | Zune's file-types page wired the Windows registry / Linux `mimeapps.list` for `.mp3/.m4a/.mp4` etc. Dorado's file-types page ships an in-app ingest-extension editor; OS-level registration is invasive (Windows assoc writes require elevation on some installs, Linux MIME registration is per-desktop-environment) and would need a per-platform installer hook. Left as a future cross-platform integration. |
| **Drag-inertia panoramic pivot strip** | `PIVOTLIST.UIX` + Zune's smooth-deceleration pan | Dorado ships mouse-wheel pan (`PointerWheelChanged` → ScrollViewer) for the pivot strip bleed-off at the authentic 734×500 min width. Zune's true behavior is touch/drag with inertia + chevron scroll-arrow assets. A drag-pan gesture recognizer + scroll-arrow overlay is a future pass; wheel-pan is the pragmatic stand-in. |
| **Settings "sharing" page** (UPnP / `ZuneNSS` / `ZuneShareEXE`) | Software settings list (Zune 4.8) | The UPnP/media-sharing server target is covered under the existing UPnP row above. The standalone settings page that toggled per-device "music/video/photos can be shared" can be added once a sharing transport exists; today the equivalent rule surface lives on the device sync-options page. |

Last updated: 2026-09-09 (post Phases 1–5 parity batch).
