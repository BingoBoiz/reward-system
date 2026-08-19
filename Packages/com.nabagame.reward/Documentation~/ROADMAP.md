# Roadmap

Development plan for `com.nabagame.reward`. Ordering principle: **package skeleton and shared primitives first**, then features smallest-to-largest against the RefUI mockups. The legacy monolith in `Assets/_GameBase/` (one `RewardManager` owning daily + playtime + speed-up + free-ads, one tabbed `RewardPanel`) stays untouched as a *behavioral reference* until each feature is rebuilt in the package, then gets deleted.

Dependencies: `0 → 1 → {2 → 3, 4} → 5` (Lucky Spin can run in parallel with Daily/Online).

**Status 2026-08-19:** phases 0, 1, 2, 4 done; data model reshaped for the Googlesheet Importer (package `0.4.0`, ARCHITECTURE decision #15). Next: phase 3 (Online Reward).

## Phase 0 — Docs & skeleton ✅ (2026-08-19)

`package.json`, README, CHANGELOG, `Documentation~` set, RefUI folder. No code.

## Phase 1 — Package scaffold + core primitives ✅ (2026-08-19)

- `Runtime/NabaGame.Reward.asmdef` — the asmdef *names* are `com.bmh.core.runtime` and **`com.nabagame.ui.runtime`** (the UI package's file is named `com.bigbear.ui.runtime.asmdef` but declares the `com.nabagame.ui.runtime` name); plus `UniTask` and `Unity.TextMeshPro`.
- **Deviation:** no `Editor/NabaGame.Reward.Editor.asmdef` yet — nothing needed it. Package panels carry explicit Odin `[Button]` debug methods instead of an attribute processor.
- **Deviation:** `AdFlow` not built — Daily Reward uses no ads. It lands with Online Reward in phase 3.
- `Runtime/Core/`: `RewardItem` (SO in 0.2–0.3, plain row + `RewardItemData` catalog since 0.4.0), `IRewardGranter`, `RewardHooks` (+ StartClass validation), PlayerPrefs profile base (JsonUtility, `NabaReward.*` keys, `Version` field), `TimeScheduler` (moved from `Assets/_GameBase/Scripts/_Others/TimeScheduler.cs`, renamespaced `NabaGame.Reward`), `AdFlow` helper, package event classes.
- Repo cleanup (still open): delete the empty `Assets/_00 Daily_Reward/` and `Assets/_00 Lucky_Spin/` folders.
- Candidates to upstream to `com.naba.extend` later (not now): `BigNumberExtension` and similar generic utils. `RewardAmountFormat` in `Core/` is the package's own minimal replacement.
- **Gate:** met — package compiles into assembly `NabaGame.Reward`, console clean, boundary grep returns nothing.

## Phase 2 — Daily Reward

- `DailyRewardManager` + `DailyRewardProfile` (streak day, last-claim UTC date `yyyy-MM-dd`, PlayerPrefs), midnight rollover armed via `TimeScheduler`.
- `DailyRewardConfig` SO: 7 rows of `RewardItem` + amount (0.4.0: `DailyRewardRowData`, importer-shaped rows keyed by string).
- `DailyRewardPanel` prefab + scripts per `RefUI/daily-reward.png` (7-card strip, claimed/claimable/locked states, OPEN ALL button).
- Demo host: granter/hooks adapters, sample data, GameController + UIManager wiring.
- **Decision to resolve with the user:** OPEN ALL semantics — current legacy logic claims max 1/day; the mockup button implies claiming everything available (catch-up or ad-gated multi-claim?).
- **Gate:** integration checklist (CONVENTIONS.md) passes in the demo host.

## Phase 3 — Online Reward

- `OnlineRewardManager`: **session-scoped** grid (decision #9) — slot timers derive from session play time (baseline pattern, ARCHITECTURE.md §5); unclaimed slots reset on app quit per the mockup ("rewards reset if you leave"). Persisted profile keeps only what must survive (e.g. speed-up ad counters if any — specify in the feature doc).
- x2/x5 speed-up via `AdFlow`; placements `OnlineReward_x2Speed`, `OnlineReward_x5Speed`; stacking rules specified in the feature doc.
- `OnlineRewardPanel` 3x5 grid per `RefUI/online-reward.png` (sequential unlock, OPEN ALL, X2/X5 buttons).
- End of phase: retire legacy playtime code paths in `_GameBase` (playtime region of `RewardManager`, `RewardPlaytimeTab`, `RewardPlaytimeSlot`).
- **Gate:** integration checklist passes.

## Phase 4 — Lucky Spin ✅ (2026-08-19)

Built as specified below, with these resolutions: wedge count is data-driven but the sample wheel art has **8** segments (panel logs an error on mismatch); grant fires on wheel stop (kill mid-animation loses the spin, no duplicate grant); ad spins are unlimited during cooldown; no extra jackpot ceremony. `AdFlow` landed here instead of phase 3. Sample folder renamed to `Samples~/RewardDemo` (shared host scripts cannot be split into two importable samples).

- `LuckySpinConfig` SO: wedge table (`RewardItem` + amount + weight), wedge count driven by data (mockup shows 10). 0.4.0: `LuckySpinRowData`, importer-shaped rows keyed by string.
- `LuckySpinManager` + `LuckySpinProfile`: weighted roll, free-spin cooldown deadline via `TimeScheduler` (persisted), ad spin via `AdFlow` (placement `LuckySpin_AdSpin`).
- `LuckySpinPanel` per `RefUI/lucky-spin.png`: wheel built from wedge data, pointer, decelerating spin-to-wedge DOTween tween landing on the rolled wedge (the legacy `SpinEffect` looper is only good for idle shimmer), free SPIN button + ad SPIN button with "Free spin in mm:ss" label.
- **Gate:** integration checklist passes.

## Phase 5 — Samples, polish, release 1.0.0

- `Samples~/Integration`: demo scene, granter/audio/`com.bmh.ads` adapters, sample `RewardItemData` + feature tables — verified importable into a scratch project.
- UI polish pass per feature against RefUI mockups (`ui-from-image`), red-dot recipe wired in the demo host.
- Delete remaining legacy reward code from `_GameBase`: `RewardManager`, `RewardPanel` + tabs, free-ads code (`RewardFreeTab`, `RawAdsWatchRewardData` flow — decision #11), reward Raw/Processed SOs no longer referenced.
- Full INTEGRATION-GUIDE dry run into a fresh project; fix every step that didn't reproduce.
- `package.json` → 1.0.0, CHANGELOG, publish to GitLab.
