# Reward Demo sample

Runnable Daily Reward, Lucky Spin, and Online Reward (playtime) screens in **one scene**, skinned with the
ASMR-Tower art set and laid out against the reference previews at a 2400x1080 design resolution. Everything
prefixed `Sample` is host-side code you own after import — the `Sample*` managers are the exact template a
real game copies.

## Run it

1. Import this sample through the Package Manager. (Upgrading from ≤0.7.0: delete the old
   `Assets/Samples/NabaGame Reward/<old>/` folder first — sample types were renamed.)
2. Open `Scenes/RewardSample.unity` and press Play. The home buttons on the left (Daily / Spin / Playtime)
   open each panel and carry red-dot badges driven by package events + panel queries.
3. The currency HUD shows every grant landing, and every grant also opens the reward-received ceremony
   popup (dim, burst, CONGRATULATION, card stagger — tap to continue). Spin: the first spin is free, then
   the button switches to the ad variant with a `Free spin in mm:ss` countdown (the sample "ad" rewards
   immediately).

## What is where

| Folder | Contents |
|---|---|
| `Scripts/` | `SampleRewardBoot` (composition root: assigns the three `RewardHooks` statics, then calls every `SetInfo` from `Start()`), `SampleDailyRewardManager` / `SampleLuckySpinManager` / `SampleOnlineRewardManager` (the dev-owned managers: `[TableList] rows` + `OnClaimed` → `SampleRewardGranter.Grant`), `SampleRewardGranter` (maps `Row.Key` onto PlayerPrefs counters, raises `SampleItemGrantedEvent`), `SampleUIRoot` (UI composition root, auto-finds panels), `SampleCurrencyHud`, `SampleHomeButtons` (red dots over the change events + panel queries), `SampleItemReceivedPanel` + `SampleItemReceivedCell` + `SampleItemReceivedBurstFx` (ceremony popup over `SampleItemGrantedEvent`, same-frame batching, key stacking) |
| `Prefabs/` | `SampleUIRoot` (root Canvas, 2400x1080 ScaleWithScreenSize match 0.5, HUD + home buttons + all panels), `DailyRewardPanel` + `DailyRewardCard` template, `LuckySpinPanel` + `LuckySpinWedge` template, `OnlineRewardPanel` + `OnlineRewardCell` template, `SampleOnlineRewardManager` (carries the 18 sample rows), `SampleItemReceivedPanel` (sorting 250) + `SampleItemReceivedCell` template |
| `Art/`, `Fonts/` | ASMR-Tower sprites and the PassionOne TMP font the prefabs reference; `Art/ItemReceived/` holds the ceremony fx sprites |

The package owns no data assets: the reward rows live on the `Sample*` managers (`SampleDailyRewardManager.rows`
— 7 rows; `SampleLuckySpinManager.rows` — 8 weighted wedges; `SampleOnlineRewardManager.rows` — 18 rows on
the manager prefab, one unlock per minute). Each row carries its own `Key`, `Sprite Icon`, `Amount`, and
optional `ClaimSfx`. Feature knobs live on the **panel prefabs**: Daily's Open All config (`openAllAdsRequired`
3, IAP product + `$4.99`), Spin's 30-min cooldown and spin timing, Online's x2/x5 durations and `x5AdsRequired`.

## Adapting it to your game

`SampleRewardGranter.Grant` is the only piece that knows what a reward *means*. Copy a `Sample*` manager,
fill its `rows` with your own keys/icons/amounts, and grant from `Row.Key` inside `OnClaimed` — keep the
fail-loud `default` arm: an unknown key must reach the Console, never a silent no-op. A claimed row whose
`OnClaimed` is null grants nothing and LogErrors `was NOT granted`; every grant is also `Debug.Log`ged.

At boot, replace the three adapters in `SampleRewardBoot.SetInfo()` with your services
(`SoundManager`/`AdManager`/`IAPManager` one-liners are in INTEGRATION-GUIDE §6), and call the managers'
`SetInfo()` from `Start()` — **Online Reward must be initialized at boot** or playtime never accrues.

The ceremony popup is host-side by design: the granter raises `SampleItemGrantedEvent` (`Key`, `Icon`,
`Amount`) and `SampleItemReceivedPanel` listens — the package never opens it.

Everything else — claim rules, streak, UTC rollover, weighted roll, cooldown, spin timing, speed-ups,
persistence — lives on the package panels and needs no changes. Panels open through `OpenPanel()` and
close through `ClosePanel()`; each panel's `#region API` is the entire surface you need. Any serialized
button/label on a panel may be disabled or deleted — the feature degrades silently. The wheel art has 8
segments; a row count that disagrees with `LuckySpinPanel.wheelSegmentCount` logs a warning. Debug
`[Button]`s on the panels cover force-wedge, cooldown, add-seconds, roll distribution, and profile reset.
