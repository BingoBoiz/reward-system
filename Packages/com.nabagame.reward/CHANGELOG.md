# Changelog

All notable changes to this package are documented here. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning: [SemVer](https://semver.org/).

## [Unreleased]

## [0.4.0] - 2026-08-19

### Changed (breaking)

- Data model reshaped for the NabaGame Googlesheet Importer (ARCHITECTURE decision #15): every table is `{Row}` POCO + `{Row}Data` ScriptableObject with a public `{row}s` list, so *Generate Assets* writes package config directly — no host bake.
- `RewardItem` is a plain `[Serializable]` row (`Key`, `DisplayName`, `Icon`), no longer a ScriptableObject; new `RewardItemData` catalog (`rewardItems`, `Get(key)` throws on unknown key) replaces one-asset-per-reward.
- `RewardHooks.Catalog` (`RewardItemData`) added and validated in `Validate()`.
- `DailyRewardConfig` → `DailyRewardRowData` (`dailyRewardRows` of `{Day, Key, Amount, LabelOverride, HideIconUntilClaim}`; `"-"` = no label override); `CardBackground` moved to `DailyRewardPanel.cardBackgrounds` (7 sprites). `DailyRewardManager.GetItem(day)`; `DailyRewardCard.StartClass(day, row, item, background, cb)`.
- `LuckySpinConfig` → `LuckySpinRowData` (`luckySpinRows` of `{Wedge, Key, Amount, Weight}` + cooldown/duration/turns); `LuckySpinManager.GetItem(index)`; `LuckySpinWedge.StartClass(index, row, item)`.
- Managers resolve keys through `hooks.Catalog` in `StartClass` and throw on unknown key, wrong `Day`/`Wedge` order, non-positive `Amount`/`Weight`.
- Sample data renamed to the importer's output names: `Data/RawRewardItem.asset`, `RawDailyReward.asset`, `RawLuckySpin.asset`; `RewardSampleBoot.rewardItemData` field. New `Documentation~/SHEET-TEMPLATE.md`.

## [0.3.0] - 2026-08-19

### Added

- `Runtime/Core/AdFlow`: rewarded-ad helper over the `ShowRewardedAd` hook (busy guard, editor-immediate reward, next-frame release).
- `Runtime/Core/RewardEvents`: `LuckySpinChangedEvent`, `SpinStartedEvent` (wedge index + duration), `SpinResultEvent` (wedge index, item, amount).
- `Runtime/Features/LuckySpin/`: `LuckySpinConfig` (weighted wedge rows, cooldown, spin duration/turns, editor-validated), `LuckySpinProfile`, `LuckySpinManager` (weighted roll resolved before the animation, free-spin cooldown through `TimeScheduler`, ad spin through `AdFlow`, grant on wheel stop), `LuckySpinPanel` (`BaseUI`, wind-up + ease-out spin onto the rolled wedge, pointer ticks, upright wedge content, cooldown countdown, ad-spin variant), `LuckySpinWedge` template widget.

### Changed

- Sample renamed `Samples~/DailyReward` -> `Samples~/RewardDemo` and now covers both features: `DailyRewardSample` + `LuckySpinSample` scenes, `LuckySpinPanel`/`LuckySpinWedge` prefabs, `LuckySpinConfig`, `SampleHomeButtons` (host-side red-dot recipe over package events), `RewardSampleBoot.startPanel`.

## [0.2.0] - 2026-08-19

### Added

- `Runtime/NabaGame.Reward.asmdef` (refs `com.bmh.core.runtime`, `com.nabagame.ui.runtime`, `UniTask`, `Unity.TextMeshPro`) — the package no longer sees `Assembly-CSharp`.
- `Runtime/Core/`: `RewardItem`, `IRewardGranter`, `RewardHooks` (+ `Validate`), `TimeScheduler` (ported from the host, renamespaced), `RewardProfileStore` + `RewardProfile` base (PlayerPrefs + JsonUtility, `Version` field), `DailyRewardChangedEvent`, `RewardAmountFormat`.
- `Runtime/Features/DailyReward/`: `DailyRewardConfig` (7 rows, editor-validated), `DailyRewardProfile`, `DailyRewardManager` (UTC-midnight rollover through `TimeScheduler`, no `Update()`), `DailyRewardPanel` (`BaseUI`), `DailyRewardCard`.
- `Samples~/DailyReward`: runnable demo scene, prefabs, sample `RewardItem`/config assets, `SampleRewardGranter`, currency HUD, `SampleUIRoot`. Registered in `package.json` `samples[]`.

### Notes

- `AdFlow` is deliberately absent: Daily Reward uses no ads. It arrives with Online Reward (ROADMAP phase 3).
- No `Editor/` assembly yet; package panels ship explicit Odin `[Button]` debug methods instead of an attribute processor, because `BaseUIInspectorProcessor` lives in `Assembly-CSharp` and cannot reach them.

## [0.1.0] - 2026-08-19

### Added

- Package skeleton: `package.json`, documentation set (`Documentation~/`: ARCHITECTURE, CONVENTIONS, INTEGRATION-GUIDE, ROADMAP, per-feature specs, RefUI mockups). No runtime code yet — see `Documentation~/ROADMAP.md`.
