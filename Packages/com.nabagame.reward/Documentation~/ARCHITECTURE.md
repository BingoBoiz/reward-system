# Architecture

How `com.nabagame.reward` is built and why. Read this before writing any package code or integrating the package into a game.

## 1. Dependency rules

```
Assembly-CSharp (host game: GameController, UIManager, AudioManager, game enums, adapters)
      │  references ▼            (never the other way)
NabaGame.Reward  /  NabaGame.Reward.Editor        (this package's asmdefs)
      │  references ▼
com.bmh.core.runtime (EventManager, Singleton) · com.nabagame.ui.runtime (BaseUI, UIPanel)
UniTask · Sirenix (Odin, precompiled) · DOTween (precompiled, vendored in host)
```

- The package **never references game code or game types**. The asmdef makes this physically impossible, not just a convention: `Assembly-CSharp` can see the package, the package cannot see `Assembly-CSharp`.
- Anything the package needs from the game (economy payout, SFX playback, rewarded ads, ceremony popups) enters through the **hooks contract** (§3) at initialization.
- Hard dependencies are limited to what every NabaGame project always has: `com.nabagame.core`, `com.nabagame.ui`, UniTask, Odin. DOTween is assumed vendored in the host (`Assets/Plugins/Demigiant/`, precompiled DLL — auto-referenced by asmdefs). No ads SDK, no save plugin.
- The package does **not** ship or reference game enums (`RewardType`, `RewardID`, `CrateID`, `RedDotKey`, …). Each game has its own list; the package stays agnostic via the reward model below.

## 2. Reward model — catalog + importer-shaped configs + `IRewardGranter`

**Sheet owns numbers, Unity owns art.** Every data table the package reads is shaped so the NabaGame Googlesheet Importer (original `com.nabagame.googlesheet.importer` or the `com.feeder.editortools` fork) can write it with *Generate Assets* and no host-side bake:

- a row is a `[Serializable]` POCO with **public primitive fields declared in sheet-column order** (original importer maps by index, Feeder by name), no constructor, no public `const`/`static`;
- the table is a `ScriptableObject` named `{Row}Data` with a **public `List<{Row}> {row}s`** field (importer contract — the one place the package uses a public camelCase field); every other field on that SO (tuning knobs) survives re-import because the importer only rewrites the list;
- the sheet tab is `{Row}` minus the `Row` suffix, cell A1 = the row class name; the asset lands at `Assets/<AssetFolder>/Raw<Tab>.asset`.

```csharp
// NabaGame.Reward — the host's reward vocabulary
[Serializable] public class RewardItem { public string Key; public string DisplayName; public Sprite Icon; }
public class RewardItemData : ScriptableObject { public List<RewardItem> rewardItems; public RewardItem Get(string key); /* throws on unknown key */ }

// feature tables
[Serializable] public class DailyRewardRow { public int Day; public string Key; public long Amount; public string LabelOverride; public bool HideIconUntilClaim; }
public class DailyRewardRowData : ScriptableObject { public List<DailyRewardRow> dailyRewardRows; }

[Serializable] public class LuckySpinRow { public int Wedge; public string Key; public long Amount; public int Weight; }
public class LuckySpinRowData : ScriptableObject { public List<LuckySpinRow> luckySpinRows; public int FreeSpinCooldownSeconds; public float SpinDurationSeconds; public int SpinFullTurns; }
```

`RewardItemData` is the catalog (the role `SpriteCollection` plays in NabaGame games): it is the only table carrying sprites, so with the original importer it is authored in Unity; the Feeder fork fills it from a `RewardItem` tab including `sp_Icon` by file name. Feature rows reference rewards by `Key` only; each manager resolves `Key → RewardItem` through `hooks.Catalog` in `StartClass` and throws on an unknown key, wrong `Day`/`Wedge` order, non-positive `Amount`/`Weight`. `LabelOverride == "-"` means "no override" because the importer rejects blank cells. Per-slot art that is not keyed by a reward (daily card backgrounds) lives on the panel prefab, never in a row.

Payout crosses into the game through one interface:

```csharp
public interface IRewardGranter
{
    // Must fail loudly (throw or Debug.LogError naming the key) on an unknown key.
    void Grant(RewardItem item, long amount);
}
```

The host implements it once, mapping `item.Key` → its own `RewardType`/profiles/save. This is the same "single grant chokepoint" idea as the legacy `RewardGranter`, with the enum knowledge moved to the host side of the boundary.

## 3. Hooks — host services and host data injected at `StartClass`

Each feature manager is initialized explicitly by the host (StartClass convention, see §7) and receives everything it needs from the game in one struct-like object:

```csharp
public sealed class RewardHooks
{
    public IRewardGranter Granter;                                  // required
    public RewardItemData Catalog;                                  // required; Key -> icon/name, the host's reward vocabulary
    public Action<AudioClip> PlaySfx;                               // required; host routes into its AudioManager
    public Action<string, Action, Action> ShowRewardedAd;           // required where a feature uses ads:
                                                                    // (placement, onReward, onSkip)
    // grows only when a return value / flow control is needed; notifications use events instead (§4)
}
```

- `StartClass(config, hooks)` **validates hooks immediately**: a missing required hook throws `InvalidOperationException` naming the field. Never a silent no-op — a claim that quietly grants nothing is the worst failure mode (see fail-loud policy in CONVENTIONS.md).
- No DI framework and no static service registration: injection at the existing `StartClass` seam keeps hosts free of extra frameworks and makes the requirement compile-time-visible at the single call site.
- Ads are a hook, not a dependency, so the package works with any mediation and is trivially fakeable in tests/editor. A ready adapter for `com.bmh.ads` ships in `Samples~`.

## 4. Events — notifications via NabaGame EventManager

Commands and queries are **direct calls** on the feature manager (UI button → `manager.ClaimDaily()`, label → `manager.GetRemainingSeconds(i)`). State-change **notifications** are events:

- The package defines its own event classes in Runtime, deriving from `NabaGame.Core.Runtime.EventManager.GameEvent`, e.g. `DailyRewardChangedEvent`, `OnlineRewardChangedEvent`, `SpinResultEvent`.
- Raised via `EventManager.Instance.Raise(...)` after the owning manager mutates state; package panels and host systems both subscribe with `AddListener<T>` / `RemoveListener<T>`.
- Rationale: `EventManager` lives in `com.nabagame.core`, a shared dependency, so package→host notifications need no extra plumbing, and listeners are optional by design (unlike grants, where silence is a bug — that is why granting is an interface, not an event).

**Red dots** stay host-side: the host's `RedDotManager` subscribes to the package change events and evaluates from public API (`dailyReward.ClaimableCount > 0`, `onlineReward.HasClaimable`, `luckySpin.FreeSpinReady`). The package neither knows the host's `RedDotKey` enum nor renders badges outside its own panels.

## 5. Time — `TimeScheduler`, no `Update()` polling

`TimeScheduler` (moved from the legacy `_GameBase/Scripts/_Others/TimeScheduler.cs` into package Runtime, namespace `NabaGame.Reward`) is the only timing mechanism feature managers may use:

```csharp
public static class TimeScheduler
{
    public static long NowMs { get; }                       // DateTimeOffset.UtcNow unix ms
    public static double SecondsUntil(long atUnixMs);
    public static Handle Schedule(long atUnixMs, Action callback);
    public static void Cancel(ref Handle handle);           // nulls the caller's field
}
```

One shared 1-second `UniTask.Delay(1000, DelayType.Realtime)` loop re-checks wall-clock deadlines each tick, so app suspend/resume needs no per-manager catch-up. A `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` reset keeps it correct with domain reload disabled. Managers never poll in `Update()`; UI countdown labels may run their own 1-second unscaled refresh loop while visible.

Accrued-time features (Online Reward) use the **baseline pattern**: elapsed = `(Time.realtimeSinceStartupAsDouble - sessionStartRealtime) * multiplier`, flushed into the profile on multiplier change and on pause/quit — never accumulated per frame.

## 6. Save — PlayerPrefs + JsonUtility

No Easy Save 3, no save plugin. Each feature owns a plain serializable profile class stored as JSON in one PlayerPrefs key:

- Key scheme: `NabaReward.Daily`, `NabaReward.Online`, `NabaReward.Spin`.
- A small Runtime base handles load/save: `JsonUtility.FromJson` on `StartClass` (missing key → fresh profile), `JsonUtility.ToJson` + `PlayerPrefs.Save()` on every state mutation and on `OnApplicationPause(true)` / `OnApplicationQuit`.
- Every profile carries an `int Version` field; loading an older version runs an explicit migration or logs an error naming the key — never silently reinterprets fields.
- Session-scoped state (Online Reward's grid, see its spec) simply lives outside the persisted profile.
- PlayerPrefs stores small strings only; profiles must stay flat and small (no textures, no per-item objects beyond simple lists).

## 7. UI — panels, StartClass, idempotency

- Every package popup inherits `NabaGame.UI.BaseUI` (`Show()`/`Hide()` are visibility truth; `OnIn/OutAnimation*` virtuals for presentation). One standalone popup per feature — no shared tabbed shell, so a host taking one feature drags in nothing about the others.
- Panels and widgets are **prefab-authored** in the package; references are `[SerializeField]`, auto-wired by `OnValidate()` where stable. No runtime `new GameObject()` UI; dynamic lists instantiate a disabled authored template.
- Initialization follows the NabaGame `StartClass()` convention: the host's `GameController` chain calls the feature manager's `StartClass(config, hooks)`, the host's `UIManager` calls the panel's `StartClass()`. `StartClass` must be **idempotent for subscriptions**: `RemoveListener` before `AddListener`, `onClick.RemoveListener` before `AddListener`.
- The host registers the package panel in its own UIManager exactly like a local panel: one serialized field + one `StartClass()` call + popup tracking. The package does not know the host's UIManager type.
- Panels render state and forward intent; every rule lives in the feature manager. Kill panel-owned tweens on close; use unscaled time for UI that runs while gameplay is paused; guard buttons with UniTask while an async action runs.

## 8. Ads idiom — `AdFlow`

All rewarded-ad flows go through one Runtime helper built on the `ShowRewardedAd` hook, encapsulating the idiom proven in paint-and-seek:

- `adBusy` guard against double-taps.
- In-editor immediate `onReward` (so flows are testable without an SDK) — the sample `com.bmh.ads` adapter already behaves this way via the host.
- `UniTask.NextFrame` release of the guard, because a mediation SDK can drop a request without firing either callback.
- Placement naming convention: `"<Feature>_<Action>"` — `OnlineReward_x2Speed`, `OnlineReward_x5Speed`, `LuckySpin_AdSpin`. Placements are listed in each feature spec and must be registered by the host.

## 9. Decision record

| # | Decision | Rationale |
|---|---|---|
| 1 | One embedded UPM package, later published to GitLab | Matches the existing `com.nabagame.*` git-package ecosystem; one version to maintain |
| 2 | Runtime/Editor asmdefs | Compile-time isolation + faster iteration; locks the dependency direction |
| 3 | Hard-dep only on core/ui/UniTask/Odin (+DOTween assumption) | Guaranteed present in every NabaGame project |
| 4 | Game enums never ship; string `Key` + `RewardItemData` catalog + `IRewardGranter` | Every game has different enums; string key + host mapping keeps the package portable |
| 5 | Hooks injected at `StartClass`, no DI framework | Explicit, compile-time visible, zero framework burden on hosts |
| 6 | Ads via hook, sample adapter for `com.bmh.ads` | SDK-agnostic, fakeable; adapter cost is one small class per host |
| 7 | Hybrid communication: direct calls for commands, EventManager events for notifications | Traceable request/response; decoupled broadcasts on shared infra |
| 8 | PlayerPrefs + JsonUtility save | Team direction; no paid plugin dependency |
| 9 | Online Reward session-scoped | Matches the mockup ("rewards reset if you leave") |
| 10 | One popup per feature, tabbed RewardPanel retired | Matches mockups; removes the shared shell coupling all features together |
| 11 | Legacy free/ads-watch tab removed | Not part of the package scope |
| 12 | Per-feature managers + per-feature profiles | A host taking one feature receives nothing about the others |
| 13 | `Assets/_GameBase` in this repo = demo host | Living reference implementation of every adapter and wiring step |
| 14 | Docs in English, namespace `NabaGame.Reward` | Package convention |
| 15 | Configs are importer-shaped (`{Row}` POCO + `{Row}Data` SO + public `{row}s` list), catalog resolved via `hooks.Catalog` (2026-08-19, supersedes the `RewardItem` ScriptableObject-per-reward shape) | The company authors reward content in Google Sheets; the importer emits one list-SO per tab, so a per-reward asset + object-ref rows forced every host to write a bake step. Sheet-shaped rows + a single catalog make *Generate Assets* the whole integration |
