# NabaGame Reward

Reusable reward features for NabaGame Unity projects. The package is game-agnostic: it owns the reward logic, timers, save state, and UI panels; the host game plugs in its own economy, audio, and ads through a small hooks contract.

## Features

| Feature | Status | Spec |
|---|---|---|
| **Daily Reward** — 7-card daily claim strip with streak | Built (0.2.0) — demo in `Samples~/RewardDemo` | [Documentation~/FEATURES/daily-reward.md](Documentation~/FEATURES/daily-reward.md) |
| **Online Reward** — timed reward grid unlocking while playing, x2/x5 ad speed-up, session-scoped | Planned | [Documentation~/FEATURES/online-reward.md](Documentation~/FEATURES/online-reward.md) |
| **Lucky Spin** — weighted spin wheel, free-spin cooldown + ad spin | Built (0.3.0) — demo in `Samples~/RewardDemo` | [Documentation~/FEATURES/lucky-spin.md](Documentation~/FEATURES/lucky-spin.md) |

Authoritative UI mockups live in [Documentation~/RefUI/](Documentation~/RefUI/).

## Requirements

Git-based package dependencies are **not** auto-resolved by Unity, so the host project must already contain:

| Dependency | Provides | Referenced as |
|---|---|---|
| `com.nabagame.core` | EventManager, Singleton | asmdef `com.bmh.core.runtime` |
| `com.nabagame.ui` | BaseUI, UIPanel, UIManagerSingleton | asmdef `com.nabagame.ui.runtime` |
| `com.cysharp.unitask` | async flows, timers | asmdef `UniTask` |
| Odin Inspector (vendored in `Assets/Plugins/Sirenix/`) | config inspectors, `SerializedScriptableObject` | precompiled DLLs (auto-referenced) |
| DOTween (vendored in `Assets/Plugins/Demigiant/`) | UI tweens | precompiled DLL (auto-referenced) |

Unity **2022.3+**. The package does **not** depend on any ads SDK or save plugin — ads go through a hook (a sample adapter for `com.bmh.ads` ships in `Samples~`), and saving uses `PlayerPrefs`.

## Install

- **This repo (development):** the package is embedded at `Packages/com.nabagame.reward/` — nothing to do.
- **Other projects (once published):** Package Manager → *Add package from git URL* → `https://gitlab.com/nbg-team1/nbg-core/reward-package.git` (final URL decided at first publish), or copy the folder into the host's `Packages/`.

## Quick start

1. Install the requirements above, then the package.
2. Implement `IRewardGranter` in your game — map each `RewardItem.Key` to your own `RewardType`/profiles; **fail loudly** on unknown keys.
3. Build a `RewardHooks` (granter + `PlaySfx` + `ShowRewardedAd`) — `Samples~` contains ready adapters.
4. Drop the feature manager prefab under your `GameController`, call `StartClass(config, hooks)` from its init chain, and register the feature panel in your `UIManager`.
5. Create your data (a `RewardItem` catalog + the feature tables — from Google Sheets through the NabaGame importer, or by hand) and press Play.

Full walkthrough: [Documentation~/INTEGRATION-GUIDE.md](Documentation~/INTEGRATION-GUIDE.md).
Design and contracts: [Documentation~/ARCHITECTURE.md](Documentation~/ARCHITECTURE.md).
Development plan: [Documentation~/ROADMAP.md](Documentation~/ROADMAP.md).
