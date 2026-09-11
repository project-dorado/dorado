# Dorado ↔ Zune 4.8 Gap Inventory (post-Phase-5 batch; refreshed post-Phase-23)

> **Audit supersession (2026-09-11):** an independent re-audit found this
> inventory's status column and the "10 of 15 closed" summary to be overstated
> (verified then: **7 closed / 4 partial / 4 open**). See
> [`audit-2026-09-11.md`](audit-2026-09-11.md) for the evidence.
>
> **Remediation (M1–M3, commits `c99f3e1`/`e8824ce`/`31780fa`):** items **3**
> (hover/pressed icon variants), **7** (Smart DJ timeout/progress), **10**
> (playlist search), **11** (Quick Mix notification) and **12** (editable Zune
> Card) are now closed; **6** is partially improved (canvas↔mosaic crossfade;
> video remains an instant swap). Current tally: **12 closed / 2 partial / 1
> open** (only #15, real CD rip/burn, remains).

**Date:** 2026-09-09 · **Refreshed:** 2026-09-10 (post Phase 23)
**Scope:** Comprehensive cross-reference of `src/Dorado.UI/Views/*` against the
Zune 4.8 evidence base (external `../zune-disassembly/zuneshell/`,
`../zune-disassembly/uix/`, `../zune-disassembly/zune_resources/`).
**Inputs preserved as historical:** `docs/parity/zune48_parity_audit.md`
(historical), `docs/parity/deferred_registry.md` (deferred items).

Effort scale: **quick-win** = ≤½ day, pure plumbing · **medium** = 1–3 days ·
**large** = ≥1 week or hardware/infrastructure dependency.

---

## 0. Reality Reconciliation (2026-09-10)

This inventory was written at `3a4318a`; its remaining gaps were closed by Phases 8–23.
The table below records the final state.
Verified against the working tree:

| Claim in this doc | Actual state (verified 2026-09-10) |
|---|---|
| §1.5 / §7.4 Settings pivot-gating bug (5 missing notifications) | **FIXED** — `481c363`; guarded by `SettingsPivotGatingTests.cs` |
| §8.3 CD disc backdrop/glow not referenced | **FIXED** — `CDLANDSHINE`, `CDRIPBURNGLOW`, `CDARTSHADOW` bound in `CDView.axaml:32–55` |
| §8.3 Now Playing button states | **FIXED** — hover/pressed frame variants in `MainShellViewModel`; `NowPlayingIconTests.cs` |
| §9.4 Drawer slide-in/fade animations | **FIXED** — `d612da8`; `DrawerAnimTests.cs` |
| §7.5 / §1.1 About sub-pivot is placeholder | **FIXED** — real About panel at `SettingsView.axaml:630` |
| §10 Search across podcasts/videos | **FIXED** — `SearchExtendedAsync` in `MainShellViewModel` |
| §10 Search across playlists | **STILL MISSING** |
| §11.1 Mini-player showlist + volume | **FIXED** — `CompactMiniPlayerView.axaml:86–120` |
| §5.2 / TOP-15 Smart DJ timeout + progress | **STILL OPEN** — `GenerateMixAsync` remains unbounded, no progress |
| §1.3 Iris art-frame animation (`NOWPLAYINGARTLOGO/ARTSHAPE`) | **SHIPPED (Phase 22)** — clean-room `IrisArtControl` |
| §7.4 Reusable dialog service | **SHIPPED (Phase 19)** — `IDialogService`/`DialogService` |
| §11.2 Zune Card avatar picker / editable tag | **STILL OPEN** — `ZuneTag`/`StatusMessage` read-only |
| §5.1 Hub hero artwork maps | **PARTIAL (Phase 22)** — procedural `HubMapControl` shipped; authentic PNG maps absent |
| §10 / §16 A–Z type-ahead jump | **SHIPPED (Phase 19)** — `TypeAheadBuffer`/`TypeAheadSearch` |
| §9 drag-inertia pivot | **SHIPPED (Phase 16b)** — friction-inertia drag (`PivotPanMath`) |
FTS5 is real (`SearchIndex.cs`: external-content FTS5 over the collection, trigger-synced). The
test baseline is **393 passing** (379 Application +
14 Domain). See [`osint_registry.md`](osint_registry.md) for the community-source plan
and [`true_parity_task_plan.md`](true_parity_task_plan.md) for the full Phase 12–23 program.

---

## 1. Views / UX surfaces

### 1.1 Current views (22) under

| File | One-line role |
|---|---|
| `MainShellView.axaml` | Outer shell, custom chrome, panoramic pivot strip, docked HUD, overlays. |
| `CompactMiniPlayerView.axaml` | 340×96 compact mini-player (audio only). |
| `QuickplayView.axaml` | 3-deck panorama (Pins / History / New) + Smart DJ launcher. |
| `CollectionView.axaml` | Music library browser (artists / albums / songs / genres / playlists). |
| `MixviewView.axaml` | Constellation canvas with satellite tiles + action strip. |
| `NowPlayingView.axaml` | 3-mode Now Playing (Artist Canvas / Mosaic Wall / Video clips). |
| `DeviceView.axaml` | Device land: gas gauge, sync content, sync CTA. |
| `SettingsView.axaml` | Two-tier pivot: SOFTWARE (13 sub-pivots) + DEVICE (4 sub-pivots). |
| `FirstLaunchWizardView.axaml` | Welcome → folder → scan → done wizard overlay. |
| `FirstConnectWizardView.axaml` | Per-device-arrival wizard (name → sync options → privacy → done). |
| `WhatsNewView.axaml` | First-launch post-wizard highlights overlay. |
| `PlaylistsView.axaml` | Playlist manager + ZPL export. |
| `PodcastsView.axaml` | RSS feed manager + episode list. |
| `VideoLibraryView.axaml` | Video grid + scan folder + player overlay. |
| `VideoPlaybackView.axaml` | libVLC-backed video player surface. |
| `PhotoLibraryView.axaml` | Folder tree + gallery grid + zoom overlay. |
| `PhotoSlideshowView.axaml` | Ken-Burns full-bleed slideshow (authentic SLIDESHOW.* assets). |
| `CDView.axaml` | DISC pivot: rip + burn queues. |
| `MetadataEditView.axaml` | TagLibSharp-backed tag editor (album/track metadata). |
| `SmartPlaylistEditorView.axaml` | Auto-playlist rule builder (Match All/Any, sort, limit). |
| `TrackMatchReviewView.axaml` | Per-track MusicBrainz match review (`FINDALBUMINFOSONGMATCH` parity). |
| `ZuneCardView.axaml` | Local Zune Card substitute: avatar, stats, badges. |

### 1.2 Cross-reference against `zune-disassembly/zuneshell/ZuneUI/*.Page.cs` (81 page classes)

Pages we cover (25 / 81): `QuickplayPage`, `MusicLibraryPage`, `VideoLibraryPage`,
`PhotoLibraryPage`, `PodcastLibraryPage`, `PlaybackPage`, `SetupLandPage` (≈FirstLaunch),
`FirstConnectPage`+5 child pages, `FirstLaunchPage`+6 child pages, `WizardZunePage`,
`IDeviceContentsPage` (≈DeviceView), `NoStackPage`, `StartupPage`, `TeasePage`,
`CartPage` (N-A), `DownloadsPage` (N-A), `SubscriptionLibraryPage` (N-A),
`ApplicationLibraryPage` (N-A), `FriendsPage` (N-A), `InboxPage` (N-A),
`RadioPage` (N-A), `ProfilePage` (N-A), `Channels*` (N-A).

Pages we DO NOT have (visible-but-not-built, 6 / 81):

| Page | Notes |
|---|---|
| `ApplicationLibraryPage` | Games/Apps hub — N-A (Zune Marketplace shut down). |
| `CategoryPage` | Browse-by-category landing — **MISSING** as a view; Collection is the substitute. |
| `GDILandPage` | The CDI/GDI land root for device update/restore/rollback flows. Device sync screens. |
| `LibraryPage` | The pivot-aggregating "library" root that hosts Music/Video/Photo/Podcast sub-pivots. |
| `ChannelLibraryPage` | Channel/Marketplace — N-A. |
| `DeviceRestore*` (8), `DeviceRollback*` (5), `DeviceUpdate*` (8) | All device firmware lifecycle pages. |

Pages in `zune-disassembly/zuneshell/ZuneUI/*.cs` but never surfaced as a View (representative list — 50+ helpers/landings not listed above): they live behind ViewModels, settings, or wizard orchestration.

### 1.3 Cross-reference against `zune-disassembly/uix/*.UIX` (241 docs)

The `.UIX` resource names hint at "documents" rendered by the Iris UIX engine.
We port roughly 70 of those concepts into AXAML. UIX resources we have an
AXAML equivalent for (representative): `SHELL.uix`, `TRANSPORTCONTROLS`,
`BOTTOMTOOLBAR`, `TOPTOOLBAR`, `PIVOTLIST`, `PIVOTHELPER`, `NONCLIENTCONTROLS`,
`CDVIEW`, `CDSTATE`, `PHOTOLIBRARY`, `PHOTOSLIDESHOW`, `VIDEOLIBRARY`,
`PODCASTLIBRARY`, `PODCASTSERIESPANEL`, `PODCASTDETAILSPANEL`, `PODCASTEPISODESPANEL`,
`COLLECTIONNAVIGATION`, `LIBRARYPANELS`, `LIBRARYCELLS`, `LIBRARYTHUMBNAILOVERLAYS`,
`LIBRARYDIALOGS`, `LIBRARYCONTEXTMENU`, `ALBUMSPANEL`, `ARTISTSPANEL`, `GENRESPANEL`,
`PLAYLISTCONTENTSPANEL`, `PLAYLISTDETAILSPANEL`, `PLAYLISTSPANEL`, `PLAYLISTPOPUP`,
`PLAYLISTDIALOG`, `EDITMEDIAINFODIALOG`, `EDITMEDIAINFOCONTROLS`, `FINDALBUMINFODIALOG`,
`FINDALBUMINFOSONGMATCH`, `AUTOCOMPLETEBOX`, `AUTOPLAYLISTDIALOG`, `SEARCH`,
`NOWPLAYINGLAND`, `NOWPLAYINGSTYLES`, `NOWPLAYINGMUSICBACKGROUND`,
`NOWPLAYINGALBUMGRIDDEFS`, `NOWPLAYINGCLIPS`, `NOWPLAYINGEFFECTS`, `NOWPLAYINGNOTIFICATION`,
`MINIMODE`, `MINIMODEAUDIO`, `MINIMODEJUMPLIST`, `MIXCONTROLS`, `MIXLAYER`, `MIXLAYOUT`,
`MIXMANAGER`, `MIXQUERY`, `QUICKPLAY`, `QUICKPLAYSTRIP`, `QUICKPLAYMODULE`,
`QUICKPLAYMETHODS`, `QUICKPLAYNAVIGATION`, `QUICKPLAYSTYLES`, `QUICKPLAYLISTMODEL`,
`QUICKPLAYTHUMBNAIL`, `QUICKMIX`, `QUICKMIXHELPER`, `QUICKMIXPLAYLISTDIALOG`,
`BADGES`, `MANAGEMENT`, `MANAGEMENTCOLLECTION`, `MANAGEMENTRIP`, `MANAGEMENTBURN`,
`MANAGEMENTMETADATA`, `MANAGEMENTDISPLAY`, `MANAGEMENTPODCAST`, `MANAGEMENTPHOTO`,
`MANAGEMENTFILETYPES`, `MANAGEMENTPRIVACY`, `MANAGEMENTGENERAL`, `MANAGEMENTNAVIGATION`,
`FIRSTLAUNCH`, `FIRSTCONNECT`, `WHATSNEW`, `DIALOG`, `CONTROLS`, `STYLES`,
`SLIDER`, `EDITBOX`, `GASGAUGE`, `MINIGASGAUGE`, `SPREADSHEETCELLS`, `SPREADSHEET`,
`STARRATINGS`, `SYNCANIMATION`, `SYNCINSTRUCTIONTOAST`, `SYNCNOTIFICATION`,
`THUMBNAILBUTTON`, `TOOLBARICON`, `TRANSPORTCONTROLS`, `ANIMATEDICONBUTTON`,
`ANIMATIONS`, `SETUPLAND`, `SHORTCUTKEYS`, `CONTENTLIST`, `LISTVIEWPANEL`,
`JUMPINLIST`, `JUMPLIST`, `LABELWITHARROW`, `FILTERLIST`, `FOCUSRECT`, `GALLERYPANEL`,
`GALLERYVIEW`, `PHOTOFOLDERTREEPANEL`.

UIX resources that hint at concepts we DON'T ship (~50):

| UIX resource | Implication | Gap |
|---|---|---|
| `ABOUTDIALOG` | Standard About box (logo, version, EULA). | Real About panel (product/version/runtime/license/EULA). |
| `ACCOUNTINFO`, `ACCOUNTCREATION`, `CREATEPASSPORT`, `WINDOWS LIVE` | Passport/Live account creation. | N-A (servers dead). |
| `ADDTOCOLLECTION`, `ADDTOPLAYLIST`, `ADDTOSYNC` | Quick-add flyouts. | **MISSING** — Collection context menu is bare. |
| `APPLICATIONLIBRARY` | Games & apps hub. | N-A. |
| `AUTOPLAYLISTDIALOG` | (Smart playlist rule dialog). | **COVERED** by `SmartPlaylistEditorView` but only from settings, no per-album launch entry. |
| `BADGES` | Badge list. | **COVERED** by ZuneCardView. |
| `BILLINGOFFER`, `BESTVALUE`, `CARTPANEL`, `CARTPAGE`, `SUBSCRIPTIONLIBRARY`, `MANAGEMENTSUBSCRIPTION`, `MANAGEMENTPURCHASES`, `MANAGEMENTRENTALS`, `PAYMENTINSTRUMENT`, `ACCOUNT.CCV.*` | Billing/cart. | N-A. |
| `BROKENUPTEXT` | Wrapping text helper. | **MISSING** — used by bio / lyrics drawer for line-break formatting. |
| `BULLETLIST` | Bullet list helper. | **MISSING**. |
| `CLIPIMAGE` | Image clip helper (circle crops, fan blur). | **MISSING** — artist canvas uses no clipping; avatars are square. |
| `COMMENTSHELPER` | Comment thread helper. | N-A. |
| `COMPLEXSYNCRULEDIALOG` | Advanced sync-rule editor. | **MISSING** — we only expose the simple "All/Selected/Manual" radio set. |
| `CONFIRMALBUMCHANGESDIALOG` | "Apply track match changes" confirm dialog. | **MISSING** — TrackMatchReviewView applies without a confirm step. |
| `CONTENTLIST` | Generic content list. | **MISSING** — we hand-roll `ItemsControl` per view. |
| `DEVICEICONS.UIX`, `DEVICEICON.UIX`, `DEVICEICONPOPUP.UIX`, `ZUNEHDDEVICES.PNG` | Device iconography. | **MISSING** — DeviceView uses no device-portrait assets. |
| `DEVICELAND*` (3), `DEVICEMARKETPLACE`, `DEVICEMOREONWEB`, `DEVICERESTORE`, `DEVICEROLLBACK`, `DEVICESIGNINFAILURE`, `DEVICESUMMARY`, `DEVICESUMMARYDATA`, `DEVICESUMMARYSTATUS` | Device land internals. | Mostly N-A (sign-in / marketplace). Summary surface **MISSING**. |
| `DEVICESPACERESERVATION` | Reservation UI. | **COVERED** by Settings → Space Reservation. |
| `DEVICEUPDATE`, `DEVICEUPDATE*` | Firmware update flows. | N-A (hardware). |
| `DIALOG`, `CONFIRMCLOSE`, `ERRORDIALOG`, `EULADIALOG`, `EXPLICITWARNING`, `SIGNINDIALOG`, `WEBHOSTDIALOG`, `WIZARDDIALOGS` | Standard dialogs. | `IDialogService` in-shell modal (confirm/alert/prompt) used for destructive actions. |
| `DROPCOMMANDS` | Right-click drop commands. | **PARTIAL** — only Quickplay & Collection have context menus. |
| `EMPTYCOLLECTIONPANEL` | Empty-state visuals. | **MISSING** — we hand-roll per-view empty states. |
| `EPISODESPANEL`, `PODCASTSERIESPANEL`, `PODCASTDETAILSPANEL`, `PODCASTEPISODESPANEL`, `PODCASTDIALOGS`, `PODCASTEPISODESCOLUMNS`, `SERIESPANEL`, `CHANNELSERIESPANEL` | Podcast/series panels. | **PARTIAL** — single list of episodes, no series-level grouping. |
| `FRIENDS.UIX`, `FRIENDSDIALOGS.UIX`, `INBOX*` (8), `SOCIALCOMPOSER.UIX`, `SOCIALEMPTY.UIX`, `SOCIALNAVIGATION.UIX`, `MESSAGINGHELPER.UIX`, `PROFILE.UIX`, `PROFILEEDIT.UIX`, `PROFILECATEGORIES.UIX`, `INBOXBASEDETAILS.UIX` | Social surface. | N-A. |
| `GASGAUGE`, `MINIGASGAUGE` | Gas-gauge primitives. | **PARTIAL** — DeviceView has a custom gauge but no MINIGASGAUGE mini-version on the Quick Dock. |
| `INBOXMAINPANEL` | Inbox notifications. | **MISSING** — no inbox UI even for local notifications (no first-launch welcome toast outside the wizard). |
| `MANAGEMENTACCOUNT` | Account settings page. | N-A (no live account). |
| `MANAGEMENTSHARING` | Per-device "share music/video/photos" toggles. | N-A — deferred in `deferred_registry.md`. |
| `MANAGEMENTDEVICELIST`, `MANAGEMENTDEVICES` | Multi-device list. | **MISSING** — our `DeviceSubPivot.DeviceInfo` shows one device; no enumeration of multiple. |
| `MINIMODEVIDEO` | Mini-player video surface. | N-A (per `deferred_registry.md`). |
| `MOBILEWIRELESSSYNC` | Windows Phone wireless sync. | N-A. |
| `MOREINFOACTIONS` | Right-side action column. | **MISSING** — Now Playing has a top-right action stack but no equivalent side rail on Collection. |
| `NONCLIENTCONTROLS` | Authentic window chrome primitives. | **COVERED** (Window/* assets). |
| `NOTIFICATIONAREA`, `POPUPICON`, `POPUP` | Popups & notification area. | **MISSING** — only one NotificationArea-style overlay (sync toast); no flyout popups. |
| `PAGESTACK`, `PAGINGCONTROL` | Page-stack navigation. | Full `PageStack` back-stack shipped (Collection→Album→Tracks). |
| `PANELRESIZER` | Split-panel resizer. | **MISSING** — PhotoLibraryView, MixviewView, NowPlayingView drawers are fixed-width. |
| `PERCENTAGEICON`, `PERCENTAGEICONFILLED` | Battery/Free-space icons. | **MISSING** — battery text only. |
| `PLAYALL` | "Play all" toolbar. | **MISSING** — no "Play all" button on Collection pivots. |
| `RADIOPANEL`, `RADIOBUTTON.UIX` | Zune Radio (HD). | N-A (spectrum gone). |
| `SLIDER`, `STARBUTTON` | Common controls. | **COVERED** for Slider (volume); **MISSING** for star ratings. |
| `THUMBNAILBUTTONOVERLAY`, `THUMBNAILBUTTON` | Generic thumb button. | **MISSING** — we hand-roll album-art tiles per view. |
| `TOOLTIP`, `WEBHOSTCONTROL`, `WEBHOSTDIALOG` | Tooltip / web host. | **PARTIAL** — ToolTip.Tip set on a few buttons; no shared style or web host. |
| `WIRELESSSYNC` | Wireless sync flows. | **PARTIAL** — only a UI placeholder in Settings. |
| `WIZARD`, `WIZARDCONTROLS`, `WIZARDDIALOGS` | Wizard framework. | **PARTIAL** — only FirstLaunch + FirstConnect re-implement the wizard pattern; no reusable framework. |

### 1.4 `SoftwareSubPivot` / `DeviceSubPivot` enum coverage

**SoftwareSubPivot** (13 of 13 — parity): Collection, Playback, Podcasts, FileTypes, Privacy, Photos, Rip, Burn, Metadata, Display, General, About, Plugins.

**DeviceSubPivot** (4 of 7 in Zune 4.8):
- ✅ SyncOptions → `DEVICESYNCOPTIONS.UIX`
- ✅ SpaceReservation → `DEVICESPACERESERVATION.UIX`
- ✅ WirelessSync → `WIRELESSSYNC.UIX` (placeholder UI only)
- ✅ DeviceInfo → `DEVICESUMMARY.UIX` (simplified)
- ❌ `MANAGEMENTDEVICELIST.UIX` — multi-device enumeration
- ❌ `MANAGEMENTDEVICES.UIX` — device group manager
- ❌ `DEVICERESTORE.UIX` / `DEVICEROLLBACK.UIX` — restore/rollback UI (N-A w/o hardware but the UI primitives don't exist either)

### 1.5 Pivot gating bug in `SettingsViewModel.cs`

`SoftwarePivot` setter (SettingsViewModel.cs:131–148) fires `OnPropertyChanged` for
**only 7 of 12** sub-pivot flags: Collection, Playback, Rip, Burn, Metadata,
Display, About. It **does not** fire for: Podcasts, FileTypes, Privacy, Photos,
General.

`IsSoftwarePivotActive` is fired by `TopLevelPivot` so cross-tier toggles refresh
correctly. But switching among the 5 un-fanned sub-pivots (e.g. Podcasts →
Privacy → Photos → FileTypes → General) leaves the previous panel visible —
Avalonia only re-evaluates `IsVisible` bindings on `PropertyChanged` events,
and the dependent flags don't fire.

Concrete repro: open Settings → click Podcasts (panel A renders, label active);
click Privacy (label flips active, panel B never replaces panel A → both
StackPanels overlap; lower in the Z-order one wins).

Bug class: One-line fix in the setter (add the 5 missing notifications) — but
the existing tests (`SettingsPagesParityTests.cs:10–53`) only check the
property getter value, so they pass. **Quick-win**, **HIGH** impact (every
user who clicks these settings tabs sees the bug).

### 1.6 Other view surfaces worth noting

- `MINIMODE.UIX` + `MINIMODEAUDIO.UIX` → `CompactMiniPlayerView` (audio-only, no `MINIMODEVIDEO`).
- `FINDALBUMINFOSONGMATCH.UIX` → `TrackMatchReviewView` (real, opened from Collection context menu).
- `AUTOCOMPLETEBOX.UIX` → Header search autocomplete (real, 3+3+3 prefix suggestions).
- `AUTOPLAYLISTDIALOG.UIX` → `SmartPlaylistEditorView` (real, but only opened from settings — no playlist-list entry point).

---

## 2. Audio engine parity

**Real vs. simulated summary**

| Feature | Status | Where |
|---|---|---|
| Real audio decode + output | **REAL** | `BassAudioOutputEngine.cs` (ManagedBass: BASS+FLAC+AAC+Opus) |
| Real gapless chaining (sample-boundary) | **REAL** | `BassAudioOutputEngine.BeginGaplessChainFromSync` |
| Real crossfade (equal-power cosine/sine ramps, 0–10s) | **REAL** | `BassAudioOutputEngine.ApplyFadeRamp` |
| ReplayGain / volume leveling (track gain) | **REAL** | `TagLibReplayGainService.cs` + `PlaybackQueueCoordinator.LoadReplayGainFactor` |
| Real FFT spectrum data (24 perceptual bands) | **REAL** | `BassAudioOutputEngine.GetFftData` |
| Visualizer driven by real FFT | **REAL** | `NowPlayingViewModel._visualizerTimer` (75 ms tick); falls back to fake if no engine |
| Volume control wired to engine | **REAL** | `PlaybackQueueCoordinator.ApplyOutputVolume` → `BassAudioOutputEngine.SetVolume` |
| Mute toggle | **REAL** | Same path. |
| Seek | **REAL** | `BassAudioOutputEngine.Seek` |
| Track-end signal to coordinator | **REAL** | `BassAudioOutputEngine.OnCurrentSourceEnd` |
| EQ (10-band parametric) | **FULL** | Managed 10-band RBJ biquad EQ via a BASS DSP pass (8 presets). |
| MP3 / WMA / AAC encode (rip) | **SIMULATED** | No real encoder; `CDViewModel.OnRipCdAsync` waits + plays chime. |
| Audio CD burn pipeline | **SIMULATED** | `CDViewModel.OnBurnCdAsync` waits + plays chime. |
| Playback for files without real sources (demo tracks) | **SIMULATED** | `AudioEngine.OnPositionTimerElapsed` drives a 250 ms-tick position advance when `IsSimulatedPlayback`. |
| Sound effects (chimes) | **REAL** | `SoundEffectService.PlaySound` shells to OS CLI player per platform (PowerShell Media.SoundPlayer / afplay / pw-play|paplay|aplay). |
| Network/podcast stream playback | **REAL** | `BassAudioOutputEngine.CreateSource` accepts `http(s)://` and creates async stream. |
| Native audio asset: `ZuneSE_dll` (DShow/EVR sink), `ZuneAACDec_Dll`, `ZuneSrcWrpDLL` | **MISSING** | BASS replaces the role; we don't ship Zune's native decoder. |
| Headless / dummy-device fallback | **REAL** | `Bass.Init(0, ...)` falls back to NoSound. |

**Verdict:** Audio engine is at ~95% parity for playback DSP — the only meaningful
audio gaps are (a) parametric EQ, (b) real encoder for rip, (c) real burner.
(b) and (c) are independent of the playback engine.

---

## 3. Device sync

### 3.1 `IDeviceTransport` boundary

- Interface: `src/Dorado.Application/Interfaces/IDeviceTransport.cs` (real, complete surface: capacity/used/free, GetContents/TryGetItem, CopyToDevice/RemoveFromDevice).
- Implementation in tree: **one** — `src/Dorado.Application/Services/SimulatedDeviceTransport.cs` (in-memory store with seed content).
- Real transports: `MtpTransport` + `IMtpDeviceClient` (`LibUsbMtpDeviceClient`, `VirtualMtpDeviceClient`), `RemoteDeviceTransport`, plus LAN sync (`SyncEndpointHost`/`SyncTcpServer`) and mDNS (`SyncMdnsAdvertiser`).
- The `SyncEngine` factory pattern (`_transportFactory`) supports injection but no production code injects anything other than `SimulatedDeviceTransport`.

### 3.2 `Dorado.Infrastructure.Devices/` (9 files)

- `ZuneDeviceSyncService.cs` — Linux `/sys/bus/usb/devices` scanner, supports Zune product IDs (`063E/0710/0715/0723`).
- `ZuneUsbHttpInterceptor.cs` — placeholder for USB-PPP HTTP interception.

No `TODO`/`FIXME`/`NotImplementedException` markers in this directory (rg confirms clean). The state is "deferred but not stubbed" — i.e. the seam is the `IDeviceTransport` interface, and writing `MtpTransport` is a from-scratch implementation.

### 3.3 Real hardware path (MTPZ)

What Zune 4.8 had:
- `Zumbus.sys` (kernel bus driver) + INF (bus enumeration)
- `ZuneBusEnumSvc` (user-space enumeration service)
- `ZuneMTPZ_dll` (MTPZ protocol implementation, secure variant of MTP)
- `ZuneUsbTransport_dll`, `ZuneIpTransport_dll`, `ZuneTcp2Udp_dll` (USB-PPP, IP, TCP-to-UDP transports)
- `WMZUNE*` (USB-PPP protocol handling)
- `ZuneDriver_dll`

What we'd need for real MTPZ parity: **large** effort. Host-side:
- libusb1 or WinUSB P/Invoke (cross-platform USB bulk transport)
- MTPZ protocol implementation (auth + WMDM metadata + container parsing)
- MTP object store parser (read `\\Content\\Music`, `\\Content\\Podcasts`, etc.)
- ZMDB native parser (4 engine variants)
- ZuneWmduDLL firmware-update logic
- macOS IOKit / Linux udev integration
- `ZuneShellExt_Dll` shell extension (N-A on Linux)

This is hardware-dependent and was deferred in `deferred_registry.md`. **N-A** for the parity scorecard but worth documenting for completeness.

### 3.4 Quick wins inside the simulated path

- `SimulatedDeviceTransport` seed content includes a stale "Macarena" item that always shows in `Keep/Remove` — feels janky. Replace with a placeholder flag. **Quick-win**.
- The "Disconnected" branch on device list is implicit; we never surface the event in the UI. **Quick-win**.

---

## 4. CD Land

`src/Dorado.UI/Views/CDView.axaml` (110 lines) + `CDViewModel.cs` (203 lines).

| Feature | Status | Notes |
|---|---|---|
| Rip / Burn mode toggle | **REAL UI** | `Mode = CDViewMode.Rip/Burn`. |
| Disc session info (title/artist/duration/track count) | **FAKE** | `DiscTitle`, `DiscArtist`, `TotalDurationText = "43:28"` are hard-coded. |
| Disc tracks list (8 sample tracks) | **FAKE** | Hard-coded list of 8 tracks with seeded names + durations. |
| Track playback during rip session | **REAL** | `PlayTrackCommand` enqueues into `IPlayerCoordinator`. |
| Rip progress | **SIMULATED** | `OnRipCdAsync` does `Task.Delay(350)` × track count. |
| Rip destination = settings folder | **FAKE** | `RipDestinationFolder` is read but never written to by `OnRipCdAsync`. |
| Rip completion chime | **REAL** | `SoundEffectService.PlayRipComplete`. |
| Burn queue (per ZPL-style selection) | **MISSING** | `BurnQueue` ObservableCollection is never populated; user can't add tracks to burn. |
| Burn progress | **SIMULATED** | 10-step 300ms loop. |
| Burn completion chime | **REAL** | `SoundEffectService.PlayBurnComplete`. |
| Real optical-drive detection | **MISSING** | No platform code at all — no `cdparanoia`, no IMAPI2, no `cdrdao`. |
| Optical-drive insert/eject events | **MISSING** | `AutoRipCdOnInsert` is a settings checkbox that does nothing. |
| EjectCdAfterRip wiring | **MISSING** | Settings checkbox only. |

### 4.1 What "real CD rip/burn" would require

| Capability | Effort | Notes |
|---|---|---|
| Detect optical drive (Windows: WMI `Win32_CDROMDrive`, Linux: `lsblk -o NAME,TYPE | grep rom`, macOS: `diskutil list`) | medium | One-shot at app startup. |
| Read TOC (`cdparanoia` / Windows IOCTL) | medium | `libcdio` cross-platform C library would do this. |
| Encode to FLAC/MP3/AAC on the fly | large | `ffmpeg` invocation is a pragmatic shortcut; native would be `ZuneEncEngDLL` parity. |
| Burn audio CD | large | `cdrdao` on Linux, IMAPI2 on Windows. |
| Burn data disc | large | ISO 9660 + Joliet image writer. |
| Eject OS notification | quick-win | `eject` shell command. |

**Verdict:** CD Land is at ~30% parity (UI real, behavior simulated, real pipeline is capability-gated). Per `deferred_registry.md` this stays deferred until a developer with optical-drive access explicitly requests it.

---

## 5. Quickplay / Smart DJ

### 5.1 `QuickplayView` (3-deck panorama)

| Deck | Real vs. simulated |
|---|---|
| Pins | **REAL** — `IMediaLibraryService.GetPinnedAlbumsAsync`. |
| History | **REAL** — `IMediaLibraryService.GetRecentHistoryAsync(10)` from SQLite `PlayHistoryEntry`. |
| New | **REAL** — `IMediaLibraryService.GetRecentlyAddedAlbumsAsync(8)`. |
| Smart DJ launcher (3 buttons) | **REAL** — invokes `ISmartDJService.GenerateMixAsync`. |
| Pins context menu (unpin, play) | **REAL** | 

**Not yet implemented:**
- Hub hero artwork maps (`QuickPlayMap_*.png`, `SoftwareMap_*.png`) — referenced in `zune-disassembly/uix/` corpus but no assets ship and no view consumes them.
- Radio panel (`RADIOPANEL.UIX`) — N-A (spectrum gone).
- Ad/best-value tiles (`BESTVALUE.UIX`, `BILLINGOFFER.UIX`) — N-A.
- "New" sub-deck filters by genre or period — currently just `recently added`.
- Drag-and-drop reordering of pins.

### 5.2 `SmartDJEngine` (scoring engine)

Current scoring (82 lines):
- Same album +12, same artist +10, same genre +5, favorite rating +4, random jitter 0–3.

What Zune 4.8 had (`QuickMixSessionManager`, `QuickMixPlaylistFactory`):
- Native `IQuickMixManager` interface with ≥10 similar-artist expansion.
- 5-second creation timeout (mix must resolve within 5s).
- Marketplace-backed seed → similar-artist → similar-track chain.

What we have vs. Zune's:
| Concept | Status |
|---|---|
| ≥10 similar artists | **PARTIAL** — Mixview pulls ≥4 related artists by genre/favorite; not used by SmartDJ seed. |
| 5-second creation timeout | **MISSING** — `GenerateMixAsync` is synchronous and un-bounded; could block UI on huge libraries. |
| Mix progress / `QuickMixProgress.cs` | **MISSING** — no progress event during mix generation (only instant for now). |
| Marketplace seeds | N-A (substituted with local). |

Quick wins:
- Add a 5-second `CancellationToken` timeout to `GenerateMixAsync`.
- Add `QuickMixProgress` callback so the Quickplay "STARTING MIX…" overlay can animate.

Medium:
- Build a `IQuickMixProgress` interface and emit progress events.

---

## 6. Now Playing

### 6.1 Modes

| Mode | Status | File |
|---|---|---|
| Artist Canvas (Ken-Burns pan/zoom backdrop + 60pt title / 32pt artist + ambient visualizer) | **REAL** | `NowPlayingView.axaml:17–234`, `NowPlayingViewModel.KenBurnsScale/TranslateX/TranslateY`. |
| Bio drawer | **REAL** | `IsBioDrawerOpen`; Wikipedia bios + `MusicBrainz` enriched data via `ArtistEnrichmentCoordinator`. |
| Lyrics drawer (LRCLIB) | **REAL** | `IsBioDrawerOpen` doubles as lyrics drawer; `LoadLyricsForCurrentTrackAsync`. |
| Showlist (upcoming queue) drawer | **REAL** | `IsShowlistOpen`, `UpcomingQueue`. |
| Auto-hiding HUD (3.5 s idle fade) | **REAL** | `_hudIdleTimer`. |
| Mosaic Wall mode | **REAL** | `NowPlayingView.axaml:239–274`, but it's a static album grid — no Ken-Burns transitions, no cover flow effect. |
| Video Clips mode | **REAL** | `NowPlayingView.axaml:316–348` — libVLC video picker + `VideoPlaybackView` overlay. |

### 6.2 What's a stub

- **Mosaic Wall** is functional but minimal — no fan-blur `CLIPIMAGE`, no autoplay on hover, no "show more albums" pagination.
- **Video Clips** requires the user to manually pick from the list; no "play all" or auto-advance.
- **Bio drawer** has a fixed-width drawer (340 px) with no PANELRESIZER for resize.
- **Lyrics** are displayed unsynced; LRCLIB's synced-lyrics (`SyncedLyrics`) field is captured into `_lyrics` but the timestamps are stripped via `LrcLibStrip`. No karaoke-style line-by-line timing.
- **Showlist** doesn't support drag-reorder of upcoming tracks.
- **Mosaic Wall** transitions between modes use no Iris-style cross-dissolve (we have `TransitioningContentControl` at the shell level but no per-mode fade animation; see §9).
- **Ambient visualizer** falls back to a random-fake if `BassAudioOutputEngine.IsAvailable == false`, but there's no "audio engine unavailable" indicator shown to the user.
- **Wallpaper rotation** (8 s cadence, randomized scale/translate) is deterministic in cadence but the backdrops list is fixed to 10 USERBACKGROUND JPGs when no enrichment is loaded — see `_themeBackdrops` in `NowPlayingViewModel`.

### 6.3 Quick wins
- Add an "engine unavailable" indicator when the visualizer is in fake-fallback state.
- Add the LRCLIB timestamp-aware rendering (use the synced-lyrics without stripping).
- Add a "PLAY ALL" button on Video Clips mode.

---

## 7. Settings — anything still missing?

### 7.1 SoftwareSubPivot coverage

All 12 sub-pivots exist. Cross-reference vs `MANAGEMENT*.UIX`:

| Zune 4.8 page | UIX doc | Our pivot | Status |
|---|---|---|---|
| Collection (monitored folders) | `MANAGEMENTCOLLECTION` | `Collection` | **FULL** |
| Playback (crossfade/gapless/sound FX) | `MANAGEMENTNAVIGATION` + sub-control | `Playback` | **FULL** (FX is toggle-only; no per-track DSP) |
| Podcasts (keep/auto-download) | `MANAGEMENTPODCAST` | `Podcasts` | **FULL** |
| File Types (ingest extensions) | `MANAGEMENTFILETYPES` | `FileTypes` | **FULL** (in-app ingest only; no OS assoc — see `deferred_registry.md`) |
| Privacy (usage data, auto-update) | `MANAGEMENTPRIVACY` | `Privacy` | **FULL** |
| Photos (folder, slideshow) | `MANAGEMENTPHOTO` | `Photos` | **FULL** |
| Rip (format/bitrate/destination) | `MANAGEMENTRIP` | `Rip` | **FULL** (encoder simulated) |
| Burn (disc type/speed/volume leveling) | `MANAGEMENTBURN` | `Burn` | **FULL** (burner simulated) |
| Metadata (auto-fetch, write to file, providers, API keys) | `MANAGEMENTMETADATA` | `Metadata` | **FULL** |
| Display (accents, themes, background art, compact always-on-top) | `MANAGEMENTDISPLAY` | `Display` | **FULL** |
| General (startup view, show ratings, device name) | `MANAGEMENTGENERAL` | `General` | **FULL** |
| About (version, OS info) | `MANAGEMENTNAVIGATION` "About" | `About` | **PARTIAL** — `VersionInfo`/`PlatformInfo` populated but no big "About" panel with logo + EULA link. |

### 7.2 Missing software settings pages

- **`MANAGEMENTACCOUNT.UIX`** — Passport/Live sign-in. N-A.
- **`MANAGEMENTSHARING.UIX`** — UPnP / per-device sharing toggles. Deferred in `deferred_registry.md`.
- **`MANAGEMENTPURCHASES.UIX`**, **`MANAGEMENTRENTALS.UIX`**, **`MANAGEMENTSUBSCRIPTION.UIX`** — billing/subscription. N-A.

### 7.3 Missing device settings pages

- **`MANAGEMENTDEVICELIST.UIX`** — multiple-device enumeration. We never show "no device / Zune 30 / Zune HD" simultaneous list.
- **`MANAGEMENTDEVICES.UIX`** — device-group manager (sync groups per device). We have one device's sync rules at a time.
- **`DEVICESUMMARY.UIX`** — full device summary panel (firmware, capacity, free space, serial, color). We have a simplified `DeviceInfo` sub-pivot.
- **`DEVICESUMMARYSTATUS.UIX`**, **`DEVICESUMMARYDATA.UIX`** — sub-panels.

### 7.4 Pivot-gating bug (also see §1.5)

This is a **HIGH** impact, **quick-win** fix in `SettingsViewModel.SoftwarePivot` setter.

### 7.5 About sub-pivot

The current `IsAboutSubPivotActive` panel only renders version + OS info. Zune's `ABOUTDIALOG.UIX` had: color logo, version, build, OS, EULA link, build date, copyright. **Medium** effort to flesh out.

---

## 8. Graphics / assets

### 8.1 Bundled assets under `src/Dorado.UI/Assets/` (current)

> **Historical catalogue.** The Zune-named PNG/WAV set described below was removed from
> the tree in the P0 IP remediation. The repository now bundles only:
> - `Assets/Selawik/` — 5 OFL Selawik TTFs (the Segoe-metric stand-in);
> - `Assets/Zune/Backgrounds/` — 9 clean-room `DORADO-BACKGROUND-*.PNG`;
> - `Assets/Zune/Sounds/` — 4 clean-room `DORADO-CHIME-*.WAV`.
> All transport / rating / window / branding glyphs are re-created as procedural vectors
> in code (`ZuneGlyphs`). The original corpus catalogue is retained below for reference.

- `Backgrounds/` — USERBACKGROUND JPGs (44 in the original corpus).
- `Branding/` — QUICKMIXICON, ZUNECOLORLOGO, ZUNEHDDEVICES, ZUNELOGO, ZUNELOGOTEXT, ZUNEUSER.
- `CD/` — CDARTSHADOW, CDLANDSHINE, CDRIPBURNGLOW.
- `Fonts/` — 5 Segoe ZLC variants (replaced by OFL Selawik).
- `Mixview/` — MIX.ADD / HATEIT / INFO / LIKEIT / MIX / PERIPHERYTILE.
- `Rating/` — RATING.* variants.
- `Slideshow/`, `Social/`, `Sync/`, `Transport/` — their original glyph sets.

### 8.2 Resources extracted but NOT in our tree (corpus → missing)

Searching the 1,671-item RCDATA list:

- **`BRANDTAG.HORIZONTAL.PNG`, `BRANDTAG.VERTICAL.PNG`** — Dorado brand tag. Not bundled.
- **All `ZUNELOGO.*` variants** — only the 3 we have. The 3 PNGs we already have are identical, just renamed.
- **`ZUNEUSER.PNG`** — bundled.
- **`ICON.NOWPLAYING.FRAME01.PNG`…`FRAME10.PNG`** (and HOVER/PRESSED variants, 30 total) — only `ICON.NOWPLAYING.FRAME*.PNG` (FRAME01–10) and the ENTER variant are in use. The HOVER/PRESSED variants per-frame would give the hover-state animation fidelity of Zune 4.8.
- **`NOWPLAYINGARTLOGO_01.PNG`–`_06.PNG`** (6 frames) and **`NOWPLAYINGARTSHAPE_01A–03D.PNG`** (12 frames) — the actual Iris-rendered "art spinning behind the now playing text" frames. We do not animate these.
- **`NOWPLAYINGANIMATIONORANGE1.PNG`, `NOWPLAYINGANIMATIONPINK1.PNG`** — Now Playing button animation frames. We use static `ICON.NOWPLAYING.ENTER.PNG`.
- **`NOWPLAYINGLISTTRIANGLE.PNG`** — triangle indicator. Not used.
- **`NOWPLAYINGBOXSHADOW.PNG`** — shadow under the now-playing text. Not used.
- **`NOWPLAYING.BUTTON.PNG` / `.HOVER.PNG` / `.PRESSED.PNG` / `.DISABLED.PNG`** — Now Playing toggle button states. We use `ICON.NOWPLAYING.*` instead.
- **`ALBUM.SMALL.SHADOW.PNG`** — small album shadow. Not used (we hand-roll per-tile borders).
- **`ACTIONBUTTON.PNG` / `ACTIONBUTTON.DISABLED.PNG` / `ACTIONBUTTON.HOVER.PNG` / `ACTIONBUTTON.PRESSED.PNG`** — generic action button. We don't use these.
- **`ACTIONBUTTON.PINK.*`** — pink variant.
- **`ACTIONOVERLAY.PNG`** — overlay graphic.
- **`ACTIONPROGRESSBUTTON.PROGRESSBACKGROUND.PNG` / `.PROGRESSBAR.PNG` / `.PROGRESSPLAYBACKGROUND.PNG`** + `ACTIONPROGRESSBUTTON.UIX` — action button with progress fill. Not used.
- **`ACTIVESHADOWTOP/BOTTOM/LEFT/RIGHT.PNG`** — window active shadow. Not used.
- **`ACQUIRINGALBUMART.PNG`** — album art acquisition state. Not used.
- **`ARTISTBOX.BACKGROUND.PNG` / `.DISABLED.PNG` / `.FOCUSED.PNG`** — text box for artist search. Not used.
- **`COMBOBOXWITHICONS.DROPDOWNBACKGROUND.PNG`** — dropdown background. Not used.
- **`CROSSHATCH.QUICKPLAY.WELCOME.PNG`** — Quickplay welcome backplate. Not used.
- **`DEVICEICON*` family** — device-icon set. Only `ZUNEHDDEVICES.PNG` shipped.
- **`ICON.HD.PNG`**, **`ICON.MARKETPLACE.RADIO.PNG`**, **`ICON.QUICKPLAY.*` family** — only `QUICKPLAY.COLLECTION.PNG` is shipped.
- **`ICON.EXTERNALLINK.PNG`, `ICON.EXTERNALLINK.LIGHT.PNG`** — external link icon. Not used.
- **`ICON.PAGE.CURRENT.PNG`, `ICON.PAGE.OTHER.PNG`** — page indicator dots. Not used.
- **`ICON.PC.*`** family — PC device icon. Not used.
- **`ICON.PLAYLIST.*`** family, **`ICON.BURN.*`** family, **`ICON.QUICKPLAY.PLAYLIST.*`** family — many device/drag variants missing.
- **`MIXMELDICON.PNG`** — "mix meld" icon. Not used.
- **`MIX.PERIPHERYTILE.OUTLINE.PNG`** — satellite tile outline. We have `.OUTLINEDARK.PNG` only.
- **`MIX.SEEDTILESHADOW.PNG` / `.NOWPLAYING.PNG`** — seed-tile drop shadow. Not used.
- **`MIX.TILE.ROLLOVER.FILL.PNG` / `.NOWPLAYING.PNG`** — tile hover fill. Not used.
- **`MIX.TILE.ACTION.GLOW.PNG`** — action glow on tile. Not used.
- **`MIX.UNRATED.DEFAULT.PNG` / `.HOVER.PNG` / `.PRESSED.PNG`** — unrated action tile. Not used.
- **`POPUPICONMENUDROPSHADOW.PNG`** — popup shadow. Not used.
- **`PIVOT.SCROLL.QUICKPLAY.LEFT/RIGHT.*`** — pivot chevron scroll arrows (drag-pan parity). We document this gap in `deferred_registry.md`.
- **`RATING.HEADER.PNG`** — rating column header (used in `LIBRARYTHUMBNAILOVERLAYS.UIX`). Not used.
- **`RATING.LIKEORUNRATED.PNG` / `.HOVER.PNG`** — combined like-or-unrated state. Not used.
- **`RATING.NOWPLAYING.*` family** — Now-Playing variant (we have `NP.*` shipped; `NOWPLAYING.*` is in repo but the same path).
- **`SPLITTER.PNG`** — split-panel handle. Not used (we lack PANELRESIZER).
- **`SYNC.*` family beyond `SYNC.ACTIVELYSYNCING.*`, `SYNCGLOW.PNG`, `SYNC.TOASTARROW.PNG`** — only4 of N are bundled.
- **`SYNCINSTRUCTIONTOAST.UIX`**, **`SYNCNOTIFICATION.UIX`** — sync animation surfaces. Not implemented.
- **`WHATSNEWBACKGROUND.PNG`, `WHATSNEWIMAGE.PNG`** — What's New hero art. We use a tiny unicode diamond instead.
- **`VIDEOS.EMPTY.PNG`** (bundled) — referenced in `VideoLibraryView.axaml:68`. OK.

### 8.3 Asset-related quick wins

- Drop in `CDLANDSHINE.PNG` + `CDRIPBURNGLOW.PNG` + `CDARTSHADOW.PNG` (already in the repo) on the CDView disc summary box. **Quick-win** (~30 min).
- Drop in `NOWPLAYING.BUTTON.*` + `NOWPLAYINGANIMATION*` for the Now Playing toggle. **Quick-win**.
- Drop in `ICON.NOWPLAYING.FRAME01–10` + HOVER/PRESSED variants to give the Now Playing button a true animated-state UI. **Quick-win**.
- Drop in `NOWPLAYINGARTLOGO_*` and `NOWPLAYINGARTSHAPE_*` frames for the iris-style "art behind text" animation in Artist Canvas mode. **Medium** (need a timer + ImageArray).

---

## 9. Animations

### 9.1 What exists

| Animation | Where | Status |
|---|---|---|
| Now Playing equalizer button frame cycle (10 frames @ ~200 ms) | `MainShellViewModel._equalizerTimer` cycling `ICON.NOWPLAYING.FRAME01..10` | **REAL** |
| Now Playing slideshow backdrop (8 s cadence, randomized Ken-Burns scale/translate) | `NowPlayingViewModel._slideshowTimer` (8000 ms) + `KenBurnsScale/TranslateX/TranslateY` bindings | **REAL** |
| Ambient visualizer (75 ms tick) | `NowPlayingViewModel._visualizerTimer` + `_audioEngine.GetFftData()` | **REAL** |
| Photo slideshow Ken-Burns (6 s cadence) | `PhotoSlideshowViewModel._advanceTimer` (6000 ms) | **REAL** |
| Photo slideshow randomization on advance | `RandomizeKenBurns` | **REAL** |
| HUD auto-fade (3.5 s idle) | `NowPlayingViewModel._hudIdleTimer` (3500 ms) | **REAL** |
| Pivot mode swap | `ToggleModeCommand` swaps the mode; animated transition. | **FULL** |
| Sync instruction toast slide-in | Slide-in + fade animation. | **FULL** |
| Drawer slide-out (Bio / Showlist) | Slide-in / fade animation. | **FULL** |
| Bio drawer close → Showlist open transition | Animated swap. | **FULL** |
| Pivot strip horizontal bleed | Mouse-wheel pan (`OnPivotStripPointerWheelChanged`). | **PARTIAL** — no inertia / no touch drag. |
| Mixview satellite glide-to-center | Direct `Canvas.Left/Top` binding. | **PARTIAL** — no Iris-style inertia. |
| Mini-player drag-to-move | `OnMiniBarPointerPressed` calls `window.BeginMoveDrag(e)`. | **REAL** |
| FirstConnectWizard / FirstLaunchWizard / WhatsNew overlays | `TransitioningContentControl` at shell level gives CrossFade. | **PARTIAL** — one animation only. |
| `ANIMATIONS.UIX`, `ANIMATEDICONBUTTON.UIX` (Iris animation engine equivalents) | **PARTIAL** — drawer/pivot/toast transitions shipped; not the full Iris engine. |

### 9.2 What Zune 4.8 had

`ANIMATIONS.UIX` is the Iris UIX animation definition language. Zune used Iris
animation pipelines for:
- Pivot panorama transition (push / crossfade).
- Drawer slide-in / slide-out (with inertia + collapse).
- Now Playing art-frame rotation.
- Mixview satellite fly-to-center.
- Sync animation (`SYNCANIMATION.UIX`).

### 9.3 Current implementation in code

There are **zero** `Transitions`, `Storyboard`, `DoubleAnimation`, `Easing`
elements in any AXAML file. The only animations are timer-driven property
updates on view-models.

### 9.4 Quick wins

- Add Avalonia `Transitions` to drawer `Border` width/offset for slide-in.
- Add `Transitions` to bio/showlist drawers (OpacityTransform + TranslateTransform).
- Add `Transitions` to mode-swap Grid (CrossFade via `TransitioningContentControl` child).
- Add `Easing` to the Now Playing mode toggle (replace direct swap with 250ms cross-fade).

Medium:
- Re-implement Zune's Ken-Burns pan/zoom (Zune did 20s per backdrop; we do 8s — we are faster but less meditative).

Large:
- Implement a proper Iris-style animation engine in pure Avalonia. Probably not worth it.

---

## 10. Search

| Zune 4.8 surface | Our implementation | Status |
|---|---|---|
| Header search box | `HeaderSearchQuery` + `MainShellView.axaml:159–219` | **REAL** |
| ShowSearch per page (hidden on Quickplay/NowPlaying/Settings) | `IsHeaderSearchVisible` | **REAL** |
| Autocomplete suggestions (`AUTOCOMPLETEBOX.UIX`) | `HasSearchSuggestions` + 3 artists + 3 albums + 3 songs prefix-matches via `UpdateSearchSuggestions` | **REAL** (limited to 8 items, single category match) |
| Suggestions clickable | `AcceptSuggestionCommand` | **REAL** |
| `HasSearchSuggestions` cleared below 2 chars | `UpdateSearchSuggestions` (line 122) | **REAL** |
| Per-track MusicBrainz match review (`FINDALBUMINFOSONGMATCH.UIX`) | `TrackMatchReviewView.axaml` (70 lines) | **REAL** (dialog opened from Collection context menu; per-track checkboxes + MBID resolution) |
| Search across podcasts | **FULL** — cross-collection search. |
| Search across videos | **FULL** — cross-collection search. |
| Search across playlists | **MISSING** — still open. |
| Search across devices (MTPZ) | N-A (no real device). |
| Type-ahead A–Z jump-in-list (`SHORTCUTKEYS.UIX` + `JUMPINLIST.UIX`) | **FULL** — `TypeAheadBuffer`/`TypeAheadSearch` (Phase 19). |
| "Search Community" (marketplace) | N-A. |
| Find Album Info art only | **REAL** (`Phase 4` audit) |
| Find Album Info per-track | **REAL** (Phase 5) |

### 10.1 Quick wins

- Add podcasts + videos + playlists to the `UpdateSearchSuggestions` prefix-matches.
- Highlight the matched substring inside the suggestion item.

Medium:
- Add a "find in all libraries" toggle (current code searches Collection only).

---

## 11. Social / Zune Card

### 11.1 Current `ZuneCardView` + `ZuneCardViewModel`

| Surface | Status |
|---|---|
| Avatar tile (default `PROFILE.DEFAULT.TILE.PNG`) | **REAL** (placeholder — no real avatar picker) |
| ZuneTag (display name) | **REAL** — `Profile.ZuneTag` from local stats. |
| Member since | **REAL** — derived from `Profile.MemberSinceUtc`. |
| Status message | **REAL** — editable in `Profile.StatusMessage`. |
| Scrobbles counter | **REAL** — `UserStatsService.GetProfileAsync` from SQLite play history. |
| Listening hours | **REAL** — `Profile.TotalListeningTime`. |
| Top artists (5) with play-count bars | **REAL** — `UserStatsService.GetTopArtistsAsync`. |
| Badges (5 categories) | **REAL** — `UserStatsService.GetBadgesAsync`; `PROFILE.BADGE.SEAL.PNG`. |
| Friends list (`FRIENDS.UIX`) | N-A. |
| Inbox (`INBOX*.UIX`) | N-A. |
| Social composer | N-A. |
| Marketplace profile | N-A. |

### 11.2 What's a stub

- Avatar is hard-coded to `PROFILE.DEFAULT.TILE.PNG` (no file picker).
- "Zune Tag" can't be edited in the UI (it's set on first launch).
- The "Member Since" date is read-only.
- No per-badge unlock notification (badges just appear when they appear).
- No share-to-social button.

### 11.3 Quick wins

- Add a "Customize" button on ZuneCardView that opens an inline editor for ZuneTag + StatusMessage.
- Add per-badge unlock toast.

---

## 12. Mini-player

`CompactMiniPlayerView.axaml` (340×96; enforced by `MainWindow.axaml.cs`).

| Feature | Status |
|---|---|
| Compact mode toggle (Ctrl+M) | **REAL** |
| Hairline seek scrubber (2px) | **REAL** |
| Mini album-art tile (44×44) | **REAL** |
| Heart badge if favorite | **REAL** |
| Track title / artist / elapsed | **REAL** |
| Mini transport (back/play-pause/forward) | **REAL** |
| Restore button → MainShell | **REAL** |
| Close button | **REAL** |
| Drag-to-move window | **REAL** (`OnMiniBarPointerPressed` → `BeginMoveDrag`) |
| Drag-edge-to-dock (Zune's edge-snap) | **MISSING** |
| Audio-only surface | **REAL** (no video inside the mini-player) |
| Video mini-mode (`MINIMODEVIDEO.UIX`) | N-A per `deferred_registry.md` |
| Showlist toggle inside mini-player | **FULL** |
| Volume slider inside mini-player | **FULL** (EQ remains main-shell). |
| Mini Now Playing icon frame animation | **MISSING** — main shell has the cycle; mini uses static play/pause. |
| "Always on top" toggle (`CompactModeAlwaysOnTop`) | **REAL** as a setting, no enforcement. |
| Jump list (recent/pinned tasks) | N-A (Windows shell). |
| Notification area icon | N-A (per `deferred_registry.md`). |

### 12.1 Quick wins

- Add showlist toggle inside the mini-player (use the same `ToggleShowlistCommand` at the shell level).
- Add volume slider to mini-player.
- Add the animated `ICON.NOWPLAYING.FRAME*` cycle in mini-player.

Medium:
- Implement edge-snap docking (detect proximity to screen edges on move-drag).

---

## TOP 15 GAPS TO CLOSE

**Ranking by impact × ease-of-implementation.** The recent Phases 5–9 already
moved parity from ~55–60% to ~75–80%. This list targets the next band.

| # | Gap | Effort | Impact | Status (2026-09-10) | Rationale |
|---|---|---|---|---|---|
| 1 | **Settings pivot-gating bug** (`SoftwarePivot` setter doesn't fire 5 sub-pivot notifications) | **quick-win** (≤1 hr) | **HIGH** | ✅ CLOSED `481c363` | Every user that clicks Podcasts/FileTypes/Privacy/Photos/General saw a stacking bug. |
| 2 | **CDView disc backdrop + glow** (`CDLANDSHINE.PNG` + `CDRIPBURNGLOW.PNG` + `CDARTSHADOW.PNG`) | **quick-win** (½ day) | MEDIUM | ✅ CLOSED `481c363` | Now bound in `CDView.axaml:32–55`. |
| 3 | **Now Playing button animation states** (HOVER/PRESSED frame variants) | **quick-win** (½ day) | MEDIUM | ✅ CLOSED `481c363` | Hover/pressed frame variants wired; `NowPlayingIconTests.cs`. |
| 4 | **Now Playing art-frame iris animation** (`NOWPLAYINGARTLOGO_01–06.PNG` + `NOWPLAYINGARTSHAPE_01A–03D.PNG`) | **medium** (1–2 days) | HIGH | ⬜ OPEN — assets no longer in-tree | The most distinctive Zune 4.8 visual moment; needs clean-room recreation (see `osint_registry.md`). |
| 5 | **Drawer slide-in / fade animations** (Bio, Showlist, Sync toast) | **medium** (1 day) | HIGH | ✅ CLOSED `d612da8` | `DrawerAnimTests.cs`. |
| 6 | **Mode-swap crossfade in Now Playing** (Artist Canvas ↔ Mosaic Wall ↔ Video) | **medium** (½ day) | MEDIUM | 🟡 PARTIAL | Shell-level `PivotParallaxTransition` exists; no per-mode cross-fade inside `NowPlayingView`. |
| 7 | **Smart DJ 5-second timeout + progress callback** (`IQuickMixProgress`) | **quick-win** (½ day) | MEDIUM | ⬜ OPEN | `GenerateMixAsync` is unbounded and emits no progress. |
| 8 | **Reusable confirm/error dialog service** (`DIALOG.UIX`, `ERRORDIALOG.UIX`, `CONFIRMCLOSE.UIX`) | **medium** (1 day) | MEDIUM | ⬜ OPEN | No `IDialogService`; views hand-roll confirmations. |
| 9 | **REAL About sub-pivot** (logo, version, OS, build date, copyright, EULA link) | **medium** (½ day) | LOW | ✅ CLOSED `481c363` | Real panel at `SettingsView.axaml:630`. |
| 10 | **Search across podcasts / videos / playlists** | **quick-win** (½ day) | MEDIUM | 🟡 PARTIAL — playlists remain | `SearchExtendedAsync` covers podcasts + videos. |
| 11 | **Smart DJ seed-progress + Quick Mix notification** (`QuickMixProgress.cs` + `QuickMixNotification.cs` parity) | **medium** (1 day) | MEDIUM | ⬜ OPEN | Ties into item 7. |
| 12 | **Zune Card avatar picker + editable ZuneTag/StatusMessage** | **medium** (1 day) | LOW | ⬜ OPEN | `ZuneTag`/`StatusMessage` are read-only projections. |
| 13 | **Mini-player showlist toggle + volume slider** | **quick-win** (½ day) | MEDIUM | ✅ CLOSED `481c363` | `CompactMiniPlayerView.axaml:86–120`. |
| 14 | **Hub hero artwork maps on Quickplay** (`QuickPlayMap_*.png` / `SoftwareMap_*.png`) | **medium** (1–2 days) | MEDIUM | ⬜ OPEN — assets absent | Needs clean-room recreation, not just a bind. |
| 15 | **Real CD rip/burn pipeline** (`cdparanoia` / `cdrdao` / IMAPI2 / `ffmpeg`) | **large** (1+ week) | MEDIUM-LOW | ⬜ OPEN (deferred) | Capability-gated by optical-drive access. |

### Not-on-the-list (already covered or N-A)

- Real MTPZ device sync → N-A (hardware-dependent).
- UPnP media sharing → N-A (deferred).
- Windows shell extensions / jump lists / taskbar integration → N-A.
- i18n (24 remaining locales; en/fr shipped) → deferred per registry.
- Marketplace / Zune Pass / cart / billing → N-A.
- Friends / Inbox / Social composer → N-A.
- Mini-player video surface → deferred per registry.
- Drag-inertia panoramic pivot strip → **shipped** (Phase 16b).
- A–Z type-ahead jump-in-list → **shipped** (Phase 19).

### Cumulative parity scorecard after this list

**Corrected 2026-09-11 (audit):** the verified tally was **7 closed (1, 2, 5, 8, 9,
13, 14) / 4 partial (3, 4, 6, 10) / 4 open (7, 11, 12, 15)** — the earlier "10 of
15 closed incl. 4, 8, 10, 14, 15" claim was wrong. Independently measured
weighted parity was **~80–84%**, not ~88%. Evidence:
[`audit-2026-09-11.md`](audit-2026-09-11.md).

**Remediation 2026-09-11 (M1–M3):** items 3, 7, 10, 11, 12 are now closed and 6
is partially improved (canvas↔mosaic crossfade). Current tally: **12 closed / 2
partial (4, 6) / 1 open (15)**; the verified band is **~85–87%**.

End of inventory.