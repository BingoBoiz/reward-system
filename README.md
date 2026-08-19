# reward-system

Development home of **`com.nabagame.reward`** — a reusable Unity package of reward features for NabaGame projects — plus a demo host game used to develop and verify it.

- Unity **2022.3.62f3**, mobile.
- The package lives embedded at [`Packages/com.nabagame.reward/`](Packages/com.nabagame.reward/) and will be published to GitLab like the other `com.nabagame.*` packages.
- `Assets/_GameBase/` is the **demo host**: a stripped fork of Paint And Seek (namespace `PainAndSeek`) that provides the managers, adapters, and scene wiring a real game would, and currently still contains the legacy reward monolith the package features are being extracted from.

## Features

| Feature | Mockup | Spec |
|---|---|---|
| Daily Reward — 7-card daily claim strip | `Documentation~/RefUI/daily-reward.png` | [spec](Packages/com.nabagame.reward/Documentation~/FEATURES/daily-reward.md) |
| Online Reward — timed grid, x2/x5 ad speed-up, session-scoped | `Documentation~/RefUI/online-reward.png` | [spec](Packages/com.nabagame.reward/Documentation~/FEATURES/online-reward.md) |
| Lucky Spin — weighted wheel, free-spin cooldown + ad spin | `Documentation~/RefUI/lucky-spin.png` | [spec](Packages/com.nabagame.reward/Documentation~/FEATURES/lucky-spin.md) |

## Documentation map

| Doc | What it covers |
|---|---|
| [AGENTS.md](AGENTS.md) | Coding conventions and safety rules for agents/developers — read first |
| [Package README](Packages/com.nabagame.reward/README.md) | Package front door: requirements, install, quick start |
| [ARCHITECTURE.md](Packages/com.nabagame.reward/Documentation~/ARCHITECTURE.md) | Dependency rules, reward model, hooks, events, time, save, decision record |
| [CONVENTIONS.md](Packages/com.nabagame.reward/Documentation~/CONVENTIONS.md) | Package folder contract, boundaries, style, definition of done |
| [INTEGRATION-GUIDE.md](Packages/com.nabagame.reward/Documentation~/INTEGRATION-GUIDE.md) | Installing the package into a host project, step by step |
| [ROADMAP.md](Packages/com.nabagame.reward/Documentation~/ROADMAP.md) | Phased development plan and current status |
| [RefUI/](Packages/com.nabagame.reward/Documentation~/RefUI/) | Authoritative UI mockups per feature |

## Current state

- The package contains **documentation only** (Phase 0) — runtime code lands per the roadmap.
- The demo host's reward flow is **not currently runnable**: the ScriptableObject data assets and scene wiring were lost in the fork strip; recreating them is part of roadmap Phase 1–2.
- Reference implementation with the full game: `D:\Fork\paint-and-seek` (read-only).
