# Changelog

All notable changes to this package are documented here. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning: [SemVer](https://semver.org/).

## [0.12.0] - 2026-08-21

Monetization reliability pass, calibrated against the shipped `com.bmh.ads` (AppLovin MAX) + Unity IAP stack that `dress-to-impress` runs on. Every rewarded ad and every purchase now resolves exactly once and always tells the player what happened. Breaking (`AdFlow` deleted, two hooks added).

Also in this release: **one clock**. Every wall-clock and monotonic read in the package goes through a single `RewardClock` static, and countdown labels tick exactly when their displayed digit changes — which fixes the x2/x5 speed-up skipping numbers (decision #34). Breaking (two static members moved).

### Changed (breaking)

- **`AdFlow` is replaced by `RewardFlow`** (decision #31), which guards purchases as well as ads. `iapBusy` is gone from `DailyRewardPanel` and `OnlineRewardPanel` — an IAP whose callback never fired used to brick the OPEN ALL button for the whole session, because only the ad path had a failsafe. `RewardFlow` also clears `Busy` **before** invoking the callback, so a `RefreshAll()` from inside the reward callback paints the live state instead of the disabled one (Daily has no countdown loop, so it stayed stale until the next change event).
- **`TimeScheduler.NowMs` and `TimeScheduler.SecondsUntil` moved to `RewardClock`** (`Runtime/Core/RewardClock.cs`). `TimeScheduler` keeps only `Schedule`/`Cancel`/`Handle` and sweeps its deadlines against `RewardClock.NowMs`. Hosts that read the clock through the scheduler replace the type name; nothing else changes.

### Fixed

- **A rewarded ad that never shows no longer freezes the button in silence** (decision #32). The shipped SDK discards `ShowRewardedVideo`'s `skip` argument and returns without calling anything when the reward is interval-throttled (`rewardAdsIntervalTime`, **10s** by default), when nothing is loaded, when the user skips, or when display fails — only a granted reward calls back. The old 15s `Busy` timeout released the guard quietly and no panel ever passed an `onSkip`, so the second ad of any multi-ad flow (Daily/Online OPEN ALL, x5 speed-up) looked like a dead button with an unmoving counter. `RewardFlow` now infers the outcome from app focus: no focus loss within ~3s means the ad never opened, focus regained with no reward means it was skipped. Either way the panel refreshes and raises `RewardHooks.ShowMessage`.
- `LuckySpinPanel.SpinByAd` ignored `Show()`'s return value and always reported success; it now returns what the flow returns. `ClosePanel` and `RefreshAll` also treat an in-flight ad like a spin in progress — closing mid-ad used to spin the wheel on a hidden panel.
- `DailyRewardPanel.RefreshAll` greys the OPEN ALL buttons while a request is in flight, matching `OnlineRewardPanel`.
- A resolve whose owning panel was destroyed mid-ad drops its callbacks instead of running against a dead MonoBehaviour.
- `INTEGRATION-GUIDE` §6 documented `IAPManager.Instance.InitiatePurchase(productId, callback)`, which does not exist in the consumer project — the real entry point is `PurchaseProduct(productId, onSuccess, onFailed)`. The snippet now compiles, and carries the four production notes that stack needs (silent ad failures, `ResetLastShowOpenAds()` before `InitiatePurchase`, remove-ads must keep rewarded alive, push the interstitial clock after a rewarded view).
- **x2/x5 speed-up skipped digits** (59 → 54 → 49 at ×5): slot timers now count 59, 58, 57 at five ticks per real second (seven at ×7), and drop back to one per second when the buff expires with no jump.
- At ×1 the old loops drifted against the display boundary by frame granularity and silently skipped a digit about once a minute; the aligned delay removes that too.
- `DailyRewardPanel` read `DateTime.UtcNow` on its own (two separate reads, no culture) — `TodayUtc` and the midnight deadline now come from `RewardClock`, and `SetInfo`/`OnRollover` arm the rollover **before** evaluating the week reset, so a midnight passing between the two reads fires on the next scheduler sweep instead of being lost until the next launch.

### Added

- **`RewardHooks.GetIapPrice`** (`Func<string, string>`, productId → localized store price; default `""`). Read on every refresh by both Open All panels, with the panel's `openAllIapPriceText` demoted to the pre-store-init fallback (decision #33 supersedes the price half of #20). A hard-coded `$4.99` is the wrong currency everywhere outside the US. Adapter is one line onto `IAPManager.GetProductPrice`.
- **`RewardHooks.ShowMessage`** (`Action<string>`; default `LogWarning`) plus the constants `AdNotAvailableMessage` / `AdSkippedMessage` / `PurchaseFailedMessage`, so a host can compare and show its own localized copy rather than the English default.
- Sample: `SampleRewardBoot`'s fake ad is now **asynchronous** and switchable through an `AdResult` enum (`Reward` / `Skip` / `Swallow`) — `Swallow` reproduces the production throttle that answers with nothing. A `fakeIapPrice` field exercises `GetIapPrice`. The old synchronous fake resolved inside `Show()` and hid exactly the ordering bugs fixed above.
- **`RewardClock`** — the package's only clock (decision #34): `NowMs`, `UtcNow`, `TodayUtc` (the `yyyy-MM-dd` save-file day key, now formatted with `CultureInfo.InvariantCulture`), `NextUtcMidnightMs`, `SecondsUntil`, `MonotonicSeconds` (`Time.realtimeSinceStartupAsDouble`, for accrual baselines), and `MsUntilNextTick(remainingSeconds, rate)` — realtime ms until a ceil-displayed countdown shows its next value. No other Runtime file reads `DateTime*` or `Time.realtimeSinceStartup*`, so a future fake/server time is a one-file change. A host daily-quest or shop-refresh system can use the same members.
- **Boundary-aligned countdowns.** `OnlineRewardPanel` and `LuckySpinPanel` replaced their fixed `UniTask.Delay(1000, ignoreTimeScale)` refresh with a `DelayType.Realtime` delay recomputed each iteration from `RewardClock.MsUntilNextTick`. Online wakes at whichever changes first — a slot digit (playtime rate = active multiplier) or a booster digit (wall rate) — and restarts its loop from `RaiseChanged()` so a multiplier change mid-sleep cannot jump digits.

## [0.11.0] - 2026-08-20

Red dots leave the package. The three panels no longer render or reference a badge anywhere — reading panel, card, or cell code shows no trace of one. Every dot is host-side now, and the sample ships a self-driven component that does the whole job. Breaking (three serialized fields removed).

### Changed (breaking)

- **`DailyRewardCard.badge`, `OnlineRewardCell.badge` and `LuckySpinPanel.spinBadge` are deleted.** The package draws no notification badge, not even inside its own panels (decision #30). Consumer prefabs keep their badge GameObjects but lose those wirings — put `SampleRedDot` on each one instead. Panels only carry a one-line pointer comment in the `#region API`.
- `SampleHomeButtons` is now just three buttons: its `dailyBadge`/`spinBadge`/`playtimeBadge` fields, its `Refresh()`, and its event subscriptions are gone — each dot subscribes for itself.

### Added

- **`SampleRedDot`** (`Samples~/RewardDemo/Scripts`): drop it on a dot `Image`, pick a `SampleRedDotKey` (`DailyReward`, `LuckySpin`, `OnlineReward`, `DailyRewardCard`, `OnlineRewardCell`, or `None` for manual `SetOn(bool)`), and it subscribes to the three `{Feature}ChangedEvent`s, evaluates the panel query, and animates itself. The card/cell keys resolve their own index from the `DailyRewardCard`/`OnlineRewardCell` parent, so one component covers per-item dots too. The bell-ring tween (tilt+grow strike → `OutElastic` settle → rest interval → loop) is ported 1:1 from the consumer project's `RedDotView`, values included.
- `DailyRewardPanel.GetState(int day)` and `OnlineRewardPanel.GetState(int slot)` are now **public, guarded API-region queries** (return `Locked` before `SetInfo` or for an out-of-range index) — the per-card/per-cell state a host dot needs, named neutrally. No new method: the existing private ones moved up.
- **Every `SetInfo(rows)` now ends with `RaiseChanged()`** — loading the profile *is* a state change, and without the raise a listener created before boot (any red dot, any HUD) sat on its empty initial reading until the next claim or timer tick. `SampleHomeButtons` used to hide this because boot called its `Refresh()` last.

## [0.10.0] - 2026-08-20

Online Reward config parity with Daily Reward: every monetized action on the panel is now an Inspector knob instead of a hard-coded constant. Breaking (one serialized field renamed).

### Changed (breaking)

- **`OnlineRewardPanel.openAllButton` is replaced by the four-field Open All cluster** `openAllAdsButton` / `openAllAdsCountLabel` / `openAllIapButton` / `openAllIapPriceLabel` — the same shape `DailyRewardPanel` already had. Consumer prefabs must author both buttons at the same spot and rewire; the demo `OnlineRewardPanel.prefab` ships them under a new `OpenAllRoot` (green + video icon + `n/required` counter, blue + gift icon + price label), positioned and 9-sliced like Daily's pair.

### Added

- `OnlineRewardPanel` Config tab knobs: **`x2AdsRequired`** (default 1) — the x2 booster's ad requirement was previously hard-coded to a single ad, x5's was already serialized — plus the Daily-style Open All set **`openAllUseAds`** (default on), **`openAllAdsRequired`** (default 1), **`openAllIapProductId`**, **`openAllIapPriceText`**. OPEN ALL now runs either a multi-ad flow or an IAP purchase, picked by the bool; the inactive mode's flow is refused (`RequestOpenAllAds` / `RequestOpenAllIap` gate on it) and exactly one button is ever visible.
- `WarnConfig()` runs from `SetInfo` and warns (never throws, per the leniency ladder) when the active mode's config is empty: a non-positive `x2AdsRequired` / `x5AdsRequired` turns that booster off, a non-positive `openAllAdsRequired` or an empty `openAllIapProductId` turns OPEN ALL off, and a set product id with no price text warns on its own.
- `OnlineRewardProfile` gains `SpeedUpX2Ads` and `OpenAllAdsWatched` alongside `SpeedUpX5Ads`, so every partial ad-watch counter survives a session. `ResetSession()` clears all three; activating a booster or completing OPEN ALL clears its own counter.
- Debug tab: `Open All Ads` and `Open All IAP` buttons replace the single `Open All`.

### Changed

- The demo `OnlineRewardPanel.prefab` sets `openAllUseAds` on with `openAllAdsRequired = 3` (matching Daily's demo, so the counter is exercised), `x2AdsRequired = 1` (the previous hard-coded behavior), and fills `openAllIapProductId` / `openAllIapPriceText` with `com.nabagame.sample.onlineopenall` / `$4.99` so flipping the bool works without further wiring.
- `AddSpeedUpAd` / `ActivateSpeedUp` no longer special-case x5 — both boosters count and clear their own persisted ad counter through a shared `SetSpeedUpAds`.

## [0.9.0] - 2026-08-20

Fixed-board refactor (ARCHITECTURE decision #28): the two fixed-count boards are now inspector-authored. Breaking.

### Changed (breaking)

- **Daily's 7 cards and Spin's 8 wedges are no longer instantiated from a template at runtime** — they are pre-authored nested prefab instances inside the panel prefab, wired in order into a serialized `List<>` (`DailyRewardPanel.cards`, `LuckySpinPanel.wedges`). Card background sprites are authored per-instance on each card's Image (the panel no longer pushes them), so layout and skins are prefab concerns: rearranging the 7 cards into a 3+3+big-day-7 grid takes zero C#. The demo prefabs keep the exact previous layouts (horizontal strip at 289px spacing; wedges polar at radius 208).
- `SetInfo(rows)` now **binds** rows onto the authored list instead of building clones: count mismatch warns and binds the min (surplus authored entries are deactivated), an empty authored list LogErrors and skips (panel logic still runs), null list entries are skipped silently (#26), and re-entry rebinds cleanly — a later `SetInfo` with more or fewer rows shows/hides the right cards (the old one-shot `built` latch stranded them).
- `DailyRewardCard.SetInfo(int day, DailyRewardRow row, Action<int> clickedCallback)` — the `Sprite cardBackground` parameter is gone.
- Spin's art-mismatch warning now compares `rows.Count` against the authored wedge list (`wheelSegmentCount` is gone — the authored count *is* the segment count). If a host passes more rows than authored wedges, the wheel can visually land on a wrapped wedge while the grant (`rows[index]`) stays correct — the warning names the mismatch up front.
- Online Reward cells and the sample's ItemReceived pool stay template-instantiated: their counts are genuinely dynamic (host row data / grant queue).

### Removed

- `DailyRewardPanel`: `cardsRoot`, `cardTemplate`, `cardBackgrounds`, `cardSpacing` serialized fields; `LuckySpinPanel`: `wedgesRoot`, `wedgeTemplate`, `wedgeRadius`, `wheelSegmentCount`; `DailyRewardCard`: the `background` Image reference (the root Image renders its authored sprite untouched).

### Changed

- Daily Open All mode is now an explicit Inspector bool **`openAllUseAds`** on `DailyRewardPanel` (Config tab, default on): on shows the ads button, off shows the IAP button — replacing the 0.8.1 "IAP config wins over ads" inference, so switching modes no longer requires clearing `openAllIapProductId`. The inactive mode's flow is refused (`RequestOpenAllAds`/`RequestOpenAllIap` gate on the bool), and `WarnConfig` warns when the active mode's config is empty. The sample `DailyRewardPanel.prefab` sets it on, so the demo now shows the ads OPEN ALL (3 ads) instead of the IAP one.
- Panel inspectors reorganized into one Odin `TabGroup` on `DailyRewardPanel`, `LuckySpinPanel`, and `OnlineRewardPanel`, same tabs in the same order on all three: `UI` (references first), `Config`, `Data` (read-only host `rows` preview + the inline `Profile` save-state view), `FX`, and `Debug` (parameterless preview buttons packed into horizontal `ButtonGroup` rows, parameterized ones full-width). Crowded tabs keep `FoldoutGroup` sub-sections (Daily `Open All`/`Cards`, Spin `Spin Button`/`Wheel`/`SFX`). Each serialized field also carries a short Vietnamese `//` comment (under 7 words) describing what to fill in — a deliberate, protected exception to the English-comments rule (see CONVENTIONS.md). No behavior or serialization change; existing prefab values are untouched.
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
