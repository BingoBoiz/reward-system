# NabaGame Reward

Reusable reward features for NabaGame Unity projects. The package is game-agnostic and owns **no data**: each feature ships as **one panel prefab** that owns the logic, timers, save state (PlayerPrefs), ads/IAP flows, and UI; the host game writes one tiny manager holding the feature's row list, and every claim comes back through the row's `OnClaimed` callback — granting is the host's one job. Audio, ads, and IAP plug in through three static hooks assigned once at boot.

## Features

| Feature | Status | Spec |
|---|---|---|
| **Daily Reward** — 7-card daily claim strip with streak, ads/IAP Open All | Shipped (0.8.0) — demo in `Samples~/RewardDemo` | [Documentation~/FEATURES/daily-reward.md](Documentation~/FEATURES/daily-reward.md) |
| **Online Reward** — timed reward grid unlocking while playing, x2/x5 ad speed-up, session-scoped | Shipped (0.8.0) — demo in `Samples~/RewardDemo` | [Documentation~/FEATURES/online-reward.md](Documentation~/FEATURES/online-reward.md) |
| **Lucky Spin** — weighted spin wheel, free-spin cooldown + ad spin | Shipped (0.8.0) — demo in `Samples~/RewardDemo` | [Documentation~/FEATURES/lucky-spin.md](Documentation~/FEATURES/lucky-spin.md) |

Authoritative UI mockups live in [Documentation~/RefUI/](Documentation~/RefUI/).

**Direction change (2026-08-20, `0.8.0`):** the package managers are gone (ARCHITECTURE decisions #21–#27) — the panel owns everything, grants moved from events to `Row.OnClaimed`, `RewardHooks` became static, init is `SetInfo(rows)` (the `StartClass` convention is retired), and every serialized UI reference is optional. The consumer-team style profile driving the design is in [Documentation~/CONSUMER-STYLE.md](Documentation~/CONSUMER-STYLE.md).

## Requirements

Git-based package dependencies are **not** auto-resolved by Unity, so the host project must already contain:

| Dependency | Provides | Referenced as |
|---|---|---|
| `com.nabagame.core` | EventManager, Singleton | asmdef `com.bmh.core.runtime` |
| `com.nabagame.ui` | BaseUI, UIPanel, UIManagerSingleton | asmdef `com.nabagame.ui.runtime` |
| `com.cysharp.unitask` | async flows, timers | asmdef `UniTask` |
| Odin Inspector (vendored in `Assets/Plugins/Sirenix/`) | `[TableList]` rows, debug buttons | precompiled DLLs (auto-referenced) |
| DOTween (vendored in `Assets/Plugins/Demigiant/`) | UI tweens | precompiled DLL (auto-referenced) |

Unity **2022.3+**. The package does **not** depend on any ads SDK or save plugin — ads/IAP go through hooks (sample adapters ship in `Samples~`), and saving uses `PlayerPrefs`.

## Install

- **This repo (development):** the package is embedded at `Packages/com.nabagame.reward/` — nothing to do.
- **Other projects (once published):** Package Manager → *Add package from git URL* → `https://gitlab.com/nbg-team1/nbg-core/reward-package.git` (final URL decided at first publish), or copy the folder into the host's `Packages/`.
- **Upgrading from ≤0.7.0:** delete the old imported sample (`Assets/Samples/NabaGame Reward/<old>/`) first — sample types were renamed.

## Quick start

1. Install the requirements above, then the package; import the **Reward Demo** sample.
2. Drag the feature panel prefab (e.g. `DailyRewardPanel`) under your UI root.
3. Write your manager from the sample template (`SampleDailyRewardManager`, ~15 lines): fill its `rows` table (Inspector `[TableList]` or the row constructor in code) — each row carries your key, icon, amount, claim SFX, and the `OnClaimed` grant callback. That one row class is everything you fill.
4. At boot (`Start()`), assign the three statics — `RewardHooks.PlaySfx` / `ShowRewardedAd` / `PurchaseIap` — then call your manager's `SetInfo()`, which calls `panel.SetInfo(rows)`.
5. Wire your home button to `panel.OpenPanel()`. Press Play. Half-filled data warns instead of breaking; a claimed row without `OnClaimed` logs `was NOT granted`.

Full walkthrough: [Documentation~/INTEGRATION-GUIDE.md](Documentation~/INTEGRATION-GUIDE.md) (includes the ASMR_Tower drop-in section).
Design and contracts: [Documentation~/ARCHITECTURE.md](Documentation~/ARCHITECTURE.md).
Development plan: [Documentation~/ROADMAP.md](Documentation~/ROADMAP.md).
