# Changelog

All notable changes to this package are documented here. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning: [SemVer](https://semver.org/).

## [Unreleased]

### Changed

- Panel inspectors reorganized with Odin groups: every serialized field on `DailyRewardPanel`, `LuckySpinPanel`, and `OnlineRewardPanel` now lives in a `Config` / `UI` / `FX` tab (`TabGroup`), with `FoldoutGroup` sub-sections for crowded tabs (Daily `Open All`/`Cards`, Spin `Spin Button`/`Wheel`/`SFX`). Each field also carries a short Vietnamese `//` comment (under 7 words) describing what to fill in — a deliberate, protected exception to the English-comments rule (see CONVENTIONS.md). No behavior or serialization change; existing prefab values are untouched.
- Demo panel prefabs (`DailyRewardPanel`, `LuckySpinPanel`, `OnlineRewardPanel`, `SampleItemReceivedPanel`) now ship `useCustomStartAnchoredPosition = true` with `customStartAnchoredPosition = (0, 0, 0)`, so a panel always opens centered no matter where its RectTransform sits in the editor. `SampleUIRoot.prefab` parks the panels on a non-overlapping grid with a clear gap — Daily `(0, 1500)`, Online `(0, -1500)`, Spin `(2800, 0)`, ItemReceived `(-2800, 0)` — and pins the always-visible HUD roots (`HomeButtons`, `SampleCurrencyHud`, which have no `UIElement` to snap them back) at `(0, 0)`.

## [0.8.1] - 2026-08-20

### Changed

- Daily Open All display rule: the ads and IAP buttons now sit at the **same position** (the mockup's single OPEN ALL spot) and **at most one is visible, picked by config — IAP (`openAllIapProductId` set) wins over ads (`openAllAdsRequired > 0`)**; never both. Flow logic, config fields, and save are unchanged.
- Daily Open All buttons resized 440x164 → 440x105 to match the `daily.jpg` mockup proportions; `checkpoint_0002_button-green` / `checkpoint_0003_button-blue` gained 9-slice borders (35px) and the button Images switched to Sliced so the flatter shape does not distort corners; `OpenAllRoot` lost its HorizontalLayoutGroup.

## [0.8.0] - 2026-08-20

Panel-owned refactor (ARCHITECTURE decisions #21-#27). One prefab per feature; the host writes one tiny manager. Breaking.

### Removed

- `DailyRewardManager`, `LuckySpinManager`, `OnlineRewardManager` MonoBehaviours — all logic (save, timers, ads, IAP, claim/spin rules) moved into the panels. The sample ships `SampleDailyRewardManager` / `SampleLuckySpinManager` / `SampleOnlineRewardManager` (~15 lines each) as the host-side template.
- Grant-carrying events `DailyRewardClaimedEvent`, `SpinResultEvent`, `OnlineRewardClaimedEvent` — grants now travel through `Row.OnClaimed` (#22). Notification events (`{Feature}ChangedEvent`, `SpinStartedEvent`, `OnlineRewardSpeedUpEvent`, `{Feature}PanelClosedEvent`) remain.
- `RewardHooks.Validate` and the hooks parameter on every init — `RewardHooks` is now a static class with safe defaults (#23).
- The parameterless `SetInfo()` / `Close()` panel aliases (#25) and the `Day`/`Wedge`/`Slot` row index fields (#24 — list position is the index; old serialized rows load unchanged, the stale keys are ignored).
- The redundant `OnApplicationPause`/`OnApplicationQuit` handlers (every mutation already saves; Online now flushes via `Application.focusChanged`, which fires even on a deactivated panel).
- Sample: the extra scenes and the `SampleStartPanel` enum — one scene (`RewardSample.unity`) with the home HUD remains (#27).

### Changed (breaking)

- Init is `SetInfo(...)` everywhere — `{Feature}Panel.SetInfo(List<{Feature}Row> rows)` replaces `StartClass(manager, hooks)`; widgets follow (`DailyRewardCard.SetInfo`, ...). `OpenPanel()`/`ClosePanel()` are the dev-facing activation APIs (#25).
- Feature knobs moved from the managers to the panel prefabs: Daily `openAllAdsRequired`/`openAllIapProductId`/`openAllIapPriceText`; Spin `freeSpinCooldownSeconds`/`spinDurationSeconds`/`spinFullTurns`/`spinStartSfx`/`landSfx`; Online `x2DurationSeconds`/`x5DurationSeconds`/`x5AdsRequired`. Manager-level `claimSfx` became per-row `ClaimSfx`. Defaults match the old sample values; only the Daily Open All values needed re-entering on the panel prefab.
- Validation flipped to the leniency ladder (#24): incomplete row data warns (aggregated, via `{Feature}Row.Warn`), only structural breakage throws, unset hooks LogError and proceed.
- Red-dot queries moved to the panels: `dailyRewardPanel.ClaimableCount`, `luckySpinPanel.FreeSpinReady`/`IsSpinning`, `onlineRewardPanel.HasClaimable`; `ResetProfile()`/`ResetSession()` likewise.
- Sample types renamed with the `Sample` prefix (#27): `RewardSampleBoot`->`SampleRewardBoot`, `ItemReceivedPanel/Cell/BurstFx`->`SampleItemReceived*`; prefabs `OnlineRewardManager`->`SampleOnlineRewardManager`, `UIMainManager`->`SampleUIRoot`. **Upgrading hosts must delete the old imported sample folder first** (stale copies are CS0101 duplicate-definition errors).

### Added

- `{Feature}Row.ClaimSfx` (per-reward audio), `{Feature}Row.OnClaimed` (the grant callback), constructors documenting the fill order, and `static Warn(rows, context)` for host `OnValidate` use.
- Panels display the received rows read-only in the Inspector (`[ShowInInspector, ReadOnly, TableList]`).
- Null-tolerant UI (#26): every serialized button/label/badge/template may be disabled or deleted — guarded dereferences, LogError-and-skip templates, bounds-checked indexing. `AdFlow.Busy` auto-releases after ~15s when a host SDK swallows both callbacks.
- `RewardHooks` static defaults + `SubsystemRegistration` reset (domain-reload-off safe); `TimeScheduler` callbacks wrapped in try/catch so one throwing timer cannot kill the shared loop; panel countdown loops gate on `IsVisible()` so a bare `Hide()` stops them.

### Notes

- Save payloads are unchanged (`NabaReward.Daily`/`.Spin`/`.Online`, `ProfileVersion` 1) — existing player state survives the upgrade.
- `Assets/_RewardDemo/` is now a symlink view of `Samples~/RewardDemo/` — single source, no mirror step.

## [0.7.0] - 2026-08-20

### Added

- `RewardHooks.PurchaseIap` (`Action<string, Action<bool>>` — productId, result) + `Validate(..., requireIap)`. Required only when a feature sells an IAP; the callback must fire on success, failure, and cancel. Consumer adapter is one line onto `IAPManager.InitiatePurchase` (ARCHITECTURE decision #20).
- Daily Reward **Open All**, gated behind ads or IAP — two independently-toggleable buttons on `DailyRewardPanel` (ads: video icon + `X/N` progress; IAP: dev-supplied price string), config on `DailyRewardManager`: `openAllAdsRequired` (0 = ads button off), `openAllIapProductId` ("" = IAP button off), `openAllIapPriceText` (throws when the product id is set without it). New API: `UnopenedCount`, `OpenAllAdsRequired`, `OpenAllAdsWatched`, `OpenAllIapEnabled`, `OpenAllIapPriceText`, `RequestOpenAllAds()`, `RequestOpenAllIap()`; placement `DailyReward_OpenAll`; `DailyRewardProfile.OpenAllAdsWatched` (ads progress persists, reset only when Open All fires; `ProfileVersion` intentionally stays 1 — the field is additive and a bump would wipe live streaks, `RewardProfileStore` has no migration path).

### Changed (breaking)

- **OPEN ALL semantics**: no longer a shortcut for tapping today's card — it claims **every remaining day of the week at once** (one `DailyRewardClaimedEvent` per day + one `DailyRewardChangedEvent`; same-frame batching hosts show one ceremony). The buttons stay available after today's card is claimed and hide only when the week is fully opened, replaced by a `COME BACK TOMORROW` label.
- **Day-7 wrap deferred**: `Claim()` sets `StreakDay = day + 1` (no `% 7`); `StreakDay == 7` = week complete, reset to 0 happens on the next UTC day (`ResetWeekIfElapsed` on load, rollover, resume). The old wrap re-rendered the week as unclaimed same-day and would have let Open All re-grant it. `ClaimableCount` guards `StreakDay < DayCount`; `SetStreakDay` now clamps to 0..7.
- `DailyRewardPanel` serialized fields: `openAllButton`/`openAllLabel`/`openAllDisabledTint` replaced by `openAllAdsButton`, `openAllAdsCountLabel`, `openAllIapButton`, `openAllIapPriceLabel`, `comeBackLabel` (prefab rebuilt: `OpenAllRoot` HorizontalLayoutGroup with the two buttons).
- Sample: `RewardSampleBoot` wires a fake `PurchaseIap` (`failIapPurchase` toggle for testing); sample scenes configure `openAllAdsRequired = 3`, product id `com.nabagame.sample.dailyopenall`, price `$4.99`; `checkpoint_0003_button-blue.png` reimported as Sprite.

## [0.6.0] - 2026-08-20

### Added

- **Online Reward (Phase 3)** — session-scoped timed reward grid (`Runtime/Features/OnlineReward/`), built on the 0.5.0 contract: dev-filled `[TableList] rows` (`OnlineRewardRow { Slot, Key, Icon, Amount, UnlockAfterSeconds }`), grants leave only as `OnlineRewardClaimedEvent { Slot, Row }` (audit `Debug.Log` per raise).
  - `OnlineRewardManager`: session play time via the baseline pattern (no `Update()`), slot unlocks and buff expiry armed on `TimeScheduler`; x2/x5 speed-ups stack to ×7 (legacy rule), X5 needs `x5AdsRequired` (2) ads with the partial counter persisted in `NabaReward.Online` (profile keeps only `Version` + `SpeedUpX5Ads`); ad-gated OPEN ALL claims every unclaimed slot in one frame; cycle resets when all slots are claimed; `ResetSession()` + Odin debug buttons.
  - `OnlineRewardPanel` (`BaseUI`, `SetInfo()`/`Close()`): `GridLayoutGroup` grid driven by row count (sample: 18 = 3×6 per `playtime reward.jpg`), per-column cell frames, 1s countdown loop while open, booster buttons that flip to buff countdowns, close-raises `OnlineRewardPanelClosedEvent`; `OnlineRewardCell` widget (claim pulse, claimed tick, timer/claim labels).
  - Events `OnlineRewardChangedEvent`, `OnlineRewardSpeedUpEvent { Multiplier }`, `OnlineRewardPanelClosedEvent`; placements `OnlineReward_x2Speed`, `OnlineReward_x5Speed`, `OnlineReward_OpenAll`.
- Sample: `OnlineRewardSample.unity` scene, `OnlineRewardPanel`/`OnlineRewardCell`/`OnlineRewardManager` prefabs (manager prefab carries the 18 sample rows), home Playtime button + red dot, granter handles `OnlineRewardClaimedEvent`. `RewardSampleBoot` opens the start panel one frame late — `UIPanel` applies `startHidden` inside its own `Start()`, so same-frame opens raced it.

## [0.5.0] - 2026-08-20

### Changed (breaking)

- Contract flip to ARCHITECTURE decisions #16-18: the package owns no data and grants nothing itself.
- Data: `RewardItem`, `RewardItemData` catalog, `DailyRewardRowData`, `LuckySpinRowData` (and their `[CreateAssetMenu]`s) are deleted. Each manager exposes a dev-filled `[TableList] public List<{Feature}Row> rows`; rows carry `Sprite Icon` inline (`DailyRewardRow`, `LuckySpinRow`). Lucky Spin tuning (`freeSpinCooldownSeconds`, `spinDurationSeconds`, `spinFullTurns`) and the weighted `Roll()` moved onto `LuckySpinManager`. The `"-"` LabelOverride sentinel is gone - empty string means no override.
- Grants: `IRewardGranter`, `RewardHooks.Granter`, and `RewardHooks.Catalog` are deleted. `StartClass({config}, hooks)` became `StartClass(hooks)`. `DailyRewardManager.Claim()` / `LuckySpinManager.FinishSpin()` mutate + save, `Debug.Log` the grant (mandatory audit trail), then raise `DailyRewardClaimedEvent { Day, Row }` / `SpinResultEvent { WedgeIndex, Row }` (payload now carries the full row instead of `RewardItem` + amount). Subscribing to the grant events is the host's grant path.
- Events: every raise allocates a fresh instance (cached mutated payloads removed); new `DailyRewardPanelClosedEvent` / `LuckySpinPanelClosedEvent`; event classes moved from `Core/RewardEvents.cs` into their feature folders.
- Validation: rows validate in `StartClass` (throws naming index/field) and in the manager's `OnValidate` (LogError, skipped while the list is still empty); the persisted `StreakDay` is range-checked against `rows` on load (LogError + reset).
- Panels: `SetInfo()` (populate + Show, overrides `BaseUI.SetInfo`) and parameterless `Close()` for Inspector `onClick` (decision #18). `DailyRewardCard.StartClass` / `LuckySpinWedge.StartClass` dropped the `RewardItem` parameter - the row carries the icon. `DailyRewardManager.GetItem` / `LuckySpinManager.GetItem` removed.
- Sample: config assets (`Data/Raw*.asset`) deleted - the demo scenes fill `rows` on the scene-placed managers. `SampleRewardGranter` is a plain static grant-event handler (PlayerPrefs economy kept); `SampleItemGrantedEvent` carries `{ Key, Icon, Amount }`; `ItemReceivedPanel`/`ItemReceivedCell` follow, and their catalog-based debug buttons take a `Sprite` parameter instead.

## [0.4.1] - 2026-08-20

### Added

- Sample: reward-received ceremony popup, the host-side "ceremony popup" recipe from INTEGRATION-GUIDE §4 — `SampleRewardGranter.Grant` raises `SampleItemGrantedEvent` (`RewardItem` + amount); `ItemReceivedPanel` (sorting 250, ported from paint-and-seek's `250_ItemReceivedPanel`) batches every grant raised in the same frame, stacks duplicates by key, and plays the dim/burst/title-rise/card-stagger ceremony (`ItemReceivedCell`, `ItemReceivedBurstFx`, `Art/ItemReceived` sprites). Wired as `250_ItemReceivedPanel` in `UIMainManager` and started from `RewardSampleBoot`. No Runtime change.

## [0.4.0] - 2026-08-19

### Changed (breaking)

- Data model reshaped for the NabaGame Googlesheet Importer (ARCHITECTURE decision #15): every table is `{Row}` POCO + `{Row}Data` ScriptableObject with a public `{row}s` list, so *Generate Assets* writes package config directly — no host bake.
- `RewardItem` is a plain `[Serializable]` row (`Key`, `DisplayName`, `Icon`), no longer a ScriptableObject; new `RewardItemData` catalog (`rewardItems`, `Get(key)` throws on unknown key) replaces one-asset-per-reward.
- `RewardHooks.Catalog` (`RewardItemData`) added and validated in `Validate()`.
- `DailyRewardConfig` → `DailyRewardRowData` (`dailyRewardRows` of `{Day, Key, Amount, LabelOverride, HideIconUntilClaim}`; `"-"` = no label override); `CardBackground` moved to `DailyRewardPanel.cardBackgrounds` (7 sprites). `DailyRewardManager.GetItem(day)`; `DailyRewardCard.StartClass(day, row, item, background, cb)`.
- `LuckySpinConfig` → `LuckySpinRowData` (`luckySpinRows` of `{Wedge, Key, Amount, Weight}` + cooldown/duration/turns); `LuckySpinManager.GetItem(index)`; `LuckySpinWedge.StartClass(index, row, item)`.
- Managers resolve keys through `hooks.Catalog` in `StartClass` and throw on unknown key, wrong `Day`/`Wedge` order, non-positive `Amount`/`Weight`.
- Sample data renamed to the importer's output names: `Data/RawRewardItem.asset`, `RawDailyReward.asset`, `RawLuckySpin.asset`; `RewardSampleBoot.rewardItemData` field. New `Documentation~/SHEET-TEMPLATE.md`.

## [0.3.0] - 2026-08-19

### Added

- `Runtime/Core/AdFlow`: rewarded-ad helper over the `ShowRewardedAd` hook (busy guard, editor-immediate reward, next-frame release).
- `Runtime/Core/RewardEvents`: `LuckySpinChangedEvent`, `SpinStartedEvent` (wedge index + duration), `SpinResultEvent` (wedge index, item, amount).
- `Runtime/Features/LuckySpin/`: `LuckySpinConfig` (weighted wedge rows, cooldown, spin duration/turns, editor-validated), `LuckySpinProfile`, `LuckySpinManager` (weighted roll resolved before the animation, free-spin cooldown through `TimeScheduler`, ad spin through `AdFlow`, grant on wheel stop), `LuckySpinPanel` (`BaseUI`, wind-up + ease-out spin onto the rolled wedge, pointer ticks, upright wedge content, cooldown countdown, ad-spin variant), `LuckySpinWedge` template widget.

### Changed

- Sample renamed `Samples~/DailyReward` -> `Samples~/RewardDemo` and now covers both features: `DailyRewardSample` + `LuckySpinSample` scenes, `LuckySpinPanel`/`LuckySpinWedge` prefabs, `LuckySpinConfig`, `SampleHomeButtons` (host-side red-dot recipe over package events), `RewardSampleBoot.startPanel`.

## [0.2.0] - 2026-08-19

### Added

- `Runtime/NabaGame.Reward.asmdef` (refs `com.bmh.core.runtime`, `com.nabagame.ui.runtime`, `UniTask`, `Unity.TextMeshPro`) — the package no longer sees `Assembly-CSharp`.
- `Runtime/Core/`: `RewardItem`, `IRewardGranter`, `RewardHooks` (+ `Validate`), `TimeScheduler` (ported from the host, renamespaced), `RewardProfileStore` + `RewardProfile` base (PlayerPrefs + JsonUtility, `Version` field), `DailyRewardChangedEvent`, `RewardAmountFormat`.
- `Runtime/Features/DailyReward/`: `DailyRewardConfig` (7 rows, editor-validated), `DailyRewardProfile`, `DailyRewardManager` (UTC-midnight rollover through `TimeScheduler`, no `Update()`), `DailyRewardPanel` (`BaseUI`), `DailyRewardCard`.
- `Samples~/DailyReward`: runnable demo scene, prefabs, sample `RewardItem`/config assets, `SampleRewardGranter`, currency HUD, `SampleUIRoot`. Registered in `package.json` `samples[]`.

### Notes

- `AdFlow` is deliberately absent: Daily Reward uses no ads. It arrives with Online Reward (ROADMAP phase 3).
- No `Editor/` assembly yet; package panels ship explicit Odin `[Button]` debug methods instead of an attribute processor, because `BaseUIInspectorProcessor` lives in `Assembly-CSharp` and cannot reach them.

## [0.1.0] - 2026-08-19

### Added

- Package skeleton: `package.json`, documentation set (`Documentation~/`: ARCHITECTURE, CONVENTIONS, INTEGRATION-GUIDE, ROADMAP, per-feature specs, RefUI mockups). No runtime code yet — see `Documentation~/ROADMAP.md`.
