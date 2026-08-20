# Lucky Spin

**Status:** built in `0.3.0` (2026-08-19), contract flip in `0.5.0`/`0.6.0`; **panel-owned rework shipped in `0.8.0` (2026-08-20, decisions #21–#27)** — the package `LuckySpinManager` is gone, `LuckySpinPanel` owns everything, the host writes a `SampleLuckySpinManager`-style manager. Reference art: `Assets/_ASMR-Tower/Art/preview/spin.jpg` (8-segment wheel `spin_0001_vong-quay`, pin pointer `spin_0000_kim-xoay`).

**Mockup:** [../RefUI/lucky-spin.png](../RefUI/lucky-spin.png) — authoritative for layout/visuals; this doc is authoritative for behavior/data.

## What it is

A weighted prize wheel. One free spin per cooldown window; additional spins by watching a rewarded ad. The wheel decelerates onto the rolled wedge and the reward is granted through the row's `OnClaimed`.

## UI spec (from mockup)

- Popup with the wheel centered, close (X) top-right.
- **Wheel of N wedges** (count driven by the row list; the shipped ASMR-Tower wheel art has 8 segments and the panel warns when rows and art disagree), each wedge showing a reward icon + amount label (`1.5K`, `100B`, `X10`, …); one highlighted jackpot wedge allowed via rarity styling. Golden rim with dot lights, white center hub, fixed pointer at 12 o'clock.
- Spin button states below the wheel:
  - **Free spin available** — green `SPIN` button (may carry a red badge).
  - **Cooldown** — ad-spin variant: `SPIN` button with a video icon and caption `Free spin in mm:ss` counting down to the next free spin.
- During a spin all buttons lock; the wheel accelerates then decelerates onto the result wedge (DOTween ease-out); result lands with feedback (SFX + grant).

## Data (dev-filled)

The host's own manager (template: `Samples~/RewardDemo/Scripts/SampleLuckySpinManager.cs`) holds `[TableList] public List<LuckySpinRow> rows`, assigns each row's `OnClaimed`, and passes the list to `LuckySpinPanel.SetInfo(rows)` at boot.

Row — list position is the wedge (`rows[0]` at 12 o'clock, clockwise):
`{ string Key, Sprite Icon, long Amount, int Weight, AudioClip ClaimSfx, Action<LuckySpinRow> OnClaimed }` — a constructor documents the fill order.

Validation is lenient (decision #24): missing `Key`/`Icon`/`Amount`, `Weight <= 0` → one aggregated warning via `LuckySpinRow.Warn(rows)`; fewer than 2 rows throws. All-invalid weights fall back to a uniform roll.

Panel knobs (`[SerializeField]` on the `LuckySpinPanel` prefab): `freeSpinCooldownSeconds` (1800), `spinDurationSeconds` (4.5), `spinFullTurns` (5), `spinStartSfx`, `landSfx`, `wheelSegmentCount`, wedge layout values, `buttonSfx`, `tickSfx`.

## Save (PlayerPrefs key `NabaReward.Spin`)

| Field | Meaning |
|---|---|
| `Version` | payload version |
| `NextFreeSpinAtMs` | unix ms deadline of the next free spin (0 = available now); survives kill/reopen via wall clock |

## API surface — `LuckySpinPanel`, `#region API`

- `SetInfo(List<LuckySpinRow> rows)` — single init: validate, load save, arm the cooldown deadline (`TimeScheduler`), build wedges, bind listeners. Call from `Start()` at boot; the panel stays hidden.
- `OpenPanel()` / `ClosePanel()` — dev-facing activation. `ClosePanel()` refuses while `IsSpinning`.
- Queries: `bool FreeSpinReady` (the red-dot query), `double SecondsUntilFreeSpin`, `bool IsSpinning`, `bool CanSpinByAd`.
- `ResetProfile()` — QA/debug reset.
- Consts: `SaveKey`, `ProfileVersion`, `AdPlacement`.

The spin button runs the free spin when ready, else the ad spin (`AdFlow`, placement `LuckySpin_AdSpin`). The roll is weighted over the rows and resolved **before** the animation: the panel raises `SpinStartedEvent`, plays the wind-up + deceleration tween onto the pre-rolled wedge, waits `spinDurationSeconds` on unscaled time, then on wheel stop plays `landSfx` + the row's `ClaimSfx`, `Debug.Log`s the grant, and invokes `Row.OnClaimed` — the host grants there.

## Events / hooks / placements

- **Grants: `Row.OnClaimed`** (decision #22) — raised on wheel stop; no `SpinResultEvent` exists.
- `SpinStartedEvent { int WedgeIndex; float DurationSeconds; }` — notification, raised when a spin begins (the wedge is already rolled).
- `LuckySpinChangedEvent` — notification, raised on free-spin availability / spinning changes (red dots / refresh).
- `LuckySpinPanelClosedEvent` — notification, raised when the player closes the panel.
- Hooks used: `PlaySfx`, `ShowRewardedAd`. Optional — unset hooks LogError and proceed (decision #23).
- Placements: `LuckySpin_AdSpin`.

## Verification script

1. Free spin: wheel decelerates exactly onto the rolled wedge; exactly one `OnClaimed` reaches the host handler (currency changes, grant log in Console); cooldown label starts (`Free spin in mm:ss`).
2. Distribution sanity: `PreviewRollDistribution` — wedge frequencies follow row weights.
3. Ad spin: available during cooldown; ad skip → no spin, button unlocked; editor path spins immediately; spam during `IsSpinning` or ad flow → single spin; a host SDK that swallows both callbacks un-sticks after the `AdFlow` timeout (~15s).
4. Kill app / reopen mid-cooldown → remaining cooldown correct from the wall-clock deadline; kill during spin animation → no grant duplication (grant-on-stop rule; a kill mid-spin loses that spin).
5. Open/close panel repeatedly → no duplicated listeners; tweens killed on close; hiding via `Hide()` stops the countdown loop.
6. Null-button pass: disable/delete `spinButton`, `cooldownLabel`, `pointer`, `spinBadge` → no exception, no stuck state.

## Decisions (Phase 4)

- Jackpot wedge: plain grant, no extra ceremony (the host's `OnClaimed` can add its own).
- Ad spins are unlimited during cooldown.
- Kill-during-animation: grant on wheel stop; a kill mid-spin loses that spin, never duplicates it.
- Wedge content stays upright while the wheel turns (`LuckySpinWedge.KeepUpright`), matching the mockup at every rest angle.
