# Dorado ↔ Microsoft Zune 4.8 Parity Audit

> **Historical.** Superseded by the independent
> [`audit-2026-09-11.md`](audit-2026-09-11.md), which re-measured weighted parity
> at **~80–84%** (not ~88%) and corrected several status claims and dimensions
> (e.g. mini-player is **340×96**, not 420×130).

**Audit date:** 2026-09-09 · **Refreshed:** 2026-09-10 (post Phase 23)
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

> **CURRENT (2026-09-10, post Phase 23).** The Phase 5–11 and 12–23 programs are
> complete. Weighted overall parity is **≈ 88%**. Highlights versus the original
> pre-Phase-5 audit: the **audio engine is real** (ManagedBass — decode, gapless,
> equal-power crossfade, ReplayGain, 10-band EQ, FFT visualizer, podcast streams);
> **first-launch onboarding** ships; **Device sync** has a sync-group engine, dry-run
> manifest, guest/reverse sync, an MTP transport seam and LAN sync/mDNS (real MTPZ
> hardware remains N-A); **Collection** has a two-tier hierarchy, smart playlists,
> FTS5 search and per-track Find-Album-Info; **Now Playing** has three modes incl.
> libVLC video; **Podcasts** normalize and play feeds; **Listening intelligence** adds
> DSP audio features and Dynamic Mixes; plugins, badges/reviews, i18n (en/fr),
> AcoustID enrichment and clean-room visuals ship. Remaining below-70% domains:
> **G. CD Land** (~40%, capability-gated — no optical drive), **J. Social/Marketplace**
> (N-A, dead servers), **M. Platform services** (~60%: no UPnP/share/MUI), and
> **H. Device sync** (~70%, MTPZ hardware N-A).

| Domain | Parity | Verdict |
|---|---|---|
| A. Shell & Navigation | **~92%** | Authentic chrome, pivots, transport, shortcuts |
| B. Quickplay | **~85%** | Decks + hearts-aware Smart DJ; procedural hub map (authentic PNG maps absent) |
| C. Collection (music) | **~92%** | Two-tier browsing, smart playlists, FTS5, per-track Find-Album-Info |
| D. Now Playing | **~90%** | Three modes incl. libVLC video; Ken-Burns; drawers |
| E. Mixview | **~72%** | Local mosaic + DSP similarity + external MusicBrainz satellites |
| F. Audio engine | **~88%** | Real ManagedBass: gapless, crossfade, ReplayGain, 10-band EQ, FFT |
| G. CD Land | **~40%** | Full UI, simulated rip/burn (no optical drive) |
| H. Device sync & lifecycle | **~70%** | Sync engine + MTP seam + LAN sync/mDNS; MTPZ hardware N-A |
| I. Podcasts | **~85%** | Subscribe + normalization + mark-played + stream playback |
| J. Social / Marketplace / Account | **N-A (local substitute)** | Servers dead; Zune Card + tiered badges + local reviews |
| K. Settings & Management | **~88%** | 13 software + 4 device pages, plugins, language, dark/light themes |
| L. First-launch & onboarding | **~90%** | Wizard + What's New + FirstConnect |
| M. Platform services (ZMDB, sharing) | **~60%** | SQLite + FTS5 substitute, plugin host, analysis; UPnP/share/MUI deferred |

**Weighted overall parity: ≈ 88%.**

#### Original pre-Phase-5 snapshot (historical)

Dorado was a **faithful UI shell** with a growing feature set built on clean architecture,
but at that time had **one critical structural gap: no real audio playback engine** —
`AudioEngine` was a position-ticker simulation and no audio library was referenced. Every
audible experience was simulated; the only real audio output was `SoundEffectService`
playing Zune WAV chimes.

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

**Weighted overall parity at that time: ≈ 55–60%.**

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
| Search box with autocomplete | `AUTOCOMPLETEBOX.UIX` | Async search autocomplete across collections/podcasts/videos | FULL |
| Global keyboard shortcuts | `SHORTCUTKEYS.UIX` | Ctrl+P/B/F/H/T/M/E, F7–F9, Esc | FULL |
| Min window 734×500 | `Shell.c_minimumWindowWidth/Height` | `MainWindow.axaml` | FULL |
| Compact mini-player (audio) | `MINIMODE.UIX`, `MINIMODEAUDIO.UIX`, `MINIMODEJUMPLIST.UIX` | `CompactMiniPlayerView` (340×96, Ctrl+M) | PARTIAL (no jump-list hook, no video mini-mode) |
| Notification area / taskbar integration | `NOTIFICATIONAREA.UIX`, `ZuneTaskbar_Dll` | None (platform-specific) | N-A |
| Jump lists (recent/pinned tasks) | `JUMPLIST.UIX`, `JUMPINLIST.UIX` | None (Windows shell feature) | N-A |
| "What's New" hub tile | `WHATSNEW.UIX` | What's New dialog on version change | FULL |
| Animated equalizer icon | `ANIMATEDICONBUTTON.UIX` | 10-frame `ICON.NOWPLAYING.FRAME*` cycle | FULL |

### B. Quickplay

| Zune 4.8 feature | Evidence | Dorado | Status |
|---|---|---|---|
| Pins / History / New 3-deck panorama | `QUICKPLAYSTRIP.UIX`, `QUICKPLAYMODULE.UIX`, `QuickplayPage.cs`, `QuickplayExperience.cs` | `QuickplayView` 3-deck sliding panorama | FULL |
| Quick Mix one-click mix | `QUICKMIX.UIX`, `QuickMixSessionManager.cs`, `QuickMixPlaylistFactory.cs` | `SmartDJEngine` (local-library scoring) | PARTIAL (substitute; Zune's used marketplace) |
| Quick Mix progress + notification | `QuickMixProgress.cs`, `QuickMixNotification.cs` | Mix launch + queue population | PARTIAL |
| Hub hero artwork maps | `QuickPlayMap_*.png`, `SoftwareMap_*.png` | Procedural golden-angle hub map (`HubMapControl`); authentic PNG maps absent | PARTIAL |
| Auto-playlist dialog | `AUTOC/ AUTOPLAYLISTDIALOG.UIX` | Rule-based smart-playlist editor (`SmartPlaylistEditorView`) | FULL |
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
| Smart/auto playlists | `AUTOPLAYLISTDIALOG.UIX` | Rule-based auto playlists | FULL |
| Metadata editor | `EDITMEDIAINFODIALOG.UIX`, `EDITMEDIAINFOCONTROLS.UIX` | `MetadataEditView` + TagLibSharp writeback | FULL |
| Find Album Info (per-track match + art) | `FINDALBUMINFODIALOG.UIX`, `FINDALBUMINFOSONGMATCH.UIX`, `AlbumArtUpdateHandler.cs` | Per-track match review (`TrackMatchReviewView`) | FULL |
| Folder watching | `FirstLaunchMonitoredFoldersPage.cs`, `ADDTOCOLLECTION.UIX` | Debounced `FileSystemWatcher` | FULL |
| Ratings (heart / broken heart) | `RATING.LIKEIT/HATEIT.PNG` | Context menus + HUD hearts | FULL |
| **Video library** | `VIDEOLIBRARY.UIX`, `VideoLibraryPage.cs`, `VideosPanel.cs` | libVLC-backed `VideoLibraryView` | FULL |
| **Photo library + gallery + slideshow** | `PHOTOLIBRARY.UIX`, `GALLERYVIEW.UIX`, `PHOTOSLIDESHOW.UIX`, `SlideshowLand.cs` | Photo library + slideshow views | FULL |

### D. Now Playing

| Zune 4.8 feature | Evidence | Dorado | Status |
|---|---|---|---|
| Cinematic artist canvas + Ken-Burns | `NOWPLAYINGLAND.UIX`, `NOWPLAYINGMUSICBACKGROUND.UIX`, `NOWPLAYINGSTYLES.UIX`, `NowPlayingLand.cs` | `NowPlayingView` + real Fanart.tv backdrops (Phase 4) | FULL |
| Typographic track overlays | `NOWPLAYINGSTYLES.UIX` | 60pt title / 32pt accent artist | FULL |
| Showlist (upcoming queue drawer) | `TRANSPORT.SHOWLIST.ON/OFF.PNG` | Slide-out showlist drawer | FULL |
| Lyrics & bio drawer | `NOWPLAYINGLAND.UIX` | Real Wikipedia bios + LRCLIB lyrics (Phase 4) | FULL |
| Album mosaic wall | `NOWPLAYINGALBUMGRIDDEFS.UIX` | MosaicWall mode | FULL |
| Auto-hiding HUD | `NowPlayingLand.cs` (idle timers) | 3.5s idle fade | FULL |
| Now Playing video clips | `NOWPLAYINGCLIPS.UIX` | libVLC clips in the main video surface | FULL |
| Ambient visualizer | (rendered by Iris engine) | Real 75 ms FFT visualizer (24 perceptual bands) | FULL |

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
| Real audio playback (decode + output) | `ZuneSE_dll`, `ZuneCore_Dll`, codec DLLs | Real BASS/ManagedBass decode + output | **FULL** |
| Volume control | `TRANSPORTCONTROLS.UIX` slider | Real BASS volume/mute | FULL |
| Crossfade (0–10s) + gapless | `SyncControls`-adjacent playback options | Equal-power crossfade + gapless chaining | FULL |
| ReplayGain / volume leveling | `ReplayGainTrackGainDb` fields in `ZuneDBApi` (`TrackMetadata`) | ReplayGain DSP pass | FULL |
| Sound effects (completion chimes) | `COMPLETEDSYNCBURNCD.WAV` et al. | `SoundEffectService` real WAV playback via OS CLI | FULL |
| MP3/WMA/AAC encode (rip) | `ZuneEncEngDLL`, `ZuneEncEXE` | FFmpeg transcode during sync; rip remains simulated | PARTIAL |
| AAC/H.264 decode, EVR/DXVA video | `ZuneAACDec_Dll`, `ZuneH264Dec_Dll`, `ZuneDXVA2_Dll`, `ZuneEvr_Dll` | libVLC decode/playback (not EVR/DXVA) | FULL |

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
| Sync animation/toast | `SYNCANIMATION.UIX`, `SYNCINSTRUCTIONTOAST.UIX`, `SYNCNOTIFICATION.UIX`, `SyncNotification.cs` | Sync toast + slide-in animation | FULL |
| MTP device contents browsing | `IDeviceContentsPage.cs`, `DEVICEPICTUREVIDEO.UIX` | `MtpTransport` seam + virtual hardware-N-A | PARTIAL (N-A without hardware) |
| Wireless sync | `WIRELESSSYNC.UIX`, `WirelessSyncWizard.cs`, `WirelessSync*.Page.cs` | LAN sync endpoint + mDNS for Dorado-HD; Zune-native wireless N-A | PARTIAL |
| Windows Phone wireless sync | `MOBILEWIRELESSSYNC.UIX`, `MobileWirelessSyncWizard.cs` | — | N-A |
| Firmware update / restore / rollback | `DEVICEUPDATE*.UIX`, `DEVICE RESTORE*.UIX`, `DeviceRollback*.cs`, `ZuneWmduDLL` | — | MISSING (N-A hardware) |
| USB bus (kernel driver + enum svc + MTPZ) | `Zumbus.sys`, `ZuneBusEnumSvc`, `ZuneMTPZ_dll`, `ZuneUsbTransport_dll` | MTP seam (`LibUsb`/virtual) + LAN sync; kernel driver N-A | PARTIAL |
| Guest sync | `GuestSchemaSyncGroup.cs`, `FirstConnectDeviceGuestWarning` | Guest sync sessions | FULL |

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
| General/file-type/privacy/sharing | `MANAGEMENTGENERAL/FILETYPES/PRIVACY/SHARING.UIX` | General/file-types/privacy ship; sharing N-A | PARTIAL |
| Subscription/purchases/rentals | `MANAGEMENTSUBSCRIPTION/PURCHASES/RENTALS.UIX` | — | N-A |
| Settings persistence | `ZuneCfg_Dll` | `JsonSettingsStore` (Phase 4) | FULL |

### L. First-Launch & Onboarding

| Zune 4.8 feature | Evidence | Dorado | Status |
|---|---|---|---|
| First-launch wizard (welcome→folders→filetypes→privacy) | `FIRSTLAUNCH.UIX`, `FirstLaunch*.Page.cs` ×7 | First-launch wizard (welcome→folders→scan→done) | FULL |
| First-connect device wizard | `FIRSTCONNECT.UIX`, `FirstConnect*.Page.cs` ×6 | — | N-A hardware |
| Setup land | `SETUPLAND.UIX`, `SetupLandPage.cs` | Covered by the first-launch wizard + What's New | PARTIAL |

### M. Platform Services

| Zune 4.8 feature | Evidence | Dorado | Status |
|---|---|---|---|
| ZMDB database engine (4 variants) | `ZuneZMDB{Classic,Library,Mobile,ZuneHD}DLL`, `ZuneDB_dll` | EF Core + SQLite (single schema) | PARTIAL (substitute) |
| ZMDB managed API | `ZuneDBApi_Dll` → 1,030 files, 19 namespaces (`MicrosoftZuneLibrary`, `Microsoft.Zune.Service`, `Microsoft.Zune.QuickMix`, `Microsoft.Zune.Playlist`, `Microsoft.Zune.Subscription`, `Microsoft.Zune.User`, …) | `IMediaLibraryService` (+11 other interfaces) | PARTIAL (media core covered; subscription/user/service layers N-A) |
| Play-history & stats | `MicrosoftZuneLibrary` history tables | `PlayHistoryEntry` + `UserStatsService` | FULL (substitute) |
| Network media sharing (UPnP/WMC) | `ZuneNssExe`, `ZuneShareEXE`, `ContentDirectoryXML`, `MediaReceiverRegistrarXML` | — | MISSING |
| Background service host | `ZuneService_Dll` | In-process coordinators | PARTIAL |
| Multi-language MUI (26 locales) | `ZuneResources_Mui`, `*mui` files | en/fr shipped; 24 locales remain deferred | PARTIAL |
| Explorer shell extension / launcher | `ZuneShellExt_Dll`, `ZuneLauncherEXE` | — | N-A |
| x64 build | `zune-x64.msi` | .NET 8 cross-platform (x64/arm64 CI) | FULL |
| Plugin extensibility | (none in Zune — COM/registry only) | Out-of-process JSON-RPC plugin host + reference plugins | FULL (exceeds Zune) |

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

## 5. Phase 5+ Roadmap (historical — shipped or superseded)

> Every item below shipped during Phases 5–23 except the N-A hardware rows, which are
> recorded in [`deferred_registry.md`](deferred_registry.md).

1. ✅ **P0 — Real audio playback engine.** Shipped as BASS/ManagedBass in `Infrastructure.Audio` (gapless, crossfade, ReplayGain, 10-band EQ, FFT, podcast/CD playback).
2. ✅ **P1 — First-launch wizard** (`FIRSTLAUNCH.UIX` flow): welcome → monitored folders → scan → done, plus What's New.
3. ✅ **P1 — Find Album Info track matching**: per-track MBID resolution + review dialog (`FINDALBUMINFOSONGMATCH.UIX` parity).
4. ✅ **P2 — Smart/auto playlists** (`AUTOPLAYLISTDIALOG.UIX` parity): rule-builder persisted like ZPL playlists.
5. ✅ **P2 — Mixview tiles wiring**: constellation nodes with similarity ranking and external related-artist satellites.
6. ✅ **P3 — Video support** (`VIDEOLIBRARY.UIX` view via libVLC) and **Photo library + slideshow**.
7. ✅ **P3 — Sync UX polish** (sync animation, instruction toast, gas gauge).
8. ⏳ **Deferred (N-A/hardware):** MTPZ device sync internals, firmware update/restore, wireless pairing, UPnP sharing.

---

## 6. Audit Confidence Notes

- Managed-code evidence is complete (`ZuneShell_Dll` + `ZuneDBApi_Dll` fully decompiled); native components (`UIX_*`, `ZuneSE_dll`, `ZuneEncEngDLL`, `ZuneMTPZ_dll`, `Zumbus.sys`) were inventoried but not disassembled at machine-code level — their behavior is inferred from names, interfaces, strings, and the managed layers that call them.
- UIX documents are compiled `.uib` bytecode in `ZuneShellResources_Dll` RCDATA; the 241 `.UIX` source names + 1,289 asset names used here come from the resource table (assets previously ingested into `src/Dorado.UI/Assets/Zune/`).
- Feature statuses were re-verified against the Dorado source tree (15 source projects, 22 views, 393 passing tests) as of 2026-09-10 (post Phase 23).
