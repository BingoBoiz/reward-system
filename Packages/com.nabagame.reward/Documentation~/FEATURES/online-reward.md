# Online Reward

**Status:** planned (Phase 3). A legacy *persistent* variant exists in the demo host as "Playtime" (`Assets/_GameBase/` — playtime region of `RewardManager`, `RewardPlaytimeTab`, `RewardPlaytimeSlot`); the package version changes it to **session-scoped** per the mockup (decision #9 in ARCHITECTURE.md).

**Mockup:** [../RefUI/online-reward.png](../RefUI/online-reward.png) — authoritative for layout/visuals; this doc is authoritative for behavior/data.

## What it is

A grid of rewards that unlock one after another on timers while the player keeps the game open. Watching rewarded ads activates x2/x5 time multipliers. Leaving the game resets the run — "REWARDS RESET IF YOU LEAVE!".

## UI spec (from mockup)

- Popup titled "REWARDS!" (gift icon) with subtitle banner "REWARDS RESET IF YOU LEAVE!", close (X) top-right.
- **3 rows × 5 columns** of reward cards, unlocking sequentially. Card anatomy: reward icon + amount label on top, countdown `mm:ss` (or `h:mm:ss`) below; timers shown on the mockup escalate from ~2 min to ~57 min.
  - **Claimable** — `CLAIM!` label (optionally a red badge), pulsing highlight.
  - **Counting** — dimmed with its remaining time.
  - Special rarity cards (RARE / EPIC frames with hero icons) at milestone positions, driven by config.
- Bottom buttons: `OPEN ALL` (green), `X2 SPEED` (purple, fast-forward icon), `X5 SPEED` (orange) — the speed buttons run the rewarded-ad flow.

## Config

`OnlineRewardRowData` (ScriptableObject, importer-shaped — sheet tab `OnlineReward`, A1 `OnlineRewardRow`): ordered rows of `{ int Slot, string Key, long Amount, int UnlockAfterSeconds }` (cumulative session time gates; row count drives the grid — mockup uses 15), plus speed-up settings `{ int X2DurationSeconds, int X5DurationSeconds, int X5AdsRequired }` on the SO. Keys resolve through `hooks.Catalog`. Validation: `Slot == index+1`, strictly increasing unlock times, keys present in the catalog.

## State & save

Session-scoped by design — the grid state lives in memory only:

- Session elapsed time uses the **baseline pattern** (ARCHITECTURE.md §5): `(realtimeSinceStartup - sessionStart) * activeMultiplier`, flushed on multiplier change; no per-frame accumulation, no `Update()` polling. Slot unlocks armed via `TimeScheduler`.
- On app quit (and, matching the mockup copy, on leaving to the point the session ends): unclaimed slots and elapsed time reset. `OnApplicationPause` handling (backgrounding vs kill) is specified precisely during Phase 3.
- PlayerPrefs key `NabaReward.Online` persists only what must survive a session: `Version` and cross-session counters if any survive design (e.g. partial x5 ad-watch count — to be decided in Phase 3; default is nothing but `Version`).

## API surface (planned)

`OnlineRewardManager`: `StartClass(config, hooks)`, `int SlotCount`, `SlotState GetState(int i)`, `(RewardItem, long) GetReward(int i)`, `double GetRemainingSeconds(int i)`, `bool HasClaimable`, `void Claim(int i)`, `void ClaimAll()`, `int SpeedMultiplier`, `void RequestSpeedUp(int multiplier)` (runs the ad flow via `AdFlow`).

## Events / hooks / placements

- Raises `OnlineRewardChangedEvent` on slot unlock, claim, and multiplier change.
- Hooks used: `Granter`, `PlaySfx`, `ShowRewardedAd` (all required).
- Placements: `OnlineReward_x2Speed`, `OnlineReward_x5Speed`.

## Verification script

1. Fresh session → slot 1 counts down from its config time; slots unlock strictly in order; `CLAIM!` appears on unlock and claiming grants through the host granter.
2. X2 then X5: timers visibly accelerate; multiplier stacking follows the spec'd rule; ad skip leaves the multiplier unchanged; editor path grants immediately.
3. Button spam on claim and speed buttons → single grant / single ad request (`AdFlow` busy guard).
4. Kill app / reopen → grid reset to the start (session-scoped), console clean.
5. Backgrounding briefly (`OnApplicationPause`) behaves per the Phase 3 spec; timers never jump from suspended wall-clock time.
6. Open/close panel repeatedly → no duplicated listeners; countdown labels stop when hidden.

## Open decisions (Phase 3)

- Exact end-of-session trigger (app kill only vs backgrounding beyond a grace period).
- x2/x5 stacking rule (legacy stacked to 7x — keep or simplify to highest-wins).
- Whether x5 requires multiple ads (legacy: 2) and whether that counter survives a session.
- OPEN ALL button semantics (claim all currently claimable vs ad-gated open-everything).
