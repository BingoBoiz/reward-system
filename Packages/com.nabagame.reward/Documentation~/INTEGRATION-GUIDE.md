# Integration Guide

How to install `com.nabagame.reward` into a NabaGame host project, from zero to a working feature. The demo host in this repo (`Assets/_GameBase/`) is the living reference for every step.

## 1. Prerequisites

Install these first — git package dependencies are **not** auto-resolved by Unity:

- `com.nabagame.core` (git) — EventManager, Singleton
- `com.nabagame.ui` (git) — BaseUI, UIPanel, UIManagerSingleton
- `com.cysharp.unitask` (git)
- Odin Inspector vendored under `Assets/Plugins/Sirenix/`
- DOTween vendored under `Assets/Plugins/Demigiant/`

Unity 2022.3+. No ads SDK is required by the package itself; the sample ads adapter targets `com.bmh.ads` if the host uses it.

## 2. Add the package

- Development repo: already embedded under `Packages/com.nabagame.reward/`.
- Other hosts: Package Manager → *Add package from git URL*, or copy the package folder into `Packages/`. Confirm "NabaGame Reward" appears in Package Manager.

## 3. Import the Integration sample

Package Manager → NabaGame Reward → Samples → **Integration** → Import. This lands adapters, a demo scene, and sample data under `Assets/Samples/NabaGame Reward/...`. Use the adapters as starting points — they are plain game-side code you own after import.

## 4. Implement `IRewardGranter`

One class in your game maps package reward keys to your economy:

```csharp
public sealed class GameRewardGranter : IRewardGranter
{
    public void Grant(RewardItem item, long amount)
    {
        switch (item.Key)
        {
            case "cash":   CommonManager.Instance.PlayerProfile.ChangeGold(amount);  break;
            case "gem":    CommonManager.Instance.PlayerProfile.ChangeGem(amount);   break;
            // ... every key used by your data assets
            default: throw new InvalidOperationException($"Unknown RewardItem key '{item.Key}'");
        }
        // then: save, raise your game's ItemGrantedEvent / ceremony popup, tracking
    }
}
```

Fail-loud on unknown keys is mandatory — never a silent default.

## 5. Build the hooks

```csharp
var hooks = new RewardHooks
{
    Granter        = new GameRewardGranter(),
    Catalog        = rewardItemData,                                   // RewardItemData asset: Key -> icon / display name
    PlaySfx        = clip => AudioManager.Instance.PlaySound(clip),   // add a clip overload if the host only has enum-based PlaySound
    ShowRewardedAd = (placement, onReward, onSkip) =>
        GameManager.Instance.ShowRewardedVideo(onReward, onSkip, placement),
};
```

Each feature manager receives this at `StartClass`. Missing required hooks throw immediately — wire everything before Play.

## 6. Wire managers and panels

1. Drop the feature manager prefab (e.g. `DailyRewardManager`) as a child of your `GameController`, add a serialized field, and one line in `GameController.StartClass()`:
   `dailyRewardManager.StartClass(dailyRewardConfig, hooks);`
2. Drop the feature panel prefab (e.g. `DailyRewardPanel`) under your UI root; add a serialized field in your `UIManager`, call its `StartClass()` in `UIManager.StartClass()`, and add it to your popup tracking list.
3. Open it like any local panel (`UIManager.Instance.dailyRewardPanel.Show()` or your Open wrapper).

## 7. Create data assets

All package tables are shaped for the NabaGame Googlesheet Importer (original `com.nabagame.googlesheet.importer` or the `com.feeder.editortools` fork): one sheet tab = one ScriptableObject, no bake step. Tab name, A1 cell and headers are fixed per table:

| Tab | A1 (row class) | Headers (prefix_Field, in this order) | Asset the importer writes |
|---|---|---|---|
| `RewardItem` | `RewardItem` | `s_Key` `s_DisplayName` `sp_Icon` | `Raw/RawRewardItem.asset` (`RewardItemData`) |
| `DailyReward` | `DailyRewardRow` | `n_Day` `s_Key` `l_Amount` `s_LabelOverride` `b_HideIconUntilClaim` | `Raw/RawDailyReward.asset` (`DailyRewardRowData`) |
| `LuckySpin` | `LuckySpinRow` | `n_Wedge` `s_Key` `l_Amount` `n_Weight` | `Raw/RawLuckySpin.asset` (`LuckySpinRowData`) |

Sheet path:

1. Paste the three tabs (`Documentation~/SHEET-TEMPLATE.md` has the sample content as TSV). Rules the importer enforces: no blank cell in the first data row (use `-` for "no label override"), `TRUE`/`FALSE` for bools, plain integers (no `7,500,000`), `???`/`RARE` are plain text.
2. In the importer's SheetInfo set `AssetFolder` (e.g. `_GameBase/Datas/Raw`) and, for the Feeder fork, `SpriteAssetFolder` (where `sp_Icon` names resolve) and `Namespace = NabaGame.Reward`.
3. Press **Generate Assets** for each tab. **Do not press Generate Script** for these tabs — the row classes already live in the package; generating them again creates duplicate types in `Assembly-CSharp`.
4. With the original importer `sp_Icon` cannot be filled: after importing `RewardItem` assign the icons in the Inspector (or skip that tab and author `RewardItemData` by hand — it is three rows).
5. Re-importing keeps the asset GUID and every non-list field (cooldown, spin duration); only rows are rewritten.

Hand path: Create → NabaGame → Reward → *Reward Item Data* / *Daily Reward Data* / *Lucky Spin Data*, fill the tables in the Inspector. Same shape, same validators.

Either way: assign `RewardItemData` to `hooks.Catalog`, the feature table to the manager's `StartClass`, and fix every Console error from the validators (row count, `Day`/`Wedge` order, empty keys, non-positive amounts/weights, keys missing from the catalog) before Play. `DailyRewardPanel.cardBackgrounds` (7 sprites) is prefab art, not data.

## 8. Register ad placements

Each feature spec lists its placement strings (e.g. `OnlineReward_x2Speed`, `LuckySpin_AdSpin`). Register them in your mediation dashboard/tracking the same way you do for game placements.

## 9. Red dots (optional)

In your `RedDotManager`, subscribe to the package change events and evaluate from public API:

```csharp
EventManager.Instance.AddListener<DailyRewardChangedEvent>(_ => Invalidate());
// evaluator: dailyRewardManager.ClaimableCount > 0
```

## 10. Verify

Run the feature spec's verification script (in `FEATURES/<feature>.md`). Common checks for every feature:

- Claim flow grants through **your** granter (watch your currency change) and the package UI updates from the event.
- Cooldowns/timers survive app pause/resume; kill-app/reopen restores persisted state (and resets session-scoped state where specified).
- Editor ad path: rewarded flows complete immediately in-editor without an SDK.
- Open/close the panel repeatedly — no duplicated listeners, no double grants on button spam.
