# Daily Reward

**Status:** shipped in package `0.2.0` (2026-08-19). The legacy host implementation (daily region of `RewardManager`, `RewardDailyTab`, `RewardDailyItem`) is now superseded and is deleted in ROADMAP phase 5.

**Mockup:** `Assets/_ASMR-Tower/Art/preview/daily.jpg` in the host repo (2400x1080, = the design resolution, so mockup pixels are canvas units 1:1). `../RefUI/daily-reward.png` is still absent. This doc is authoritative for behavior/data.

## What it is

A 7-day claim strip: once per (UTC) day the player claims the next card in the row; claimed cards stay marked, future cards stay locked. Rewards escalate across the week; day 7 is the hero reward.

## UI spec (from mockup)

- Popup with "DAILY!" title (calendar icon) and a close (X) button top-right.
- One horizontal row of 7 reward cards. Card anatomy: reward icon (from `RewardItem.Icon`), amount label (formatted, e.g. `+7.5K`, `+200B`), and a state:
  - **Claimed** — dimmed/blue with a green check overlay.
  - **Claimable** — bright card with a `CLAIM` header; may carry a red notification badge.
  - **Locked** — `CLAIM` header shown but visually inert; mystery cards may hide the icon (silhouette / `???`) until claimed.
- Rarity-colored card frames (e.g. green RARE, orange EPIC) driven by config, not hardcoded.
- `OPEN ALL` button (gift icon) under the row — see Open decisions.

## Config

`DailyRewardRowData` (ScriptableObject, importer-shaped — sheet tab `DailyReward`, A1 `DailyRewardRow`): 7 ordered rows of `{ int Day, string Key, long Amount, string LabelOverride, bool HideIconUntilClaim }`; `LabelOverride == "-"` = none. Keys resolve through `hooks.Catalog` (`RewardItemData`) at `StartClass`. Validation (editor + StartClass): exactly 7 rows, `Day == index+1`, no empty key, `Amount > 0`, key present in the catalog. Card backgrounds are `DailyRewardPanel.cardBackgrounds` (prefab art).

## Save (PlayerPrefs key `NabaReward.Daily`)

| Field | Meaning |
|---|---|
| `Version` | payload version for migration |
| `StreakDay` | 0-based index of the next unclaimed day (0..7) |
| `LastClaimDateUtc` | `yyyy-MM-dd` UTC of the last claim; claimable when today (UTC) differs |

Reset semantics: persists across sessions. Cycle behavior after day 7 (restart vs hold) is specified during Phase 2.

## API surface (planned)

`DailyRewardManager`: `StartClass(config, hooks)`, `int ClaimableCount` (0 or 1 today), `DayState GetState(int day)`, `DailyRewardRow GetRow(int day)`, `RewardItem GetItem(int day)`, `void Claim()`. Midnight rollover armed via `TimeScheduler`; raises the change event on rollover and claim.

## Events / hooks / placements

- Raises `DailyRewardChangedEvent` after claim and at UTC-midnight rollover.
- Hooks used: `Granter` (required), `PlaySfx` (required). No ads unless Open All becomes ad-gated (see below).
- Placements: none yet.

## Verification script

1. Fresh install → day 1 claimable, days 2–7 locked. Claim → grant reaches the host granter, card flips to claimed, `ClaimableCount == 0`.
2. Same day: no further claim possible; button spam produces exactly one grant.
3. Advance device/UTC date (or debug override) → next day claimable; red badge logic follows `ClaimableCount`.
4. Kill app / reopen → streak and claimed states restored from PlayerPrefs.
5. Open/close panel repeatedly → no duplicated listeners, single event subscription.

## Decisions (resolved 2026-08-19)

- **OPEN ALL** claims the one card that is claimable today — a shortcut for tapping the card itself. It is not catch-up and not ad-gated. When today's card is already taken the button goes non-interactable, dims, and reads "COME BACK TOMORROW".
- **Post-day-7:** the week restarts — `StreakDay = (StreakDay + 1) % 7`, matching the legacy `RewardManager.ClaimDaily`.
- **Streak break:** missing a day does not reset the streak. The player simply resumes at the next unclaimed day. `ClaimableCount` is 1 whenever `LastClaimDateUtc != today (UTC)`.

## Shipped deviations from the mockup

- The ASMR-Tower art set has no gem icon and no long green button, so the 7-day table is built from the icons that exist (money / lucky-spin / no-ads) and `OPEN ALL` uses `checkpoint_0002_button-green` at its native 2.68 aspect (440x164) instead of the mockup's ~508x98.
- Locked cards render at full opacity like the mockup; claimable is signalled by the red badge plus a scale pulse.
