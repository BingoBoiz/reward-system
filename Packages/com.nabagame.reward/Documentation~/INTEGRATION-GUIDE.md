# Integration Guide

How to install `com.nabagame.reward` into a NabaGame host project, from zero to a working feature. The demo host in this repo (`Assets/_RewardDemo/`, a symlink view of `Samples~/RewardDemo/`) is the living reference for every step.

The whole contract in one paragraph: **drag one panel prefab, write one tiny manager, assign three static hooks at boot.** The panel owns everything (save, timers, ads, IAP, rules); your manager owns the data (`rows`) and the grant reaction (`OnClaimed`). Read a panel's `#region API` — that is the entire surface you need.

## 1. Prerequisites

Install these first — git package dependencies are **not** auto-resolved by Unity:

- `com.nabagame.core` (git) — EventManager, Singleton
- `com.nabagame.ui` (git) — BaseUI, UIPanel, UIManagerSingleton
- `com.cysharp.unitask` (git)
- Odin Inspector vendored under `Assets/Plugins/Sirenix/`
- DOTween vendored under `Assets/Plugins/Demigiant/`

Unity 2022.3+. No ads SDK is required by the package itself; the adapters below are one line each onto whatever the host uses.

## 2. Add the package

- Development repo: already embedded under `Packages/com.nabagame.reward/`.
- Other hosts: Package Manager → *Add package from git URL*, or copy the package folder into `Packages/`. Confirm "NabaGame Reward" appears in Package Manager.
- **Upgrading from ≤0.7.0: delete any old imported sample first** (`Assets/Samples/NabaGame Reward/<old version>/`) — six sample types were renamed with the `Sample` prefix and the stale copies are duplicate-definition compile errors.

## 3. Import the Reward Demo sample

Package Manager → NabaGame Reward → Samples → **Reward Demo** → Import. One scene (`RewardSample.unity`), the `Sample*` scripts, prefabs, and filled sample data land under `Assets/Samples/NabaGame Reward/...`. Everything prefixed `Sample` is plain game-side code you own after import — copy and adapt it.

## 4. Write your manager and fill the rows

The package owns no data and ships no manager. Your manager is ~15 lines — the sample's `SampleDailyRewardManager` is the template:

```csharp
public class SampleDailyRewardManager : MonoBehaviour
{
    [TableList] public List<DailyRewardRow> rows = new List<DailyRewardRow>();

    public void SetInfo()
    {
        foreach (DailyRewardRow row in rows) row.OnClaimed = OnClaimed;
        SampleUIRoot.Instance.dailyRewardPanel.SetInfo(rows);
    }

    void OnClaimed(DailyRewardRow row) => SampleRewardGranter.Grant(row.Key, row.Icon, row.Amount);

    void OnValidate() => DailyRewardRow.Warn(rows, this);
}
```

Fill `rows` in the Inspector (Odin `[TableList]`) or from code — the constructor documents what to fill:

```csharp
rows = new List<DailyRewardRow>
{
    new DailyRewardRow("cash", 7500000, icon: cashIcon),
    new DailyRewardRow("spin", 1, icon: spinIcon, labelOverride: "RARE"),
    // ... list position IS the day; no Day/Wedge/Slot field exists
};
```

Everything you fill lives in the one row class: `Key` (your own vocabulary — the package never interprets it), `Icon`, `Amount`, `ClaimSfx` (per-reward audio), `OnClaimed` (the grant callback), plus per-feature extras (`LabelOverride`/`HideIconUntilClaim`, `Weight`, `UnlockAfterSeconds`).

**Incomplete data warns, it never breaks**: missing icons/keys produce one aggregated Console warning naming each gap (`DailyRewardRow.Warn`), and the feature keeps running — finish filling at your own pace. Only structure that cannot run throws: an empty list, fewer than 2 wedges, non-increasing unlock times.

## 5. Handle the grant — `Row.OnClaimed`, mandatory

There are no grant events. **The row's `OnClaimed` callback is the only grant path** — your manager assigns it before `SetInfo` (step 4). If a claimed row's `OnClaimed` is null, nothing is granted and the Console shows `'key' xN was NOT granted`. Every grant also logs key + amount as an audit line.

Your handler switches on `Key` and must fail loudly on an unknown one:

```csharp
public static void Grant(string key, Sprite icon, long amount)
{
    switch (key)
    {
        case "cash": GameManager.Instance.PlayerProfile.asmrProfile.AddMoney((int)amount); break;
        // ... every key used in your row lists
        default: Debug.LogError($"Unknown reward key '{key}' x{amount} — no grant mapping"); return;
    }
    // then: your ItemGrantedEvent / ceremony popup / tracking
}
```

The ceremony popup is host-side by design: the sample's granter raises `SampleItemGrantedEvent` and `SampleItemReceivedPanel` (sorting 250) batches same-frame grants, stacks duplicates by key, and plays the ceremony. The package never opens it.

## 6. Assign the hooks — three statics, once, at boot

```csharp
// first lines of your boot Start(), before any panel SetInfo
RewardHooks.PlaySfx        = clip => { if (clip) SoundManager.Instance.sfxSource.PlayOneShot(clip); };
RewardHooks.ShowRewardedAd = (placement, onReward, onSkip) => AdManager.Instance.ShowRewardedVideo(onReward, onSkip, placement);
RewardHooks.PurchaseIap    = (productId, result) => IAPManager.Instance.InitiatePurchase(productId, result);
```

Unset hooks never throw — the defaults `Debug.LogError` naming the hook and then reward/succeed immediately, so a freshly dragged prefab runs before you wire anything. `PurchaseIap` product ids (e.g. `DailyRewardPanel.openAllIapProductId`) must be registered in your IAP catalog; the displayed price is the panel's `openAllIapPriceText` string — the package never queries the store.

## 7. Wire the panel

1. Drop the feature panel prefab (e.g. `DailyRewardPanel` from the sample) under your UI root; add a field for it on your `UIManagerSingleton` (or use the sample's `SampleUIRoot`, which auto-finds panels in `OnValidate`).
2. Tune the panel's Inspector knobs if needed — they live on the panel prefab: Daily `openAllAdsRequired`/`openAllIapProductId`/`openAllIapPriceText`; Spin `freeSpinCooldownSeconds`/`spinDurationSeconds`; Online `x2DurationSeconds`/`x5DurationSeconds`/`x5AdsRequired`.
3. Call your manager's `SetInfo()` **from `Start()` at boot** (see `SampleRewardBoot`) — never from `Awake()` (`UIPanel` applies `startHidden` in its own `Start()`), and never open a panel in the same frame it was initialized.
   - **Online Reward must be initialized at boot, not lazily on first open** — playtime accrues from `SetInfo`; a lazy init means the grid never unlocks while the panel is closed.
4. Open it from any button or stub with `OpenPanel()` / close with `ClosePanel()`:
   `public void OpenDaily() { SampleUIRoot.Instance.dailyRewardPanel.OpenPanel(); }`

Any serialized button/label/badge on the panel may be disabled or deleted — the panel guards every reference and simply drops that affordance; nothing throws.

## 8. Register ad placements

Placement strings are `public const` in each panel's `#region API` (`OnlineReward_x2Speed`, `OnlineReward_x5Speed`, `OnlineReward_OpenAll`, `LuckySpin_AdSpin`, `DailyReward_OpenAll`). Register them in your mediation dashboard/tracking the same way you do for game placements.

## 9. Red dots (optional)

Subscribe to the package change events and evaluate from the panel queries:

```csharp
EventManager.Instance.AddListener<DailyRewardChangedEvent>(_ => Invalidate());
// evaluators: dailyRewardPanel.ClaimableCount > 0, luckySpinPanel.FreeSpinReady, onlineRewardPanel.HasClaimable
```

`SampleHomeButtons` is the complete recipe.

## 10. Installing into ASMR_Tower specifically

The sample is skinned with the ASMR_Tower art set on purpose — it drops into `_ASMR_Tower` and looks native:

1. **Delete `Assets/Samples/NabaGame Reward/0.4.0/` first** (see step 2 — otherwise CS0101 duplicate types).
2. Import the sample; drag the sample's `SampleUIRoot` prefab (self-contained canvas root with all panels) and a `SampleRewardBoot` object with the three `Sample*` managers into `Scn_GP_ASMR_Tower.unity`. No edits to `UIManagerGlobal.cs` are required.
3. Fill the three existing `HomePanel` stubs, one line each — the buttons (`btDailyReward`, `btSpin`, `btNoAds`) already exist in the scene with empty `onClick` slots; drag `HomePanel` into them and pick the method:
   ```csharp
   public void OpenDaily() { SampleUIRoot.Instance.dailyRewardPanel.OpenPanel(); }
   public void OpenSpin()  { SampleUIRoot.Instance.luckySpinPanel.OpenPanel(); }
   ```
4. Replace the sample adapters in `SampleRewardBoot.SetInfo()` with the host's services (exact one-liners in step 6), and point the granter at `GameManager.Instance.PlayerProfile.asmrProfile.AddMoney(...)` — the `MoneyBar` HUD updates itself via `MoneyEvent`.
5. Known **host** issues to report to the ASMR team (not package bugs, but they will look like it):
   - `ASMRProfile.AddMoney` saves a literal `0` to PlayerPrefs (`PlayerPrefs.SetInt("asmr_money", 0)`) — all granted currency evaporates on relaunch until they fix it to save `Money`.
   - The `UIManagerGlobal` root GameObject is saved inactive in `Scn_GP_ASMR_Tower.unity`, so `UIManagerGlobal.Instance` resolves null as the scene stands.

## 11. Verify

Run the feature spec's verification script (in `FEATURES/<feature>.md`). Common checks for every feature:

- A claim lands in **your** `OnClaimed` handler (watch your currency change) and the Console shows the grant audit line. A logged grant with no currency change means your `OnClaimed` assignment or key mapping is wrong.
- Cooldowns/timers survive backgrounding (focus loss) and kill-app/reopen restores persisted state (session-scoped state resets where specified).
- Editor ad path: rewarded flows complete immediately in-editor without an SDK.
- Open/close the panel repeatedly — no duplicated listeners, no double grants on button spam.
- Disable or delete any serialized button/label on the panel — the feature degrades silently, nothing throws or gets stuck.
