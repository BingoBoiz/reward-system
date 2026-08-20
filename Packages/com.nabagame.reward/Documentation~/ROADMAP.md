# Roadmap

Development plan for `com.nabagame.reward`. Ordering principle: **package skeleton and shared primitives first**, then features smallest-to-largest against the RefUI mockups. The legacy `_GameBase` monolith served as behavioral reference and has been removed with the repo restructure; the demo host is now `Assets/_RewardDemo/` (a symlink view of `Samples~/RewardDemo`).

Dependencies: `0 → 1 → {2 → 3, 4} → 5` (Lucky Spin can run in parallel with Daily/Online). Phase 2.5 (contract flip) was inserted 2026-08-20 and reworks everything built before it; phase 3 builds on the new contract.

**Status 2026-08-20:** phases 0, 1, 2, 2.5, 3, 4, **4.5** done (package `0.8.0` — all three features shipped on the panel-owned contract: one prefab per feature, host-written `Sample*` managers, grants via `Row.OnClaimed`, static `RewardHooks`, `SetInfo(rows)` init). `0.9.0` (2026-08-20, decision #28) converted the two fixed-count boards to inspector-authored: Daily's 7 cards and Spin's 8 wedges are pre-authored prefab instances wired into serialized lists, backgrounds per-instance, `SetInfo` binds instead of builds — layout is now pure prefab work. **Next: phase 5** (sample import dry-run into a scratch project + `_ASMR_Tower`, polish, `1.0.0`).

## Phase 4.5 — Panel-owned refactor (`0.8.0`, breaking) ✅ (2026-08-20)

Decisions #21–#27; documented like phase 2.5 because it reworks everything before it. Verified end-to-end in Play mode (all three features granting, null-`OnClaimed` guard firing).

- **Managers out of the package:** `{Feature}Manager` MonoBehaviours deleted; their entire body (save, `TimeScheduler` timers, `AdFlow` ads, IAP, claim/spin state) merged into `{Feature}Panel` — a plain-C# state class was rejected as pure forwarding overhead. The host writes a ~15-line manager (templates: `Sample{Feature}Manager` in the sample) holding `[TableList] rows` and the `OnClaimed` grant mapping. The panel keeps the manager files' meta GUIDs, so the sample scenes' 33 serialized rows survived the move verbatim.
- **Grants:** `Row.OnClaimed` callback replaces the grant-carrying events (#22); every grant keeps the audit `Debug.Log`; a null callback LogErrors "was NOT granted". `SampleRewardGranter` became a plain `Grant(key, icon, amount)` the managers call.
- **Hooks:** `RewardHooks` static with safe defaults + `SubsystemRegistration` reset (#23); `AdFlow` lost its ctor and gained a ~15s Busy timeout (#26).
- **Naming:** `SetInfo(rows)` is the single init — `StartClass` retired repo-wide; `OpenPanel`/`ClosePanel` are the activation APIs; aliases deleted (#25). Panel members regrouped into the fixed `API`/`Logic`/`UI`/`Debug` regions, `API` first.
- **Leniency (#24):** incomplete rows warn via `{Feature}Row.Warn`, structure throws, hooks never throw; `Day`/`Wedge`/`Slot` fields dropped (list position is the index); Daily's card-count/backgrounds mismatch downgraded to warn + wrap-around (wrap-around later replaced by per-instance authored backgrounds in `0.9.0`, decision #28).
- **Lifecycle hardening:** Online's pause flush moved to `Application.focusChanged` (survives a deactivated panel; the `disableWhenHidden` checkbox can no longer break the no-suspended-playtime rule); countdown loops gate on `IsVisible()`; `TimeScheduler` callbacks wrapped in try/catch; every serialized UI ref null-guarded (#26).
- **Sample (#27):** one scene `RewardSample.unity` (home HUD + all three panels), `Sample*` prefixes everywhere, knobs re-entered on the panel prefabs, `Assets/_RewardDemo` symlinks into `Samples~/RewardDemo` (no mirror step). Build settings point at the single scene.
- **Gate:** met — compile clean, boundary grep clean, Play-mode grants verified for all three features, save payloads unchanged.

## Phase 2.5 — Contract flip (`0.5.0`, breaking) ✅ (2026-08-20)

Reworked the shipped features (Daily Reward, Lucky Spin) onto the #16–18 contract. No new features in this phase. Landed as specified below, with these resolutions: package events split into per-feature files (`DailyRewardEvents.cs`, `LuckySpinEvents.cs`, `OnlineRewardEvents.cs` — `SpinResultEvent { Row }` in `Core/` would have violated the Core-never-references-a-feature rule); panels keep `OpenPanel()`/`ClosePanel()` with `SetInfo()` as a `BaseUI` override alias; every event raise allocates a fresh instance (the 0.4.x cached payloads were a consumer anti-pattern, CONSUMER-STYLE.md); persisted `StreakDay` is range-checked against `rows` on load; `SampleRewardGranter` became a static handler subscribing all grant events; `SampleItemGrantedEvent` carries `{ Key, Icon, Amount }`.

- **Data:** delete `RewardItemData`, `RewardItem`, `DailyRewardRowData`, `LuckySpinRowData` (and their `[CreateAssetMenu]`s). Rows gain `public Sprite Icon` and move behind a serialized `[TableList] public List<{Feature}Row> rows` on each manager. Lucky Spin tuning knobs (`FreeSpinCooldownSeconds`, `SpinDurationSeconds`, `SpinFullTurns`) become `[SerializeField]`s on `LuckySpinManager`; the weighted `Roll()` moves into the manager. `StartClass(hooks)` validates the list (throw naming index/field) — plus cheap `OnValidate` LogErrors on the manager.
- **Grants:** delete `IRewardGranter` and `hooks.Granter`/`hooks.Catalog`. `DailyRewardManager.Claim()` and `LuckySpinManager.FinishSpin()` stop granting; they mutate + save, `Debug.Log` the grant, then raise `DailyRewardClaimedEvent { Day, Row }` / `SpinResultEvent { WedgeIndex, Row }`. Add the per-panel button/close events listed in each feature spec.
- **Panels:** add parameterless `SetInfo()` / `Close()` (decision #18). `DailyRewardCard`/`LuckySpinWedge` `StartClass` drop the separate `RewardItem` param (row carries the icon).
- **Sample + demo host:** `RewardSampleBoot` fills the row lists (Inspector), drops `rewardItemData`/config fields, subscribes to the grant events and grants there (`SampleRewardGranter` becomes a plain event handler keeping the PlayerPrefs economy + `SampleItemGrantedEvent` ceremony recipe). `ItemReceivedPanel` debug buttons lose `hooks.Catalog`. Scene refs to the three `Data/Raw*.asset` files removed; assets deleted.
- **Docs:** README quick start, INTEGRATION-GUIDE §4–§7, FEATURES Config/API/Events sections — already rewritten 2026-08-20; verify against shipped code at the end of the phase.
- **Gate:** integration checklist passes for both features on the new contract; boundary grep clean; kill/reopen, button spam, repeated open/close re-verified.

## Phase 0 — Docs & skeleton ✅ (2026-08-19)

`package.json`, README, CHANGELOG, `Documentation~` set, RefUI folder. No code.

## Phase 1 — Package scaffold + core primitives ✅ (2026-08-19)

- `Runtime/NabaGame.Reward.asmdef` — the asmdef *names* are `com.bmh.core.runtime` and **`com.nabagame.ui.runtime`** (the UI package's file is named `com.bigbear.ui.runtime.asmdef` but declares the `com.nabagame.ui.runtime` name); plus `UniTask` and `Unity.TextMeshPro`.
- **Deviation:** no `Editor/NabaGame.Reward.Editor.asmdef` yet — nothing needed it. Package panels carry explicit Odin `[Button]` debug methods instead of an attribute processor.
- **Deviation:** `AdFlow` not built — Daily Reward used no ads at this point. It landed with Lucky Spin (phase 4); Daily uses it since `0.7.0` for the ads-gated Open All.
- `Runtime/Core/`: `RewardItem` (SO in 0.2–0.3, plain row + `RewardItemData` catalog since 0.4.0), `IRewardGranter`, `RewardHooks` (+ StartClass validation), PlayerPrefs profile base (JsonUtility, `NabaReward.*` keys, `Version` field), `TimeScheduler` (moved from `Assets/_GameBase/Scripts/_Others/TimeScheduler.cs`, renamespaced `NabaGame.Reward`), `AdFlow` helper, package event classes.
- Repo cleanup (still open): delete the empty `Assets/_00 Daily_Reward/` and `Assets/_00 Lucky_Spin/` folders.
- Candidates to upstream to `com.naba.extend` later (not now): `BigNumberExtension` and similar generic utils. `RewardAmountFormat` in `Core/` is the package's own minimal replacement.
- **Gate:** met — package compiles into assembly `NabaGame.Reward`, console clean, boundary grep returns nothing.

## Phase 2 — Daily Reward ✅ (2026-08-19)

- `DailyRewardManager` + `DailyRewardProfile` (streak day, last-claim UTC date `yyyy-MM-dd`, PlayerPrefs), midnight rollover armed via `TimeScheduler`.
- Data: 7 importer-shaped rows keyed by string (`DailyRewardRowData`, 0.4.0) — **reworked to a dev-filled row list in phase 2.5**.
- `DailyRewardPanel` prefab + scripts (7-card strip, claimed/claimable/locked states, OPEN ALL button). OPEN ALL semantics resolved: claims today's one card, "COME BACK TOMORROW" when taken (see the feature spec).
- Demo host: hooks adapters, sample data, boot + UIManager wiring.
- **Gate:** met — integration checklist (CONVENTIONS.md) passes in the demo host.

## Phase 3 — Online Reward ✅ (2026-08-20, `0.6.0`)

Built on the phase-2.5 contract from day one: dev-filled `List<OnlineRewardRow>` (rows carry `Sprite Icon`), grants leave as `OnlineRewardClaimedEvent`. Open decisions resolved (see the feature doc): session ends on app kill only; x2/x5 stack to ×7 (legacy rule); X5 needs 2 ads with the partial counter persisted; OPEN ALL is ad-gated and claims every unclaimed slot (placement `OnlineReward_OpenAll`).

- `OnlineRewardManager`: **session-scoped** grid (decision #9) — slot timers derive from session play time (baseline pattern, ARCHITECTURE.md §5); grid resets on app quit and cycles when fully claimed. Persisted profile (`NabaReward.Online`) keeps only `Version` + `SpeedUpX5Ads`.
- x2/x5 speed-up via `AdFlow`; placements `OnlineReward_x2Speed`, `OnlineReward_x5Speed`, `OnlineReward_OpenAll`.
- `OnlineRewardPanel` grid per `Assets/_ASMR-Tower/Art/preview/playtime reward.jpg` (18 cells 3×6 in the sample; row count drives the grid), OPEN ALL, X2/X5 booster buttons with countdown/ad-counter states. Sample scene `OnlineRewardSample.unity` + home Playtime button with red dot.
- Deferred to phase 5: retiring legacy playtime code paths in `_GameBase` (playtime region of `RewardManager`, `RewardPlaytimeTab`, `RewardPlaytimeSlot`) — they sit with the rest of the legacy monolith cleanup.
- **Gate:** met — integration checklist passes in the demo host (grants land end to end, console clean).

## Phase 4 — Lucky Spin ✅ (2026-08-19)

Built as specified below, with these resolutions: wedge count is data-driven but the sample wheel art has **8** segments (panel logs an error on mismatch); grant fires on wheel stop (kill mid-animation loses the spin, no duplicate grant); ad spins are unlimited during cooldown; no extra jackpot ceremony. `AdFlow` landed here instead of phase 3. Sample folder renamed to `Samples~/RewardDemo` (shared host scripts cannot be split into two importable samples).

- Data: wedge table, wedge count driven by data (mockup shows 10). 0.4.0: `LuckySpinRowData`, importer-shaped rows keyed by string — **reworked to a dev-filled row list in phase 2.5**.
- `LuckySpinManager` + `LuckySpinProfile`: weighted roll, free-spin cooldown deadline via `TimeScheduler` (persisted), ad spin via `AdFlow` (placement `LuckySpin_AdSpin`).
- `LuckySpinPanel` per `RefUI/lucky-spin.png`: wheel built from wedge data, pointer, decelerating spin-to-wedge DOTween tween landing on the rolled wedge (the legacy `SpinEffect` looper is only good for idle shimmer), free SPIN button + ad SPIN button with "Free spin in mm:ss" label.
- **Gate:** integration checklist passes.

## Phase 5 — Samples, polish, release 1.0.0

- `Samples~/RewardDemo`: one scene, `Sample*` managers with filled row lists + `OnClaimed` grant mapping, audio/ads adapters — verified importable into a scratch project and into `_ASMR_Tower` (INTEGRATION-GUIDE §10).
- UI polish pass per feature against RefUI mockups (`ui-from-image`), red-dot recipe wired in the demo host.
- Full INTEGRATION-GUIDE dry run into a fresh project; fix every step that didn't reproduce.
- `package.json` → 1.0.0, CHANGELOG, publish to GitLab.
