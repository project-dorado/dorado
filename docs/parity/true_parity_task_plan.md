# Dorado True-Parity Task Plan

Derived from the audit in [`zune48_parity_audit.md`](zune48_parity_audit.md). Scoping decisions: **ManagedBass** audio engine, **video/photos included**, **i18n deferred**.

Cross-cutting convention per phase: unit tests for every new service · design-invariants audit (0 violations) · Release build 0 warnings/0 errors · walkthrough section · milestone commit.

## Status Snapshot (updated after Phase 8)

| Phase | Status | Commit |
|---|---|---|
| 5 — Real Audio Engine | ✅ **COMPLETE** | `fc5e7a1` |
| 6 — Collection Parity | ✅ **COMPLETE** | `ead9a07` |
| 7 — Onboarding & Shell Parity | ✅ **COMPLETE** | `f3d95aa` |
| 8 — Video & Photos | ✅ **COMPLETE** | `833b5ef` |
| 9 — Device Sync Architecture | **NEXT** | — |
| 10 — CD Land Real Pipeline | Optional (no drive to test) | — |
| 11 — Final Parity Sweep | Final | — |

Tests: **99 passing** · Design audit: **24 files, 0 violations** · Estimated audit parity after Phase 8: **~80%** (re-measure in Phase 11).

---

## Phase 9 — Device Sync Architecture (P2, hardware N-A but architecturally real)

Goal: replace the simulated sync blob with a genuine sync-group engine so that (a) the UI reflects Zune's sync semantics faithfully and (b) a real MTPZ transport can slot in later with zero UI changes.

| # | Task | Status |
|---|---|---|
| 9.1 | **Sync-group engine.** `SyncGroup` / `SyncCategory` / `SyncMode` domain models (music/pictures/videos/podcasts categories mirroring `SchemaSyncGroup`/`DetailsBackedSchemaSyncGroup`), persisted per device serial. | ✅ Done — `SyncModels.cs`, `SyncGroupService` (SQLite via AppDbContext `SyncGroups`), default group built from Settings rules |
| 9.2 | **Rule evaluation → planned transfer.** Engine consumes the sync rules already in Settings (music/podcast/video/pictures rules) and computes a *planned transfer set*: files to add, remove, keep. **Dry-run mode** (no device): the Device view shows "what would sync" as a reviewable manifest. | ✅ Done — `SyncEngine.BuildPlan` (removals first to free space, capacity-truncated adds, `NewestCount` limits, hearted-only selection); Device view "PREVIEW WHAT WILL SYNC" manifest |
| 9.3 | **`IDeviceTransport` abstraction.** Contract: enumerate contents, read device DB metadata, copy to/from device, free-space query. Implement `SimulatedTransport` (in-memory device filesystem, powers the existing UI states); a future `MtpTransport` implements the same contract against `ZuneMTPZ` semantics. | ✅ Done — `IDeviceTransport` + `SimulatedDeviceTransport` (seeded stale content, byte accounting); a real MTP transport is the only remaining hardware step (N-A) |
| 9.4 | **Live gas gauge + sync animation from engine progress.** Replace the fake progress blob with per-category byte accounting from the planned set; the Phase 7 toast/glow already renders it. | ✅ Done — `ApplyPlanAsync` reports progress; `ApplyTransportToGauge` folds live `transport` bytes into `ZuneDevice` gas gauge; sync-complete chime |
| 9.5 | **Guest sync mode** (`GuestSchemaSyncGroup` parity): temporary profile that copies selected content without claiming ownership; device view "GUEST SESSION" state. | ✅ Done — guest `SyncGroup` (add-only, rules flow but removals suppressed); START/END buttons + badge in Device view |
| 9.6 | **Reverse sync (device → PC) manifest.** Even without a transport, model the flow: browse device contents via `SimulatedTransport`, "copy back to collection" produces a file-import queue. | ✅ Done — "ON DEVICE" browser + per-item COPY BACK → `PendingImports` queue |
| 9.7 | Tests: rule evaluation → planned set (add/remove/keep), dry-run manifest correctness, guest session isolation, category byte accounting. | ✅ Done — 13 tests in `SyncEngineParityTests` + `SyncGroupPersistenceTests` (108 total, 0 failed) |

## Phase 10 — CD Land Real Pipeline (P3, capability-gated — no optical drive to test)

| # | Task |
|---|---|
| 10.1 | **`IOpticalDriveService`** with platform detection (Linux: `udisks2`/`/dev/sr0` + `blockdev` capability; Windows: MCI/SPTI). Graceful "no optical drive" state — the current DISC view stays as the manual/simulated mode. |
| 10.2 | **Real rip:** CDDA extraction (Bass `BASS_CD` add-on or platform `cdparanoia`) → encode via Bass encoders (FLAC/MP3 honoring the rip settings) → library ingest with MusicBrainz release tagging (reuse Phase 4/6 services). |
| 10.3 | **Real burn:** playlist → Audio CD via platform tooling (Linux `cdrdao`/`wodim`, Windows IMAPI2) with the authentic burn-completion chime. |

*Execution only if you want it blind-implemented; everything is capability-gated so machines without drives (yours) keep today's experience.*

## Phase 11 — Final Parity Sweep (P3)

| # | Task | Status |
|---|---|---|
| 11.1 | **CI/release packaging for native audio+video.** Add `apt-get install -y libvlc` (linux-x64/arm64 jobs) so published Linux builds get video; verify Bass natives ship in archives (they do — vendored); document the win-arm64 Bass limitation (simulated audio fallback) in release notes. | ✅ Done — `.github/workflows/ci.yml` (build+test+audit with libvlc installed) and `release.yml` (4-RID self-contained publish, natives verification, platform notes); README Platform Notes added |
| 11.2 | **Re-run the parity audit** against `docs/parity/zune48_parity_audit.md` — update every status column, measure the delta from ~55%, refresh the executive summary. | ✅ Done — dated re-audit snapshot added; ≈55–60% → ≈75–80% |
| 11.3 | **Performance pass:** startup (deferred service init), large-library scan responsiveness, artwork decode caching, slideshow memory. | ✅ Done — startup was already lazy (pivot loads on navigation); SQLite WAL journaling for scan/sync responsiveness; artwork decode cache (600 tiles / 48 hi-res slideshow frames, bounded) |
| 11.4 | **Polish backlog triage:** mini-player video surface (currently text-only), Mixview external related-artist satellites (currently local-only), notification-area tray icon. Fold in or move to deferred. | ✅ Done — all three triaged to the deferred registry with rationale |
| 11.5 | **Deferred registry (documented, not scheduled):** i18n (26 locales), UPnP media sharing (ZuneNSS parity), Explorer/taskbar shell integration, MTPZ firmware update/restore/rollback (hardware N-A), Windows jump lists. | ✅ Done — `docs/parity/deferred_registry.md` (9 items incl. the 11.4 triage) |

## Execution Order

**9 ✅ → 11 → (10 only if blind-implementing CD is desired)**

Phase 9 ✅ complete (all sync semantics + transport abstraction + guest/reverse sync). Remaining: Phase 11 (CI packaging, measured re-audit, performance pass, deferred registry) and the optional capability-gated Phase 10.

---

# Next Program — Phases 12–17 (approved 2026-09-10)

Scoping decisions: **balanced** (finish Zune 4.8 fidelity + layer rune-inspired modern
capabilities), **plugin host with Last.fm + Discord reference plugins**, **no physical Zune
hardware** (build the transport seam + virtual harness), **i18n un-deferred (first pass)**.
Sources and licenses: [`osint_registry.md`](osint_registry.md).
Execution order: **0 → 12 → 13 → 16a → 14 → 17 → 16b → 15** (16a may parallelize with 13/14).

Cross-cutting convention per phase: unit tests for every new service · design-invariants audit
(0 violations) · Release build 0 warnings/0 errors · README scorecard + `deferred_registry.md`
refresh · milestone commit.

| Phase | Goal | Status |
|---|---|---|
| **0 — OSINT registry + reconciliation** | Catalog community sources with license/clean-room notes; reconcile docs vs code (`gap_inventory.md` §0, `GEMINI.md` FTS5, README test count). | ✅ Done |
| **12 — Plugin host runtime** | Out-of-process host (`.znp` loader, `plugin.json`, stdio/socket JSON-RPC, health/restart, host services), event bridge from `IPlayerCoordinator`, `SoftwareSubPivot.Plugins` settings page; reference `Dorado.Plugins.LastFm` + `Dorado.Plugins.Discord`. | ✅ Done |
| **13 — Listening intelligence** | Audio-feature analysis (BPM/energy/valence/acousticness/danceability/spectral-centroid) persisted per track; cosine similarity; `DynamicMix` rules surfaced on Quickplay. | ✅ Done |
| **14 — Podcast modernization** | Feed normalization (namespace-agnostic parsing, iTunes durations, media/enclosure fallbacks, HTML-page discovery, dedupe). | ✅ Done |
| **15 — MTP transport** | `MtpTransport` + `IMtpDeviceClient` seam; `LibUsbMtpDeviceClient` (product-ID detection) + `VirtualMtpDeviceClient`; shared contract tests. Real MTPZ session layer remains hardware-N-A. | ✅ Done |
| **16a — Fidelity quick wins** | 10-band managed-biquad EQ ✅, FTS5 search ✅; A–Z type-ahead deferred (needs a list-control scroll-into-view refactor). | 🟡 EQ+FTS5 done |
| **16b — Fidelity medium** | Drag-inertia pivot strip ✅; Quickplay hub hero maps + Iris art-frame animation deferred (assets absent post IP move — need clean-room recreation). | 🟡 Partial |
| **17 — i18n first pass** | `ILocalizationService` + catalog, `en` + `fr`, language selector in Settings → General, persisted. Remaining string extraction/locales incremental (see deferred registry). | ✅ Done |

---

# Next Program — Phases 18–23 "Fidelity Finish" (approved 2026-09-10)

Scoping decisions: **north star = finish Zune fidelity leftovers**; native/OS deps acceptable
**where cross-platform and guarded**; plugin ecosystem maturity **in scope** (tooling +
integration tests + expanded services); **no Zune hardware** (MTPZ stays N-A).
Visual recreation is **procedural/vector in code** (clean-room, no binary assets); dialogs are
an **in-shell modal overlay**; A–Z type-ahead spans **all lists**; badges implement the
**full Zune taxonomy with local Forums/Reviews substitutes**.

Order: **18 → 19 → 20 → 21 → 22 → 23**. Each phase: tests · design audit 0 violations ·
Release 0 warnings · README scorecard + `deferred_registry.md` refresh · milestone commit.

| Phase | Goal | Status |
|---|---|---|
| **18 — Reconciliation baseline** | Fix `GEMINI.md` aspirational claims (MPRIS/SMTC, WinUSB/libusb, ZMDB/SSDP), add OS-media + real-DSP analysis to the registry, correct README hub-map claim, re-measure scorecard (~85% → ~88%). | ✅ Done |
| **19 — Interaction fidelity** | `IDialogService` (in-shell modal) + migrate destructive confirms; long-press-to-pin (B3); A–Z type-ahead across all lists (ListView + `ScrollIntoView`). | NEXT |
| **20 — Reputation badges (C3)** | Tiered (Bronze/Silver/Gold) Album/Artist Power Listener + Milestones; Reviews substitute (local review entity/editor); Forums substitute (Curator reputation from edits/playlists/pins). | Planned |
| **21 — Artist-background fallback (C1)** | `IArtistImageProvider` chain: Fanart.tv → community `ZuneArtistImages` (configurable) → Wikimedia/Last.fm → theme fallback. | Planned |
| **22 — Clean-room visuals (D2 + Iris + chevrons)** | Procedural hub hero map; code-generated Iris art-frame reveal; vector pivot chevrons. | Planned |
| **23 — Plugin ecosystem maturity** | `.znp` pack target + author template/sample; real stdio process E2E tests; expanded `player/*` + paginated `library/queryTracks` services. | Planned |
