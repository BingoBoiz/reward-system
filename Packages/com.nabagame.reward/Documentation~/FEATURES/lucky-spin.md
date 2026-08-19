# Lucky Spin

**Status:** built (package 0.3.0, 2026-08-19) — `Runtime/Features/LuckySpin/`, demo in `Samples~/RewardDemo/Scenes/LuckySpinSample.unity`. Reference art: `Assets/_ASMR-Tower/Art/preview/spin.jpg` (8-segment wheel `spin_0001_vong-quay`, pin pointer `spin_0000_kim-xoay`).

**Mockup:** [../RefUI/lucky-spin.png](../RefUI/lucky-spin.png) — authoritative for layout/visuals; this doc is authoritative for behavior/data.

## What it is

A weighted prize wheel. One free spin per cooldown window; additional spins by watching a rewarded ad. The wheel decelerates onto the rolled wedge and the reward is granted through the host.

## UI spec (from mockup)

- Popup with the wheel centered, close (X) top-right.
- **Wheel of N wedges** (count driven by config data; the shipped ASMR-Tower wheel art has 8 segments and the panel logs an error when config and art disagree), each wedge showing a reward icon + amount label (`1.5K`, `100B`, `X10`, …); one highlighted jackpot wedge (red, hero icon) allowed via rarity styling. Golden rim with dot lights, white center hub, fixed pointer at 12 o'clock.
- Spin button states below the wheel:
  - **Free spin available** — green `SPIN` button (may carry a red badge).
  - **Cooldown** — ad-spin variant: `SPIN` button with a video icon and caption `Free spin in mm:ss` counting down to the next free spin.
- During a spin all buttons lock; the wheel accelerates then decelerates onto the result wedge (DOTween ease-out); result lands with feedback (SFX hook + grant).

## Config

`LuckySpinRowData` (ScriptableObject, importer-shaped — sheet tab `LuckySpin`, A1 `LuckySpinRow`): ordered wedge rows `{ int Wedge, string Key, long Amount, int Weight }` (row count = wedge count; wheel visuals generated from rows), plus `FreeSpinCooldownSeconds`, `SpinDurationSeconds`, `SpinFullTurns` (kept across re-import). Keys resolve through `hooks.Catalog`. Validation (editor + StartClass): ≥ 2 rows, `Wedge == index+1`, no empty key, `Amount > 0`, `Weight > 0`, key present in the catalog.

## Save (PlayerPrefs key `NabaReward.Spin`)

| Field | Meaning |
|---|---|
| `Version` | payload version |
| `NextFreeSpinAtMs` | unix ms deadline of the next free spin (0 = available now); survives kill/reopen via wall clock |

## API surface

`LuckySpinManager`: `StartClass(config, hooks)`, `bool FreeSpinReady`, `double SecondsUntilFreeSpin` (from `TimeScheduler.SecondsUntil`), `bool IsSpinning`, `bool CanSpinByAd`, `bool SpinFree()`, `bool SpinByAd()` (via `AdFlow`), `bool SpinForced(int wedgeIndex)` (debug), `ResetProfile()`, `SetCooldownSeconds(int)`. The roll is weighted over config rows and resolved **before** the animation starts: the manager raises `SpinStartedEvent` (wedge index + duration), waits `SpinDurationSeconds` on unscaled time, grants through the host, then raises `SpinResultEvent` and `LuckySpinChangedEvent`. The panel only animates onto the pre-rolled wedge; the close button is locked while `IsSpinning`.

## Events / hooks / placements

- Raises `SpinStartedEvent` (wedge index, duration) when a spin begins, `SpinResultEvent` (wedge index, `RewardItem`, amount) on landing and `LuckySpinChangedEvent` on free-spin availability / spinning changes.
- Hooks used: `Granter`, `PlaySfx`, `ShowRewardedAd` (all required).
- Placements: `LuckySpin_AdSpin`.

## Verification script

1. Free spin: wheel decelerates exactly onto the rolled wedge; grant reaches the host granter once; cooldown label starts (`Free spin in mm:ss`).
2. Distribution sanity: with a debug loop of N rolls, wedge frequencies follow config weights.
3. Ad spin: available during cooldown; ad skip → no spin, button unlocked; editor path spins immediately; spam during `IsSpinning` or ad flow → single spin.
4. Kill app / reopen mid-cooldown → remaining cooldown correct from the wall-clock deadline; kill during spin animation → no grant duplication (grant-on-stop rule; exact recovery behavior specified in Phase 4).
5. Open/close panel repeatedly → no duplicated listeners; tweens killed on close.

## Decisions (Phase 4)

- Jackpot wedge: plain grant, no extra ceremony (host can listen to `SpinResultEvent` for its own).
- Ad spins are unlimited during cooldown.
- Kill-during-animation: grant on wheel stop; a kill mid-spin loses that spin, never duplicates it.
- Wedge content stays upright while the wheel turns (`LuckySpinWedge.KeepUpright`), matching the mockup at every rest angle.
