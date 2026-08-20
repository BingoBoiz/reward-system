# Consumer style profile — the ASMR_Tower team

Who this package is written for. Researched 2026-08-20 from `D:\Fork\speed-clicker\Speed_Clicker\Assets\_ASMR_Tower` (plus the `_Speed_Clicker` shared code it depends on). **This team is already a live consumer**: their `Packages/manifest.json` points at this repo (`com.nabagame.reward` git URL, `#main`) and the `RewardDemo` sample is imported under their `Assets/Samples/NabaGame Reward/0.4.0/`.

The package API must feel native to how they already write code — while refusing to import their bad habits. When ARCHITECTURE.md and this file disagree, ARCHITECTURE.md wins.

## Hard facts about their codebase

- **All their game code is global-namespace `Assembly-CSharp`.** Zero namespaces, zero asmdefs of their own. They consume namespaces only via `using` (`NabaGame.Core.Runtime.EventManager`, `NabaGame.UI`, `BMH.Ads`, `DG.Tweening`, `Sirenix.OdinInspector`).
  - ⚠ **Name-collision risk:** their global scope already defines `RewardType`, `GameMode`, `OfferType`, `RewardCheckPointData`, `RewardCheckPointInfo`, and a `UIManager`. Package type names must stay distinctive (feature-prefixed) because `using NabaGame.Reward;` in their files resolves against all of that.
- **Google Sheet importer is installed but provably unused** (zero references in their `Assets/`). Remote config likewise. All tuning is typed into the Inspector or hardcoded. Do not build any data path on the sheet importer.
- **UniTask is installed but foreign to them** (3 files project-wide, none in ASMR_Tower). Package may use UniTask internally; the public API must be `void` + callbacks + events, never `async UniTask<T>`.
- Their `HomePanel` already contains empty `public void OpenDaily() { }` / `OpenSpin() { }` / `BuyNoAds() { }` stubs wired to scene buttons — waiting for this package.

## Habits to match

1. **Data = flat `[Serializable]` row + Odin `[TableList] List<Row>`, filled in the Inspector.** Their `RawDailyGiftData` row is `{ int ID; RewardType RewardType; int Qty; float Time; }` — flat public fields, no hierarchy. They also ship a hand-filled Odin `Dictionary<GameMode, List<Row>>` asset (`RewardCheckPointData`), so Inspector-filled dictionaries are proven too. Polymorphic rows use `[ShowIf]` on one flat class, never `[SerializeReference]`.
2. **Messaging = `EventManager` + `GameEvent` subclass** with a couple of public fields and a constructor: `EventManager.Instance.Raise(new ASMRCheckPointEvent(ID));`. It is their only bus.
3. **Subscribe in `Start()`, then prime the handler immediately** with a synthetic event carrying current state (their `MoneyBar` idiom). Listeners expect payloads to carry the **full current value, not a delta**, and to be safely callable at any time.
4. **Buttons are wired in the Inspector (`onClick` dropdown), not `AddListener`.** Zero `AddListener` calls in ASMR_Tower code (the older `_Speed_Clicker` half does the opposite — support both). Consequence: anything a designer hooks must be **`public void` with zero args or one primitive/enum arg**.
5. **Panels: `BaseUI` subclass, `SetInfo(...)` populates then `Show()`, `Close()` cleans up then `Hide()`**, scene-placed, reached as a public field on a `UIManagerSingleton` (`UIManagerGlobal.Instance.ShopPanel.SetInfo();`). (`SetInfor` typo also occurs — mention both in docs, ship `SetInfo`.)
6. **Callbacks are plain `Action`;** ads are `AdManager.Instance.Show(AdsType.Rewarded, onDone, placement)`. Our `ShowRewardedAd` hook maps onto that in one line.
7. **DOTween for delays and counters** (`DOVirtual.DelayedCall`, `DOVirtual.Int` rolling a currency label) instead of coroutines.
8. **PlayerPrefs write-through with a cached in-memory field;** spend/grant methods return `bool` for affordability. Guard-clause early returns; `List.Find(x => x.ID == id)` lookups; `$"..."` interpolation.
9. **Odin vocabulary** — `SerializedScriptableObject`, `[TableList]`, `[Button]`, `[ShowIf]`, `[FoldoutGroup]` — is native to them.

## Habits to refuse (evidence exists for every one)

1. **Consumer-written persistence.** Their `ASMRProfile.AddMoney` writes `PlayerPrefs.SetInt("asmr_money", 0)` — literal `0`, a shipped save-loss bug nobody noticed. The package owns its persistence entirely; the consumer never hand-writes a `PlayerPrefs.Set*` for package state.
2. **Ad-hoc magic-string keys** (`"asmr_money"`, `$"ASMR_{MapTypePlay}"` duplicated across files). Package keys are centralized `const` with the `NabaReward.` prefix.
3. **God classes / install-by-editing-GameManager.** Their 847-line `GameManager` must be edited to add anything. Installing this package must require zero edits to any *existing* host manager — the dev authors one small new `Sample*`-style manager instead, which is a new file, not a god-class edit.
4. **`Update()` polling for one-shot timers** (their `LoadingPanel` ticks every frame to fire one callback). Package timing stays on `TimeScheduler`.
5. **Reach-through singleton chains** (`GameManager.Instance.PlayerProfile.asmrProfile.AddMoney(...)` in 6+ files). The package never touches a host singleton; everything crosses via hooks and events.
6. **Empty methods on the success path.** Their `UnlockReward()` is empty *and called after a rewarded ad* — player watches an ad, receives nothing, silently. This is exactly why every package grant is `Debug.Log`ged and a claimed row whose `OnClaimed` is null LogErrors "was NOT granted".
7. **Silent non-exhaustive `switch`** (5 empty `case` branches leaving UI dead) and **unguarded dictionary/list indexing from persisted state** (`CheckPoints[ID]` with `ID` from PlayerPrefs, no bounds check). Package validates rows at `StartClass` and clamps/errors on persisted indices against current list length.
8. **Mutable shared event payloads** (one cached `MoneyEvent` mutated and re-raised). Package events are freshly allocated per raise.
9. Their code has zero `try/catch` swallowing and zero `FindObjectOfType` — keep both absent.

## What this means for the package API

- **Filling data:** a serialized `[TableList] List<{Feature}Row>` on the dev's own scene-placed manager is simultaneously their `RawDailyGiftData` shape and their Inspector-filling mechanism. Rows carry the icon `Sprite` inline (they resolve art via direct references, not string lookups). Code-side filling before `SetInfo(rows)` also works via the row constructors.
- **Granting:** the row's `OnClaimed` callback (decision #22) — the grant reaction lives on the data class the dev already fills; no bus subscription to forget. Notification `GameEvent`s on the shared `EventManager` remain for red dots/HUD (their `MoneyBar` pattern already proves it feels right).
- **Opening panels:** `OpenPanel()` / `ClosePanel()` (decision #25 — the old parameterless `SetInfo()`/`Close()` aliases are gone; `SetInfo` is the init verb now). Their empty `HomePanel.OpenDaily()` stub becomes one line: `SampleUIRoot.Instance.dailyRewardPanel.OpenPanel();`.
- **Buttons may be missing:** they never null-check a serialized `Button` and will hand the package half-wired prefabs — every package UI reference is therefore optional by design (decision #26).
