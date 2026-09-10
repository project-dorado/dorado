# Dorado ↔ Microsoft Zune 4.8 Parity Audit

**Audit date:** 2026-09-09
**Method:** Systematic comparison of the decompiled Microsoft Zune Desktop 4.8 component stack against the current Dorado implementation.

---

## 1. Evidence Base (Decompilation Corpus)

| Artifact | Source | Decompile/Extract | Size |
|---|---|---|---|
| `ZuneShell_Dll` (managed) | `Zune-x86.msi` | ilspycmd → `zune-disassembly/zuneshell/` | 821 C# files (`ZuneUI/` = 685) |
| `ZuneDBApi_Dll` (managed) | `Zune-x86.msi` | ilspycmd → `zune-disassembly/zunedbapi/` | 1,030 C# files (19 namespaces) |
| `ZuneShellResources_Dll` RCDATA | `Zune-x86.msi` | 7z PE extraction → `zune-disassembly/zuneshell_resources/` | 1,857 resources (241 `.UIX` documents, 1,289 PNG, 44 JPG, 4 WAV) |
| `zune-x64.msi` | `ZuneSetupPkg.exe` | inventoried → `zune-disassembly/setup_pkg/x64/` | x64 player payload (same managed code as x86) |
| `zunewmdu-x86/x64.msi` | `ZuneSetupPkg.exe` | 7z → `zune-disassembly/wmdu/` | `ZuneWmduDLL` (native firmware-update service) |
| Native component stack | `Zune-x86.msi` | inventoried → `zune-disassembly/msi/` | UIX engine ×5, ZMDB engine ×4, codecs, MTPZ, bus driver, services |

Both `review/ZunePackage.exe` and `review/ZuneSetupPkg.exe` verified: `ZunePackage.exe` sha256 `ef5e5deb…` identical to the previously disassembled copy (no rework required). `ZuneSetupPkg.exe` is the offline multi-language setup bootstrapper containing the x86/x64 MSI set.

### Zune 4.8 native component map (from MSI payload)

- **Iris UIX engine:** `UIX_Dll`, `UIXcontrols_Dll`, `UIXrender_Dll`, `UIX_renderapi_Dll`, `UIXsup_Dll` (native Direct3D declarative renderer)
- **Database (ZMDB):** `ZuneDB_dll`, `ZuneDBApi_Dll` (managed API), engine variants `ZuneZMDBClassicDLL`, `ZuneZMDBLibraryDLL`, `ZuneZMDBMobileDLL`, `ZuneZMDBZuneHDDLL`
- **Audio:** `ZuneSE_dll`, `ZuneAACDec_Dll`, `ZuneSrcWrpDLL`, `ZuneEncEngDLL` + `ZuneEncEXE` (MP3/WMA/AAC encoders), `l3codecp.acm`
- **Video:** `ZuneH264Dec_Dll`, `ZUNEMp4Dec_Dll`, `ZuneDXVA2_Dll`, `ZuneEvr_Dll`
- **Device/USB:** `ZuneMTPZ_dll` (MTPZ), `Zumbus.sys` + INF (kernel bus driver), `ZuneBusEnumSvc`, `ZuneDriver_dll`, `ZuneUsbTransport_dll`, `ZuneIpTransport_dll`, `ZuneTcp2Udp_dll`, `WMZUNE*` (USB-PPP)
- **Services:** `ZuneService_Dll`, `ZuneNssExe`/`ZuneShareEXE` (network sharing), `ZuneWlanCfgSvc`, `ZuneNetProxy_dll`, `ZuneCfg_Dll`, `ZuneConfigEXE`
- **Shell integration:** `ZuneShellExt_Dll`, `ZuneTaskbar_Dll`, `ZuneLauncherEXE`
- **Marketplace/DRM:** `ZuneMarketplaceResources_Dll`, PPCRL/MSIDCRL (Passport auth), `ContentDirectoryXML`, `MediaReceiverRegistrarXML` (UPnP/WMC)

---

## 2. Executive Summary

> **RE-AUDIT SNAPSHOT (2026-09-09, post Phases 5–9):** The critical gaps closed below moved weighted overall parity from **≈ 55–60% → ≈ 75–80%** (weighted for UI presentation, hardware-dependent features neutral). Deltas per domain: **F. Audio engine** SIMULATED 0% → **HIGH ~85%** (ManagedBass 2.4: real decode, gapless, crossfade, ReplayGain, EQ, FFT visualizer, podcast streams); **L. First-launch** MISSING 0% → **HIGH ~85%** (welcome→folders→privacy→done wizard, What's New, monitored-folder scan); **H. Device sync** SIMULATED ~35% → **MEDIUM ~65%** (sync-group engine, dry-run manifest, guest sessions, reverse sync, transport seam; real MTPZ hardware remains N-A); **C. Collection** 80% → **HIGH ~90%** (smart playlists, Find-Album-Info per-track matching, autocomplete, back-stack, Mixview tiles); **D. Now Playing** 75% → **HIGH ~85%** (now-playing video clips via libVLC); **I. Podcasts** 50% → **MEDIUM-HIGH ~70%** (real audio playback); **M. Platform services** 40% → **~50%** (video/photo libraries on the SQLite ZMDB substitute). Remaining below-70% domains: B. Quickplay (~65%), E. Mixview (~60%), G. CD Land (~40%, capability-gated — no optical drive), J. Social/Marketplace (N-A, dead servers), M. Platform services (~50%: no UPnP/share/MUI).
>
> The original pre-Phase-5 snapshot is preserved below for history.

Dorado is a **faithful UI shell** with a **growing feature set** built on clean architecture, but it has **one critical structural gap: there is no real audio playback engine** — `AudioEngine` (`src/Dorado.Infrastructure.Audio/AudioEngine.cs`) is a position-ticker simulation and no audio library (NAudio/ManagedBass) is referenced anywhere. Every audible experience (music, crossfade, ReplayGain, volume, the visualizer, podcast streams) is currently simulated; the only real audio output is `SoundEffectService` playing authentic Zune WAV chimes through OS CLI players.

| Domain | Parity | Verdict |
|---|---|---|
| A. Shell & Navigation | **HIGH (~85%)** | Authentic chrome, pivots, transport, shortcuts |
| B. Quickplay | **MEDIUM (~65%)** | Decks + Smart DJ substitute; no hub artwork maps |
| C. Collection (music) | **HIGH (~80%)** | Full browsing/editing; Find-Album-Info is art-only |
| D. Now Playing | **HIGH (~75%)** | Ken-Burns + real metadata; no video clips |
| E. Mixview | **MEDIUM (~60%)** | Constellation + MixStack; similarity is local-only |
| F. **Audio engine** | **SIMULATED (0%)** | **No sound; all DSP is fake** |
| G. CD Land | **SIMULATED (~40%)** | Full UI, simulated rip/burn (no drive available) |
| H. Device sync & lifecycle | **SIMULATED (~35%)** | Gas gauge UI real; sync/firmware simulated |
| I. Podcasts | **PARTIAL (~50%)** | RSS subscribe; no marketplace; playback simulated |
| J. Social / Marketplace / Account | **N-A (substitute ~70%)** | Zune Card local substitute; servers dead |
| K. Settings & Management | **HIGH (~75%)** | Two-tier pivots + persistence (Phase 4) |
| L. First-launch & onboarding | **MISSING (0%)** | No wizard |
| M. Platform services (ZMDB, sharing) | **PARTIAL (~40%)** | SQLite substitute; no UPnP/share/MUI |

**Weighted overall parity: ≈ 55–60%** (weighting UI presentation strongly, hardware-dependent features neutrally).

**Top 3 actions for Phase 5:**
1. **Real audio playback engine** — integrate a cross-platform .NET 8 audio library (NAudio WASAPI/ALSA or ManagedBass); this unblocks crossfade, ReplayGain, volume, gapless, FFT visualizer, and podcast playback. *Everything else in the audio domain is blocked on this.*
2. **First-launch onboarding wizard** (welcome → monitored folders → privacy → done; mirrors `FirstLaunchLand` flow).
3. **Find Album Info track matching** — extend the Phase 4 art lookup to the per-track metadata review dialog Zune used (`FINDALBUMINFOSONGMATCH.UIX`).

---

## 3. Parity Matrix

Statuses: **FULL** (implemented, real) · **PARTIAL** (subset) · **SIMULATED** (UI real, behavior fake) · **MISSING** (absent) · **N-A** (Zune server/hardware dependency is dead; substitute noted).

### A. Shell & Navigation

| Zune 4.8 feature | Evidence | Dorado | Status |
|---|---|---|---|
| Custom borderless chrome, minimal window controls | `NONCLIENTCONTROLS.UIX`, `TOPTOOLBAR.UIX` | `MainShellView.axaml` + authentic `WINDOW.*.PNG` | FULL |
| Panoramic top pivots (4: QUICKPLAY/COLLECTION/DEVICE/SETTINGS) | `PIVOTLIST.UIX`, `Shell.uix` | `NavigationPivot` + opacity-modulated pivot strip (adds SOCIAL/DISC/MIXVIEW — deliberate extension) | FULL |
| Docked bottom transport (hairline scrub, tri-state heart) | `TRANSPORTCONTROLS.UIX`, `BOTTOMTOOLBAR.UIX` | Docked HUD + authentic transport assets | FULL |
| Page stack with back navigation | `PAGESTACK.UIX`, `Page.cs`, `ZunePage.cs` | Pivot switching only; back-stack exists solely in Mixview (`MixStack`) | PARTIAL |
| Search box with autocomplete | `AUTOCOMPLETEBOX.UIX` | Instant header search (no autocomplete dropdown) | PARTIAL |
| Global keyboard shortcuts | `SHORTCUTKEYS.UIX` | Ctrl+P/B/F/H/T/M/E, F7–F9, Esc | FULL |
| Min window 734×500 | `Shell.c_minimumWindowWidth/Height` | `MainWindow.axaml` | FULL |
| Compact mini-player (audio) | `MINIMODE.UIX`, `MINIMODEAUDIO.UIX`, `MINIMODEJUMPLIST.UIX` | `CompactMiniPlayerView` (420×130, Ctrl+M) | PARTIAL (no jump-list hook, no video mini-mode) |
| Notification area / taskbar integration | `NOTIFICATIONAREA.UIX`, `ZuneTaskbar_Dll` | None (platform-specific) | N-A |
| Jump lists (recent/pinned tasks) | `JUMPLIST.UIX`, `JUMPINLIST.UIX` | None (Windows shell feature) | N-A |
| "What's New" hub tile | `WHATSNEW.UIX` | — | MISSING |
| Animated equalizer icon | `ANIMATEDICONBUTTON.UIX` | 10-frame `ICON.NOWPLAYING.FRAME*` cycle | FULL |

### B. Quickplay

| Zune 4.8 feature | Evidence | Dorado | Status |
|---|---|---|---|
| Pins / History / New 3-deck panorama | `QUICKPLAYSTRIP.UIX`, `QUICKPLAYMODULE.UIX`, `QuickplayPage.cs`, `QuickplayExperience.cs` | `QuickplayView` 3-deck sliding panorama | FULL |
| Quick Mix one-click mix | `QUICKMIX.UIX`, `QuickMixSessionManager.cs`, `QuickMixPlaylistFactory.cs` | `SmartDJEngine` (local-library scoring) | PARTIAL (substitute; Zune's used marketplace) |
| Quick Mix progress + notification | `QuickMixProgress.cs`, `QuickMixNotification.cs` | Mix launch + queue population | PARTIAL |
| Hub hero artwork maps | `QuickPlayMap_*.png`, `SoftwareMap_*.png` | Card-based hero (no artwork maps) | MISSING |
| Auto-playlist dialog | `AUTOC/ AUTOPLAYLISTDIALOG.UIX` | — | MISSING |
| Radio panel (streams) | `RADIOPANEL.UIX`, `RadioPage.cs` | — | N-A (dead streams) |
| Ad/best-value tiles | `BESTVALUE.UIX`, `BILLINGOFFER.UIX` | — | N-A |

### C. Collection (Music)

| Zune 4.8 feature | Evidence | Dorado | Status |
|---|---|---|---|
| Two-column artist discography browser | `ARTISTSPANEL.UIX`, `MusicLibraryPage.cs` | `CollectionView` artists pivot | FULL |
| Album artwork grid | `ALBUMSPANEL.UIX` | Albums grid with real covers (Phase 4) | FULL |
| Dense track data table | `TRACKSPANEL.UIX`, `TRACKSPANELCOLUMNS.UIX`, `SPREADSHEET*.UIX` | Songs data table | FULL |
| Genre hub | `GENRESPANEL.UIX` | Genres card grid | FULL |
| Playlists + ZPL export | `PLAYLISTS*.UIX`, `PLAYLISTDIALOG.UIX`, `ADDTOPLAYLIST.UIX`, `Microsoft.Zune.Playlist` | `PlaylistsView`, ZPL XML export | FULL |
| Smart/auto playlists | `AUTOPLAYLISTDIALOG.UIX` | — | MISSING |
| Metadata editor | `EDITMEDIAINFODIALOG.UIX`, `EDITMEDIAINFOCONTROLS.UIX` | `MetadataEditView` + TagLibSharp writeback | FULL |
| Find Album Info (per-track match + art) | `FINDALBUMINFODIALOG.UIX`, `FINDALBUMINFOSONGMATCH.UIX`, `AlbumArtUpdateHandler.cs` | Phase 4 art-only lookup (no per-track matching review) | PARTIAL |
| Folder watching | `FirstLaunchMonitoredFoldersPage.cs`, `ADDTOCOLLECTION.UIX` | Debounced `FileSystemWatcher` | FULL |
| Ratings (heart / broken heart) | `RATING.LIKEIT/HATEIT.PNG` | Context menus + HUD hearts | FULL |
| **Video library** | `VIDEOLIBRARY.UIX`, `VideoLibraryPage.cs`, `VideosPanel.cs` | — | MISSING |
| **Photo library + gallery + slideshow** | `PHOTOLIBRARY.UIX`, `GALLERYVIEW.UIX`, `PHOTOSLIDESHOW.UIX`, `SlideshowLand.cs` | — | MISSING |

### D. Now Playing

| Zune 4.8 feature | Evidence | Dorado | Status |
|---|---|---|---|
| Cinematic artist canvas + Ken-Burns | `NOWPLAYINGLAND.UIX`, `NOWPLAYINGMUSICBACKGROUND.UIX`, `NOWPLAYINGSTYLES.UIX`, `NowPlayingLand.cs` | `NowPlayingView` + real Fanart.tv backdrops (Phase 4) | FULL |
| Typographic track overlays | `NOWPLAYINGSTYLES.UIX` | 60pt title / 32pt accent artist | FULL |
| Showlist (upcoming queue drawer) | `TRANSPORT.SHOWLIST.ON/OFF.PNG` | Slide-out showlist drawer | FULL |
| Lyrics & bio drawer | `NOWPLAYINGLAND.UIX` | Real Wikipedia bios + LRCLIB lyrics (Phase 4) | FULL |
| Album mosaic wall | `NOWPLAYINGALBUMGRIDDEFS.UIX` | MosaicWall mode | FULL |
| Auto-hiding HUD | `NowPlayingLand.cs` (idle timers) | 3.5s idle fade | FULL |
| Now Playing video clips | `NOWPLAYINGCLIPS.UIX` | — | MISSING (no video) |
| Ambient visualizer | (rendered by Iris engine) | Simulated spectrum bars (no FFT — blocked on audio engine) | SIMULATED |

### E. Mixview

| Zune 4.8 feature | Evidence | Dorado | Status |
|---|---|---|---|
| Constellation graph (seed + orbiting tiles) | `MIXCONTROLS.UIX`, `MIXLAYER.UIX`, `MIXLAYOUT.UIX`, `MixResult*.cs` | `MixviewView` interactive canvas | FULL |
| MixStack back navigation | `MixStack.cs`, `MixStackEntry.cs` (mirrored 1:1 in `MixModels.cs`) | `MixStack` push/pop + breadcrumb | FULL |
| Server similarity queries | `MIXQUERY.UIX`, `MixPriorityList*.cs` (marketplace-backed) | Local-library scoring (genre/artist/favorites) | PARTIAL (substitute) |
| Hover action tiles (play/like/hate/info/add) | `MIX.PLAY/HATEIT/LIKEIT/INFO/ADD.PNG` | Play + Smart DJ actions; rating tiles present but unwired | PARTIAL |
| Glide-to-center transition | `MIXMANAGER.cs`, Iris animation | Animated re-centering | PARTIAL |

### F. Audio Engine ⚠️ CRITICAL

| Zune 4.8 feature | Evidence | Dorado | Status |
|---|---|---|---|
| Real audio playback (decode + output) | `ZuneSE_dll`, `ZuneCore_Dll`, codec DLLs | `AudioEngine` = position timer only; no NAudio/Bass/WASAPI | **SIMULATED** |
| Volume control | `TRANSPORTCONTROLS.UIX` slider | Stored property, no signal path | SIMULATED |
| Crossfade (0–10s) + gapless | `SyncControls`-adjacent playback options | `CrossfadeDurationSeconds` plumbed through UI only | SIMULATED |
| ReplayGain / volume leveling | `ReplayGainTrackGainDb` fields in `ZuneDBApi` (`TrackMetadata`) | Settings toggle, no DSP | SIMULATED |
| Sound effects (completion chimes) | `COMPLETEDSYNCBURNCD.WAV` et al. | `SoundEffectService` real WAV playback via OS CLI | FULL |
| MP3/WMA/AAC encode (rip) | `ZuneEncEngDLL`, `ZuneEncEXE` | Simulated rip progress | SIMULATED |
| AAC/H.264 decode, EVR/DXVA video | `ZuneAACDec_Dll`, `ZuneH264Dec_Dll`, `ZuneDXVA2_Dll`, `ZuneEvr_Dll` | — | MISSING |

### G. CD Land

| Zune 4.8 feature | Evidence | Dorado | Status |
|---|---|---|---|
| CD view & disc detection | `CDVIEW.UIX`, `CDSTATE.UIX`, `CDLand.cs` | `DISC` pivot + `CDView` | PARTIAL (no optical-drive detection) |
| Rip pipeline | `RipState.cs`, `ZuneEncEngDLL` | Simulated progress + authentic chime | SIMULATED (no drive on dev machine — accepted) |
| Burn pipeline | `BurnableCD.cs`, `BurnSessionItem.cs`, `PURCHASEFORBURN.UIX` | Simulated burn queue + chime | SIMULATED |
| Completion chimes | `COMPLETEDRIPREVERSESYNC.WAV`, `COMPLETEDSYNCBURNCD.WAV` | Wired to rip/burn completion | FULL |

### H. Device Sync & Lifecycle (hardware-dependent)

| Zune 4.8 feature | Evidence | Dorado | Status |
|---|---|---|---|
| Device land + segmented gas gauge | `DEVICELANDELEMENTS.UIX`, `GASGAUGE.UIX`, `Deviceland.cs`, `DeviceExperience.cs` | `DeviceView` + 6-segment authentic gauge | FULL (data simulated) |
| Space reservation | `DEVICESPACERESERVATION.UIX` | Slider + GB preview (Settings + Device) | FULL (persisted Phase 4) |
| Sync options & sync groups | `DEVICESYNCOPTIONS.UIX`, `DEVICESYNCGROUPS.UIX`, `SyncGroup.cs`, `SchemaSyncGroup.cs`, `SyncCategory.cs`, `SyncMode.cs` | Music/podcast rule selection lists | PARTIAL (no real sync-group engine) |
| Sync animation/toast | `SYNCANIMATION.UIX`, `SYNCINSTRUCTIONTOAST.UIX`, `SYNCNOTIFICATION.UIX`, `SyncNotification.cs` | Status text | MISSING |
| MTP device contents browsing | `IDeviceContentsPage.cs`, `DEVICEPICTUREVIDEO.UIX` | — | MISSING (N-A without hardware) |
| Wireless sync | `WIRELESSSYNC.UIX`, `WirelessSyncWizard.cs`, `WirelessSync*.Page.cs` | Wireless settings UI states | SIMULATED |
| Windows Phone wireless sync | `MOBILEWIRELESSSYNC.UIX`, `MobileWirelessSyncWizard.cs` | — | N-A |
| Firmware update / restore / rollback | `DEVICEUPDATE*.UIX`, `DEVICE RESTORE*.UIX`, `DeviceRollback*.cs`, `ZuneWmduDLL` | — | MISSING (N-A hardware) |
| USB bus (kernel driver + enum svc + MTPZ) | `Zumbus.sys`, `ZuneBusEnumSvc`, `ZuneMTPZ_dll`, `ZuneUsbTransport_dll` | `Infrastructure.Devices` simulated sync; `ZuneUsbHttpInterceptor` (localhost placeholder for USB-PPP interception) | SIMULATED |
| Guest sync | `GuestSchemaSyncGroup.cs`, `FirstConnectDeviceGuestWarning` | — | MISSING |

### I. Podcasts

| Zune 4.8 feature | Evidence | Dorado | Status |
|---|---|---|---|
| Podcast library + series + episodes | `PODCASTLIBRARY.UIX`, `PODCASTSERIESPANEL.UIX`, `PODCASTEPISODESPANEL.UIX`, `PodcastLibraryPage.cs` | `PodcastsView` two-column manager | PARTIAL (RSS-only substitute) |
| RSS subscription | (marketplace feed in Zune) | Real RSS 2.0 ingest | FULL (substitute) |
| Episode details dialog | `PODCASTDETAILSPANEL.UIX`, `PodcastEpisodeDetails.cs` | Episode list with descriptions | PARTIAL |
| Podcast sync limits | `PodcastSyncLimit.cs`, `PODCASTSYNC` | Sync-rule strings in Settings | PARTIAL |
| Channel/radio integration | `CHANNEL*.UIX`, `Channel*.cs` | — | N-A (marketplace) |

### J. Social / Marketplace / Account — *Zune servers dead (~2011–2015)*

| Zune 4.8 feature | Evidence | Dorado | Status |
|---|---|---|---|
| Zune Card / profile | `PROFILE.UIX`, `PROFILEEDIT.UIX`, `ProfilePage.cs` | `ZuneCardView` + `SOCIAL` pivot (local stats) | N-A — substitute FULL |
| Achievements/badges | `BADGES.UIX`, `ProfileBadge.cs` | 5 badges with authentic seal asset | N-A — substitute FULL |
| Friends & messaging | `FRIENDS.UIX`, `INBOX*.UIX`, `FriendsPage.cs`, `Microsoft.Zune.Messaging` | — | N-A |
| Social composer | `SOCIALCOMPOSER.UIX` | — | N-A |
| Marketplace, Zune Pass, cart, billing | `CARTPAGE/CARTPANEL.cs`, `Subscription*.cs`, `ACCOUNTCREATION.UIX`, `CREATEPASSPORT.UIX`, `PAYMENTINSTRUMENT.UIX` | — | N-A |
| DRM state | `DrmStateDescriptions.cs` | — | N-A |
| Sign-in (Passport/Live) | `SIGNINDIALOG.UIX`, `WINDOWS LIVELIVE.UIX`, `Microsoft.Zune.UserCredential` | — | N-A |

### K. Settings & Management

| Zune 4.8 feature | Evidence | Dorado | Status |
|---|---|---|---|
| Two-tier management hub | `MANAGEMENT*.UIX` (14 documents) | SOFTWARE/DEVICE tiers, 7 sub-pivots | PARTIAL (subset) |
| Collection settings (monitored folders) | `MANAGEMENTCOLLECTION.UIX` | COLLECTION sub-pivot + folder picker + scan | FULL |
| Rip settings | `MANAGEMENTRIP.UIX` | RIP sub-pivot (format/bitrate/destination) | FULL (encoder simulated) |
| Burn settings | `MANAGEMENTBURN.UIX` | BURN sub-pivot | FULL (burner simulated) |
| Metadata settings | `MANAGEMENTMETADATA.UIX` | Metadata sub-pivot + provider gating (Phase 4) | FULL |
| Display settings | `MANAGEMENTDISPLAY.UIX` | Display sub-pivot (accents, themes, compact) | FULL |
| General/file-type/privacy/sharing | `MANAGEMENTGENERAL/FILETYPES/PRIVACY/SHARING.UIX` | — | MISSING (privacy/sharing partly N-A) |
| Subscription/purchases/rentals | `MANAGEMENTSUBSCRIPTION/PURCHASES/RENTALS.UIX` | — | N-A |
| Settings persistence | `ZuneCfg_Dll` | `JsonSettingsStore` (Phase 4) | FULL |

### L. First-Launch & Onboarding

| Zune 4.8 feature | Evidence | Dorado | Status |
|---|---|---|---|
| First-launch wizard (welcome→folders→filetypes→privacy) | `FIRSTLAUNCH.UIX`, `FirstLaunch*.Page.cs` ×7 | Demo-data seed only | MISSING |
| First-connect device wizard | `FIRSTCONNECT.UIX`, `FirstConnect*.Page.cs` ×6 | — | N-A hardware |
| Setup land | `SETUPLAND.UIX`, `SetupLandPage.cs` | — | MISSING |

### M. Platform Services

| Zune 4.8 feature | Evidence | Dorado | Status |
|---|---|---|---|
| ZMDB database engine (4 variants) | `ZuneZMDB{Classic,Library,Mobile,ZuneHD}DLL`, `ZuneDB_dll` | EF Core + SQLite (single schema) | PARTIAL (substitute) |
| ZMDB managed API | `ZuneDBApi_Dll` → 1,030 files, 19 namespaces (`MicrosoftZuneLibrary`, `Microsoft.Zune.Service`, `Microsoft.Zune.QuickMix`, `Microsoft.Zune.Playlist`, `Microsoft.Zune.Subscription`, `Microsoft.Zune.User`, …) | `IMediaLibraryService` (+11 other interfaces) | PARTIAL (media core covered; subscription/user/service layers N-A) |
| Play-history & stats | `MicrosoftZuneLibrary` history tables | `PlayHistoryEntry` + `UserStatsService` | FULL (substitute) |
| Network media sharing (UPnP/WMC) | `ZuneNssExe`, `ZuneShareEXE`, `ContentDirectoryXML`, `MediaReceiverRegistrarXML` | — | MISSING |
| Background service host | `ZuneService_Dll` | In-process coordinators | PARTIAL |
| Multi-language MUI (26 locales) | `ZuneResources_Mui`, `*mui` files | English only | MISSING |
| Explorer shell extension / launcher | `ZuneShellExt_Dll`, `ZuneLauncherEXE` | — | N-A |
| x64 build | `zune-x64.msi` | .NET 8 cross-platform (x64/arm64 CI) | FULL |
| Plugin extensibility | (none in Zune — COM/registry only) | `Dorado.Plugins.Protocol` JSON-RPC scaffold (unused) | FULL (exceeds Zune) |

---

## 4. Deliberate Substitutions for Dead Zune Services

| Dead Zune dependency | Dorado replacement |
|---|---|
| Zune Marketplace / Zune Pass (catalog, streaming, DRM) | Local library only; Smart DJ from local tracks |
| Zune Social servers (profiles, messaging, badges) | Local `ZuneCardView` stats + badge engine from SQLite history |
| Marketplace Quick Mix seeds | Local similarity scoring (album/artist/genre/favorite vectors) |
| Marketplace podcast catalog | Direct RSS 2.0 subscriptions |
| Marketplace album art/metadata service | MusicBrainz + Cover Art Archive + Fanart.tv + LRCLIB (Phase 4) |
| Zune device + ZuneHD wireless sync | Simulated device with authentic UI (real MTPZ deferred until hardware available) |

---

## 5. Phase 5+ Roadmap (ranked)

1. **P0 — Real audio playback engine.** Add NAudio (WASAPI on Windows, ALSA/PulseAudio on Linux) or ManagedBass to `Infrastructure.Audio`; rewire `PlaybackQueueCoordinator` to a real output device. Unblocks: volume, crossfade, gapless, ReplayGain, FFT visualizer, podcast/CD playback. *This is the single highest-leverage parity item.*
2. **P1 — First-launch wizard** (`FIRSTLAUNCH.UIX` flow): welcome → monitored folders → done, with demo-data decision point.
3. **P1 — Find Album Info track matching**: per-track MBID resolution + review dialog (`FINDALBUMINFOSONGMATCH.UIX` parity), optionally honoring `WriteTagsToFile`.
4. **P2 — Smart/auto playlists** (`AUTOPLAYLISTDIALOG.UIX` parity): rule-builder persisted like ZPL playlists.
5. **P2 — Mixview rating tiles wiring** (like/hate/info/add actions on constellation nodes using the already-extracted `MIX.*` assets).
6. **P3 — Video support** (playback engine extension + `VIDEOLIBRARY.UIX` view) and **Photo library + slideshow**.
7. **P3 — Sync UX polish** (sync animation, instruction toast, notification area).
8. **Deferred (N-A/hardware):** MTPZ device sync internals, firmware update/restore, wireless pairing, UPnP sharing.

---

## 6. Audit Confidence Notes

- Managed-code evidence is complete (`ZuneShell_Dll` + `ZuneDBApi_Dll` fully decompiled); native components (`UIX_*`, `ZuneSE_dll`, `ZuneEncEngDLL`, `ZuneMTPZ_dll`, `Zumbus.sys`) were inventoried but not disassembled at machine-code level — their behavior is inferred from names, interfaces, strings, and the managed layers that call them.
- UIX documents are compiled `.uib` bytecode in `ZuneShellResources_Dll` RCDATA; the 241 `.UIX` source names + 1,289 asset names used here come from the resource table (assets previously ingested into `src/Dorado.UI/Assets/Zune/`).
- Feature statuses were verified against the Dorado source tree (13 views, 13 view-models, 10 application interfaces, 6 application services, 4 infrastructure projects, 58 passing tests) as of commit `6570600`.
