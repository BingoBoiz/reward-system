# Reference UI Mockups

This folder holds the **authoritative UI mockups** for every feature in this package. Drop the image files here with the exact names below.

| File | Feature | Spec doc | Status |
|---|---|---|---|
| `daily-reward.png` | Daily Reward — horizontal strip of 7 claim cards + OPEN ALL button | [../FEATURES/daily-reward.md](../FEATURES/daily-reward.md) | awaiting image |
| `online-reward.png` | Online Reward — 3x5 timed grid, OPEN ALL / X2 SPEED / X5 SPEED buttons, "rewards reset if you leave" | [../FEATURES/online-reward.md](../FEATURES/online-reward.md) | awaiting image |
| `lucky-spin.png` | Lucky Spin — 8-wedge wheel, free SPIN button + ad SPIN button with "Free spin in mm:ss" cooldown | [../FEATURES/lucky-spin.md](../FEATURES/lucky-spin.md) | awaiting image (built against `Assets/_ASMR-Tower/Art/preview/spin.jpg`) |

Update the Status column to `present` when an image is added.

## Rules for agents and developers

- When building or reviewing UI for a feature, **match its mockup**. Use the `ui-from-image` skill with the absolute image path (e.g. `d:\Fork\reward-system\Packages\com.nabagame.reward\Documentation~\RefUI\daily-reward.png`).
- Conflict resolution: for **layout/visuals** the image wins; for **behavior/data** the feature spec doc in [../FEATURES/](../FEATURES/) wins.
- `Documentation~` is never imported by Unity, so images here cost nothing in the project or builds.
- Keep filenames stable — the feature docs and package README link to them.
