# Online Reward

**Status:** shipped in `0.6.0` (Phase 3); **panel-owned rework shipped in `0.8.0` (2026-08-20, decisions #21–#27)** — the package `OnlineRewardManager` is gone, `OnlineRewardPanel` owns everything, the host writes a `SampleOnlineRewardManager`-style manager. The package version is **session-scoped** (decision #9 in ARCHITECTURE.md).

**Mockup:** `Assets/_ASMR-Tower/Art/preview/playtime reward.jpg` (host repo) — authoritative for layout/visuals (`../RefUI/online-reward.png` is still awaiting the exported image); this doc is authoritative for behavior/data.

## What it is

A grid of rewards that unlock one after another on timers while the player keeps the game open. Watching rewarded ads activates x2/x5 time multipliers, or opens the whole grid at once. Leaving the game resets the run — "Reward reset if you leave!".

## UI spec (from mockup)

- Popup titled "REWARDS!" (gift icon) with subtitle "Reward reset if you leave!", close (X) top-right, dark board over a dimmed screen.
- Grid of reward cards (mockup and sample default: **3 rows × 6 columns = 18**; row count drives the grid via `GridLayoutGroup`). Card anatomy: colored frame (per-column variety from `frameSprites`), amount label on top, reward icon center, countdown `mm:ss` (or `h:mm:ss`) below.
  - **Claimable** — `CLAIM!` label, pulsing highlight. The red dot on the cell is host-side (`SampleRedDot`, key `OnlineRewardCell`), not part of the cell.
  - **Counting** — dimmed with its remaining time.
  - **Claimed** — check mark, icon tinted down, non-interactable.
- Bottom buttons: `OPEN ALL`, `X2 SPEED` / `X5 SPEED` (rainbow, fast-forward icon, ad badge; each shows an `n/required` ad counter when its requirement is above 1). While a speed-up is active its button shows the remaining buff time and is non-interactable; slot timers keep counting one displayed second at a time (five ticks per real second at ×5), never skipping digits.
- `OPEN ALL` occupies one spot with **two authored buttons stacked at the same position** — `OpenAllAdsButton` (green, video icon, `n/required` counter) and `OpenAllIapButton` (blue, gift icon, price label). Exactly one is visible, picked by `openAllUseAds`; the same shape as Daily Reward's OPEN ALL.

## Data (dev-filled)

The host's own manager (template: `Samples~/RewardDemo/Scripts/SampleOnlineRewardManager.cs`) holds `[TableList] public List<OnlineRewardRow> rows`, assigns each row's `OnClaimed`, and passes the list to `OnlineRewardPanel.SetInfo(rows)` **at boot** — playtime accrues from that call, so never init lazily on first open.

Row — list position is the slot (`rows[0]` unlocks first):
`{ string Key, Sprite Icon, long Amount, int UnlockAfterSeconds, AudioClip ClaimSfx, Action<OnlineRewardRow> OnClaimed }` — cumulative session-time gates, strictly increasing; a constructor documents the fill order.

Validation: missing `Key`/`Icon`/`Amount` → one aggregated warning via `OnlineRewardRow.Warn(rows)`; an empty list or non-increasing `UnlockAfterSeconds` throws (the timer machine cannot run with it).

Panel knobs (`[SerializeField]` on the `OnlineRewardPanel` prefab), Config tab:

| Knob | Default | Meaning |
|---|---|---|
| `x2DurationSeconds` | 120 | how long the x2 buff runs |
| `x2AdsRequired` | 1 | ads needed to activate x2 (counter persisted, shown as `n/required` when above 1) |
| `x5DurationSeconds` | 120 | how long the x5 buff runs |
| `x5AdsRequired` | 2 | ads needed to activate x5 |
| `openAllUseAds` | on | on shows the ads OPEN ALL button, off shows the IAP one |
| `openAllAdsRequired` | 1 | ads needed for OPEN ALL when `openAllUseAds` is on |
| `openAllIapProductId` | "" | IAP product for OPEN ALL when `openAllUseAds` is off |
| `openAllIapPriceText` | "" | fallback price on the IAP button; `RewardHooks.GetIapPrice` wins when the store answers |

FX tab: `cellStaggerDelay`, `buttonSfx`. The sample fills 18 rows on `Prefabs/SampleOnlineRewardManager.prefab`.

`WarnConfig()` runs inside `SetInfo` and warns (never throws) when the active mode's config is empty: `x2AdsRequired`/`x5AdsRequired` at or below 0 turns that booster off, `openAllUseAds` on with `openAllAdsRequired` at or below 0 turns OPEN ALL off, `openAllUseAds` off with an empty `openAllIapProductId` does the same, and a set product id with an empty price text warns on its own.

## State & save

Session-scoped by design — the grid state lives in memory only:

- Session elapsed time uses the **baseline pattern** (ARCHITECTURE.md §5): `accumulated + (RewardClock.MonotonicSeconds - baseline) * activeMultiplier`, flushed at the old rate before any multiplier change; no per-frame accumulation, no `Update()` polling. The nearest locked slot is armed via `TimeScheduler` (deadline scaled by the active multiplier); a second handle arms the earliest buff expiry.
- **Focus loss flushes + saves; focus gain resets the baseline** so suspended wall-clock time never counts as playtime. The panel listens to the static `Application.focusChanged`, not `OnApplicationPause` — the static event fires even when a host UI framework deactivates the hidden panel GameObject. Buff end-times are wall-clock, so buffs may expire while suspended. App kill resets the whole grid (decision: backgrounding does not end the session, only the kill does). In-editor, alt-tab counts as focus loss — alt-tabbed time does not accrue.
- When every slot is claimed the cycle resets (claimed cleared, elapsed time zeroed, timers restart) so the panel never goes dead — matching legacy.
- PlayerPrefs key `NabaReward.Online` (Version 1) persists only `Version`, `SpeedUpX2Ads`, `SpeedUpX5Ads`, and `OpenAllAdsWatched` — the partial ad-watch counters survive sessions; buffs and grid state do not. `ResetSession()` clears all three.

## API surface — `OnlineRewardPanel`, `#region API`

- `SetInfo(List<OnlineRewardRow> rows)` — single init: validate, load save, start accrual, arm unlock/buff deadlines, build cells, bind listeners. **Call from `Start()` at boot.**
- `OpenPanel()` / `ClosePanel()` — dev-facing activation. A UniTask loop refreshes countdown labels and booster buttons only while visible, waking at the next displayed-digit boundary (`RewardClock.MsUntilNextTick`, `DelayType.Realtime`) so a ×5 buff counts 59, 58, 57… instead of skipping; every state change restarts the cadence.
- Queries: `int SlotCount`, `bool HasClaimable` (the red-dot query), `OnlineSlotState GetState(int slot)` (guarded; `Locked` before `SetInfo` or out of range).
- `ResetSession()` — QA/debug reset.
- Consts: `SaveKey`, `ProfileVersion`, `SpeedUpX2`, `SpeedUpX5`, `OpenAllPlacement`, `X2Placement`, `X5Placement`.

A claim marks the slot, plays the row's `ClaimSfx`, `Debug.Log`s the grant, then invokes `Row.OnClaimed` — the host grants there. OPEN ALL claims every unclaimed slot in one frame — one `OnClaimed` per slot, so a batching ceremony shows one popup — gated by either `openAllAdsRequired` rewarded ads (counter persisted) or the `openAllIapProductId` purchase, whichever `openAllUseAds` selects; the inactive mode's flow is refused outright. X2/X5 run the ad flow (`RewardFlow`; each needs its own `x2AdsRequired`/`x5AdsRequired` ads, counters persisted), stack to ×7, and expire on wall-clock deadlines.

## Events / hooks / placements

- **Grants: `Row.OnClaimed`** (decision #22) — raised per claimed slot (single claim and each slot of OPEN ALL, same frame); no `OnlineRewardClaimedEvent` exists.
- `OnlineRewardChangedEvent` — notification, raised on slot unlock, claim, cycle reset, and multiplier change.
- `OnlineRewardSpeedUpEvent { int Multiplier; }` — notification, raised when a speed-up activates after the ad flow.
- `OnlineRewardPanelClosedEvent` — notification, raised when the player closes the panel.
- Hooks used: `PlaySfx`, `ShowRewardedAd`, `PurchaseIap` (only when `openAllUseAds` is off). Optional — unset hooks LogError and proceed (decision #23).
- Placements: `OnlineReward_x2Speed`, `OnlineReward_x5Speed`, `OnlineReward_OpenAll`.

## Verification script

1. Fresh session → slot 1 counts down from its row time; slots unlock strictly in order; `CLAIM!` appears on unlock and claiming reaches the host's `OnClaimed` handler (currency changes, grant log in Console, ceremony popup).
2. X2 then X5: timers visibly accelerate; x2+x5 stack to ×7; each booster needs its configured ad count (`n/required` counter, persisted, hidden when the requirement is 1); ad skip leaves the multiplier unchanged; editor path grants immediately.
3. Button spam on claim and speed buttons → single grant / single ad request (`RewardFlow` busy guard); a skipped, throttled or unavailable ad raises a `ShowMessage` notice and leaves the counter untouched (ARCHITECTURE §8).
4. OPEN ALL with `openAllUseAds` on → `openAllAdsRequired` ads (counter climbs on the button), every unclaimed slot claimed in one frame (one batched ceremony), then the cycle resets and the counter clears. Flip `openAllUseAds` off with a product id set → the blue IAP button with its price replaces the green one, a cancelled purchase grants nothing and logs, a successful one opens all.
5. Kill app / reopen → grid reset to the start (session-scoped), console clean.
6. Background / drop focus for minutes — **with the panel hidden and even with `disableWhenHidden` ticked** — return → timers never jump from suspended wall-clock time (`Application.focusChanged` flush).
7. Open/close panel repeatedly → no duplicated listeners; the countdown loop stops when hidden, including via a bare `Hide()`.
8. Null-button pass: disable/delete `openAllAdsButton`, `openAllIapButton`, either open-all label, a booster's `button`/`label`/`adsCountLabel`, or `cellTemplate` → no exception, no stuck state (missing template = empty panel + one error).

## Resolved decisions (2026-08-20)

- End-of-session trigger: **app kill only** — backgrounding pauses accrual (baseline reset on focus regained) but keeps the grid.
- x2/x5 stacking: **legacy rule kept** — both active means playtime ticks ×7.
- X5 ads: **2 ads required** (`x5AdsRequired`, serialized); the partial counter persists in `NabaReward.Online`.
- X2 ads: **serialized like X5** (`x2AdsRequired`, default 1) instead of the hard-coded single ad; its partial counter persists too, and the `n/required` badge hides itself at a requirement of 1.
- OPEN ALL: **ads or IAP, same as Daily Reward** — `openAllUseAds` picks the mode, the prefab authors both buttons at the same spot and the panel shows exactly one (placement `OnlineReward_OpenAll`, product `openAllIapProductId`). Locked slots are included either way.
- Pause handling: **`Application.focusChanged` replaces `OnApplicationPause`** (0.8.0) — Unity messages die on a deactivated GameObject; the static event does not, so the no-suspended-playtime promise holds regardless of how the host hides the panel.
