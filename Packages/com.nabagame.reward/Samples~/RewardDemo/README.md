# Reward Demo sample

Runnable Daily Reward and Lucky Spin screens, skinned with the ASMR-Tower art set and laid out against
the reference previews at a 2400x1080 design resolution.

## Run it

1. Import this sample through the Package Manager.
2. Open `Scenes/LuckySpinSample.unity` (opens the spin wheel on Play) or `Scenes/DailyRewardSample.unity`
   (opens the 7-day strip). Both scenes are identical apart from `RewardSampleBoot.startPanel`; the home
   buttons on the left reopen either panel and carry red-dot badges driven by package events.
3. Press Play. The currency HUD shows every grant landing. Spin: the first spin is free, then the button
   switches to the ad variant with a `Free spin in mm:ss` countdown (the sample "ad" rewards immediately).

## What is where

| Folder | Contents |
|---|---|
| `Scripts/` | `SampleUIRoot` (UI composition root), `RewardSampleBoot` (builds `RewardHooks`, drives every `StartClass`), `SampleRewardGranter` (`IRewardGranter` -> PlayerPrefs counters), `SampleCurrencyHud`, `SampleHomeButtons` (host-side red dots over `DailyRewardChangedEvent` / `LuckySpinChangedEvent`) |
| `Prefabs/` | `UIMainManager` (root Canvas, 2400x1080 ScaleWithScreenSize match 0.5, HUD + home buttons + both panels), `DailyRewardPanel` + `DailyRewardCard` template, `LuckySpinPanel` + `LuckySpinWedge` template |
| `Data/` | `RawRewardItem` (`RewardItemData` catalog: cash/spin/noads + icons), `RawDailyReward` (`DailyRewardRowData`, 7 rows), `RawLuckySpin` (`LuckySpinRowData`, 8 weighted wedges, 30 min free-spin cooldown). Named and shaped exactly as the NabaGame Googlesheet Importer writes them — `Documentation~/SHEET-TEMPLATE.md` holds the same content as sheet tabs |
| `Art/`, `Fonts/` | sprites and the PassionOne TMP font the prefabs reference |

## Adapting it to your game

`SampleRewardGranter` + `RawRewardItem` (the `RewardItemData` catalog handed over as `hooks.Catalog`) are the only pieces that
know what a reward *means*. Replace them with your own keys/icons and an `IRewardGranter` that maps `RewardItem.Key` onto
your economy, and keep the granter's fail-loud `default` arm:
an unknown key must reach the Console, never a silent no-op. Replace `RewardSampleBoot.ShowRewardedAd`
with your ads SDK call (placement `LuckySpin_AdSpin`).

Everything else — claim rules, streak, UTC rollover, weighted roll, cooldown, spin timing, persistence —
lives in the package (`NabaGame.Reward.DailyRewardManager`, `NabaGame.Reward.LuckySpinManager`) and needs
no changes. The wheel art has 8 segments; `LuckySpinPanel.wheelSegmentCount` must match your config's row
count (an error is logged otherwise). Debug `[Button]`s on the managers and panels cover force-wedge,
cooldown, roll distribution and profile reset.
