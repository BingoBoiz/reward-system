# Daily Reward

**Status:** shipped in package `0.2.0` (2026-08-19); contract flip in `0.6.0`; Open All (ads/IAP) in `0.7.0`; **panel-owned rework shipped in `0.8.0` (2026-08-20, decisions #21–#27)** — the package `DailyRewardManager` is gone, `DailyRewardPanel` owns everything, the host writes a `SampleDailyRewardManager`-style manager.

**Mockup:** `Assets/_ASMR-Tower/Art/preview/daily.jpg` in the host repo (2400x1080, = the design resolution, so mockup pixels are canvas units 1:1). `../RefUI/daily-reward.png` is still absent. This doc is authoritative for behavior/data.

## What it is

A 7-day claim strip: once per (UTC) day the player claims the next card in the row; claimed cards stay marked, future cards stay locked. Rewards escalate across the week; day 7 is the hero reward.

## UI spec (from mockup)

- Popup with "DAILY!" title (calendar icon) and a close (X) button top-right.
- One horizontal row of 7 reward cards. Card anatomy: reward icon (from the row's `Icon`), amount label (formatted, e.g. `+7.5K`, `+200B`), and a state:
  - **Claimed** — dimmed/blue with a green check overlay.
  - **Claimable** — bright card with a `CLAIM` header; may carry a red notification badge.
  - **Locked** — `CLAIM` header shown but visually inert; mystery cards may hide the icon (silhouette / `???`) until claimed.
- Rarity-colored card frames (e.g. green RARE, orange EPIC) driven by config, not hardcoded.
- Under the row, `OpenAllRoot` holds **two OPEN ALL buttons stacked at the same position (the mockup's single-button spot, 440x105) — at most ONE is visible, picked by config**:
  - **IAP button** (blue, gift icon): label `OPEN ALL` + the dev-supplied price string; shown when `openAllIapProductId` is set and the week is not fully opened. **IAP config wins over ads.**
  - **Ads button** (green, video icon): label `OPEN ALL` + progress `X/N`; shown when IAP is NOT configured, `openAllAdsRequired > 0`, and the week is not fully opened.
  - When the whole week is opened (`UnopenedCount == 0`) both buttons hide and a static `COME BACK TOMORROW` label shows.

## Data (dev-filled)

The host's own manager (template: `Samples~/RewardDemo/Scripts/SampleDailyRewardManager.cs`) holds `[TableList] public List<DailyRewardRow> rows`, assigns each row's `OnClaimed`, and passes the list to `DailyRewardPanel.SetInfo(rows)` at boot.

Row — the one file the dev fills, list position is the day (`rows[0]` = day 1):
`{ string Key, Sprite Icon, long Amount, AudioClip ClaimSfx, string LabelOverride, bool HideIconUntilClaim, Action<DailyRewardRow> OnClaimed }` — a constructor documents the fill order for code authoring; empty `LabelOverride` = none.

Validation is lenient (decision #24): missing `Key`/`Icon`/`Amount` → one aggregated warning via `DailyRewardRow.Warn(rows)` (call it from your manager's `OnValidate` too); only a null/empty list throws. A row count other than 7 works — `cardBackgrounds` wrap around with a warning.

Panel knobs (`[SerializeField]` on the `DailyRewardPanel` prefab): `openAllAdsRequired` (0 = ads button off), `openAllIapProductId` ("" = IAP button off), `openAllIapPriceText` (display string), `cardBackgrounds` (prefab art, not data), spacing/stagger, `buttonSfx`.

## Save (PlayerPrefs key `NabaReward.Daily`)

| Field | Meaning |
|---|---|
| `Version` | payload version for migration |
| `StreakDay` | 0-based index of the next unclaimed day (0..7); `7` = week fully opened, resets to 0 on the next UTC day |
| `LastClaimDateUtc` | `yyyy-MM-dd` UTC of the last claim; claimable when today (UTC) differs |
| `OpenAllAdsWatched` | rewarded ads watched toward Open All; reset inside `OpenAll()`, carries across weeks until consumed |

Reset semantics: persists across sessions. Saved on every mutation — there is no pause/quit save pass.

## API surface — `DailyRewardPanel`, `#region API`

- `SetInfo(List<DailyRewardRow> rows)` — single init: validate, load save, arm the midnight rollover (`TimeScheduler`), build cards, bind listeners. Call from `Start()` at boot; the panel stays hidden.
- `OpenPanel()` / `ClosePanel()` — dev-facing activation (refresh + `Show()` / `Hide()` + `DailyRewardPanelClosedEvent`).
- Queries: `int DayCount`, `int StreakDay`, `int ClaimableCount` (0 or 1 today — the red-dot query), `int UnopenedCount`.
- `ResetProfile()` — QA/debug reset.
- Consts: `SaveKey`, `ProfileVersion`, `OpenAllPlacement`.

A claim advances the streak, saves, plays the row's `ClaimSfx`, `Debug.Log`s the grant, then invokes `Row.OnClaimed` — the host grants there; a null callback `LogError`s "was NOT granted". Open All (ads or IAP) claims every remaining day — one `OnClaimed` per day in the same frame, so a batching ceremony shows one popup.

## Events / hooks / placements

- **Grants: `Row.OnClaimed`** (decision #22) — no grant events exist.
- `DailyRewardChangedEvent` — notification, raised after claim, reset, ads-progress ticks, and at UTC-midnight rollover (red dots / refresh).
- `DailyRewardPanelClosedEvent` — notification, raised when the player closes the panel.
- Hooks used: `PlaySfx`, `ShowRewardedAd` (when `openAllAdsRequired > 0`), `PurchaseIap` (when `openAllIapProductId` is set; contract: the callback must fire on success, failure, and cancel). All optional — unset hooks LogError and proceed (decision #23).
- Placements: `DailyReward_OpenAll`.

## Verification script

1. Fresh install → day 1 claimable, days 2–7 locked. Claim → `OnClaimed` reaches the host handler (currency changes) and the grant log appears in the Console; card flips to claimed, `ClaimableCount == 0`.
2. Same day: no further claim possible; button spam grants exactly once.
3. Advance device/UTC date (or debug override) → next day claimable; red badge logic follows `ClaimableCount`.
4. Kill app / reopen → streak, claimed states, and ads progress restored from PlayerPrefs.
5. Open/close panel repeatedly → no duplicated listeners, no double grants.
6. Ads Open All: with `openAllAdsRequired = 3`, click the ads button 3 times → label 0/3 → 1/3 → 2/3 → the third completed ad claims all remaining days (one audit log + `OnClaimed` per day), buttons hide, `COME BACK TOMORROW` shows.
7. IAP Open All: click the IAP button → `RewardHooks.PurchaseIap` runs; `cb(true)` claims all remaining days, `cb(false)` logs and changes nothing, button stays clickable.
8. Claim today's card first, then Open All → only the remaining days grant (no double grant).
9. Claim day 7 singly → whole week renders claimed, Open All buttons hide same-day; next UTC day resets to day 1.
10. Both configs off → no Open All buttons, single-card claim unaffected; unset ad/IAP hooks only LogError.
11. Null-button pass: disable/delete `openAllAdsButton`, `openAllIapButton`, `comeBackLabel`, or a card's sub-widgets → no exception, panel still opens, claims still work.
12. Clear one row's `OnClaimed` → claiming it logs `was NOT granted` and grants nothing; everything else unaffected.

## Decisions

- **OPEN ALL (reworked 2026-08-20):** Open All claims **every remaining day of the displayed week at once**, gated behind ads or IAP. Two buttons share the mockup's single-button position and **only one shows at a time, driven by data: IAP config (`openAllIapProductId`) wins; otherwise the ads button shows when `openAllAdsRequired > 0`** — never both. The package itself runs the ad flow / calls the IAP hook — the dev supplies only `openAllAdsRequired`, `openAllIapProductId`, `openAllIapPriceText`. The visible button stays available after today's card was tapped; it hides only when the week is fully opened.
- **Post-day-7 (reworked 2026-08-20):** claiming day 7 (or Open All) sets `StreakDay = 7` — the whole week renders claimed for the rest of the day and Open All is gone. The reset to day 1 happens on the next UTC day (`ResetWeekIfElapsed`). The old `% 7` wrap was an exploit: it made the week look unclaimed again immediately.
- **Ads progress:** `OpenAllAdsWatched` persists and carries across week cycles until consumed by `OpenAll()`; it is not reset at rollover.
- **Streak break:** missing a day does not reset the streak. The player simply resumes at the next unclaimed day. `ClaimableCount` is 1 whenever `StreakDay < DayCount` and `LastClaimDateUtc != today (UTC)`.

## Shipped deviations from the mockup

- The ASMR-Tower art set has no gem icon, so the 7-day table is built from the icons that exist (money / lucky-spin / no-ads); the Open All buttons use `checkpoint_0002_button-green` / `checkpoint_0003_button-blue` 9-sliced to 440x105 (~the mockup's button proportions).
- The mockup shows a single OPEN ALL button; the panel matches that — one button visible at the mockup position, its ads/IAP variant picked by config (decision above).
- Locked cards render at full opacity like the mockup; claimable is signalled by the red badge plus a scale pulse.
