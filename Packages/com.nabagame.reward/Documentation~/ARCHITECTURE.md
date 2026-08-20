# Architecture

How `com.nabagame.reward` is built and why. Read this before writing any package code or integrating the package into a game.

## 1. Dependency rules

```
Assembly-CSharp (host game: its own Sample-style managers, UI root, adapters)
      │  references ▼            (never the other way)
NabaGame.Reward                                   (this package's asmdef)
      │  references ▼
com.bmh.core.runtime (EventManager, Singleton) · com.nabagame.ui.runtime (BaseUI, UIPanel)
UniTask · Sirenix (Odin, precompiled) · DOTween (precompiled, vendored in host)
```

- The package **never references game code or game types**. The asmdef makes this physically impossible, not just a convention: `Assembly-CSharp` can see the package, the package cannot see `Assembly-CSharp`.
- Services the package needs from the game (SFX playback, rewarded ads, IAP) enter through the **static hooks** (§3) assigned once at boot; reward data enters through the **row list** passed to each panel's `SetInfo` (§2); grants leave through each row's **`OnClaimed` callback** (§2); state changes are additionally announced as **notification events** (§4).
- Hard dependencies are limited to what every NabaGame project always has: `com.nabagame.core`, `com.nabagame.ui`, UniTask, Odin. DOTween is assumed vendored in the host (`Assets/Plugins/Demigiant/`, precompiled DLL — auto-referenced by asmdefs). No ads SDK, no save plugin.
- The package does **not** ship or reference game enums (`RewardType`, `RewardID`, `CrateID`, `RedDotKey`, …). Each game has its own list; the package stays agnostic via the reward model below.

## 2. Reward model — one panel, one row list, grants via `OnClaimed`

**The package ships one prefab per feature — the panel — and the panel owns everything**: PlayerPrefs save, timers, ads, IAP, claim/spin rules (decision #21 removed the per-feature manager MonoBehaviours from the package). The host writes one tiny manager of its own that holds the data and feeds the panel:

```csharp
// host code — this manager is the dev's job; the sample ships one per feature as the template
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

**Everything the dev fills lives in one row class per feature** — identity, art, audio, and the grant callback; nothing is scattered:

```csharp
// NabaGame.Reward — list position is the index (rows[0] = day 1 / 12-o'clock wedge / first slot)
[Serializable] public class DailyRewardRow  { public string Key; public Sprite Icon; public long Amount; public AudioClip ClaimSfx; public string LabelOverride; public bool HideIconUntilClaim; [NonSerialized] public Action<DailyRewardRow> OnClaimed; }
[Serializable] public class LuckySpinRow   { public string Key; public Sprite Icon; public long Amount; public int Weight; public AudioClip ClaimSfx; [NonSerialized] public Action<LuckySpinRow> OnClaimed; }
[Serializable] public class OnlineRewardRow { public string Key; public Sprite Icon; public long Amount; public int UnlockAfterSeconds; public AudioClip ClaimSfx; [NonSerialized] public Action<OnlineRewardRow> OnClaimed; }
```

- Rows are flat `[Serializable]` POCOs with public fields — the exact shape the consumer team already authors (`RawDailyGift`, see CONSUMER-STYLE.md). Each row class ships a constructor whose parameters document what to fill for code-side authoring; the parameterless one keeps Inspector `[TableList]` authoring working. Both paths are first-class.
- `Key` is an opaque string the package never interprets; it exists so the host's `OnClaimed` handler can `switch` on it. `Icon`/`LabelOverride` render as-is (empty = no override). Feature-level tuning knobs (`freeSpinCooldownSeconds`, `spinDurationSeconds`, speed-up settings, Open-All ads/IAP config) are `[SerializeField]`s on the **panel prefab**.
- **Grants travel through `Row.OnClaimed`, not events** (decision #22 supersedes #17): after the panel mutates and saves state, it `Debug.Log`s the grant (mandatory audit line) and invokes the row's `OnClaimed`. A null `OnClaimed` grants nothing and `Debug.LogError`s naming the row — the host's handler must fail loudly on an unknown `Key` the same way.
- **Validation is lenient by design** (decision #24): missing `Icon`/`Key`/`Amount` produce one aggregated `Debug.LogWarning` naming each index and field (`{Feature}Row.Warn(rows)`, also callable from the host manager's `OnValidate`) — the feature keeps running. Only structure that breaks the machine throws: a null/empty list, fewer than 2 wedges, non-increasing `UnlockAfterSeconds`. Persisted indices loaded from PlayerPrefs are range-checked against the current list before use.
- The panel shows the received rows read-only in its Inspector (`[ShowInInspector, ReadOnly, TableList]`) so the dev can see at runtime exactly what their manager passed in.

## 3. Hooks — static host services, assigned once at boot

`SetInfo(rows)` carries data only, so app-wide services live on a static class the host assigns **before** any panel `SetInfo` (decision #23 supersedes the injection half of #5):

```csharp
public static class RewardHooks
{
    public static Action<AudioClip> PlaySfx;                 // default: silent no-op
    public static Action<string, Action, Action> ShowRewardedAd;   // (placement, onReward, onSkip)
                                                             // default: LogError + reward immediately
    public static Action<string, Action<bool>> PurchaseIap;  // (productId, result) — result MUST fire on
                                                             // success, failure, and cancel
                                                             // default: LogError + succeed
}
```

- **Defaults, not nulls**: a freshly dragged prefab with no boot code still works — unset ads/IAP hooks `Debug.LogError` naming themselves and then behave as if rewarded/succeeded. There is no `Validate`-and-throw; leniency is the contract (decision #24).
- A `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` reset restores the defaults each play session — mandatory, or delegates closing over destroyed objects survive when domain reload is disabled.
- Ads are a hook, not a dependency, so the package works with any mediation and is trivially fakeable. The consumer adapter is one line onto `AdManager.Instance.ShowRewardedVideo(onReward, onSkip, placement)`; IAP is one line onto `IAPManager.Instance.InitiatePurchase(productId, callback)`. The package never learns prices from the store — display price is a dev-supplied string on the panel (decision #20).

## 4. Events — notifications for the host

Queries are **direct calls** on the panel (`dailyRewardPanel.ClaimableCount`). Grants travel through `OnClaimed` (§2). What remains on the event bus is **notifications** — optional to listen to:

- `DailyRewardChangedEvent`, `LuckySpinChangedEvent`, `OnlineRewardChangedEvent` — state changed; refresh badges/HUD.
- `SpinStartedEvent { int WedgeIndex; float DurationSeconds; }`, `OnlineRewardSpeedUpEvent { int Multiplier; }`, `{Feature}PanelClosedEvent` — lifecycle signals.
- Raised via `EventManager.Instance.Raise(...)` with a **fresh event instance** per raise — never a cached, mutated one (a consumer-side bug we refuse to mirror, CONSUMER-STYLE.md). Payloads carry full values, not deltas.

**Red dots** stay host-side: the host subscribes to the change events and evaluates from the panel queries (`dailyRewardPanel.ClaimableCount > 0`, `onlineRewardPanel.HasClaimable`, `luckySpinPanel.FreeSpinReady`). The package neither knows the host's `RedDotKey` enum nor renders badges outside its own panels.

## 5. Time — `TimeScheduler`, no `Update()` polling

`TimeScheduler` (package Runtime, namespace `NabaGame.Reward`) is the only timing mechanism package code may use:

```csharp
public static class TimeScheduler
{
    public static long NowMs { get; }                       // DateTimeOffset.UtcNow unix ms
    public static double SecondsUntil(long atUnixMs);
    public static Handle Schedule(long atUnixMs, Action callback);
    public static void Cancel(ref Handle handle);           // nulls the caller's field
}
```

One shared 1-second `UniTask.Delay(1000, DelayType.Realtime)` loop re-checks wall-clock deadlines each tick, so app suspend/resume needs no per-panel catch-up. Callbacks run inside a `try/catch` — one throwing callback must not kill the shared loop. A `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` reset keeps it correct with domain reload disabled. No `Update()` polling; UI countdown labels run a 1-second unscaled refresh loop **gated on `IsVisible()`** so an external `Hide()` cannot leak it.

Accrued-time features (Online Reward) use the **baseline pattern**: elapsed = `(Time.realtimeSinceStartupAsDouble - baseline) * multiplier`, flushed on multiplier change and on focus loss. The flush listens to the static **`Application.focusChanged`**, not `OnApplicationPause` — a Unity message dies silently if the host's UI framework deactivates a hidden panel, while the static event fires regardless of GameObject state.

## 6. Save — PlayerPrefs + JsonUtility

No Easy Save 3, no save plugin. Each feature owns a plain serializable profile class stored as JSON in one PlayerPrefs key:

- Key scheme: `NabaReward.Daily`, `NabaReward.Online`, `NabaReward.Spin`.
- `RewardProfileStore` handles load/save: `JsonUtility.FromJson` on `SetInfo` (missing key → fresh profile), `JsonUtility.ToJson` + `PlayerPrefs.Save()` on **every state mutation** — there is no pause/quit save pass, because save-on-mutation already covers it and pause messages on a panel are unreliable (§5).
- Every profile carries an `int Version` field; loading an older version runs an explicit migration or logs an error naming the key — never silently reinterprets fields.
- Session-scoped state (Online Reward's grid, see its spec) simply lives outside the persisted profile.
- PlayerPrefs stores small strings only; profiles must stay flat and small.

## 7. UI — panels own the feature

- Every package popup inherits `NabaGame.UI.BaseUI` (`Show()`/`Hide()` are visibility truth). One standalone popup per feature — no shared tabbed shell, so a host taking one feature drags in exactly one prefab.
- **The panel owns all feature rules** — save, timers, ads, IAP, claim/spin state machine. There is no package-side manager to place, wire, or initialize; merging removed a whole class of manager↔panel desyncs (decision #21).
- **Lifecycle naming** (decision #25): `SetInfo(List<{Feature}Row>)` is the **single** init — validate, load save, start timers, bind rows onto the authored card/wedge list (fixed boards, decision #28) or build the cell list from the authored template (dynamic lists), bind listeners idempotently (`RemoveListener` before `AddListener`). `OpenPanel()` / `ClosePanel()` are the dev-facing activation APIs (refresh + `Show()` / `Hide()` + closed-event). The old parameterless `SetInfo()`/`Close()` aliases are deleted; `StartClass` is retired everywhere.
  - Call `SetInfo(rows)` from `Start()` at boot, never from `Awake()` (`UIPanel` applies `startHidden` in its own `Start()`), and never open a panel in the same frame.
  - The names `OpenPanel`/`ClosePanel` are load-bearing: the demo host's `BaseUIInspectorProcessor` matches them literally to draw the Odin open/close inspector buttons.
- **Every serialized UI reference is optional** (decision #26): the dev may disable or delete any button, label, badge, template, or authored card/wedge and nothing throws or gets stuck — dereferences are guarded, missing templates and empty authored lists `LogError` and skip, null authored-list entries are skipped silently, cell/wedge indexing is bounds-checked. A panel opened before `SetInfo` logs one error and refuses, instead of NRE-spamming.
- Each panel's members are grouped in four fixed regions, `API` first: `#region API` (SetInfo/OpenPanel/ClosePanel + red-dot queries + reset — everything a consuming dev needs), `#region Logic`, `#region UI`, `#region Debug`. A dev reads only the API region to use the feature.
- Panels are prefab-authored; references are `[SerializeField]`. No runtime `new GameObject()` UI. Two list models (decision #28): **fixed boards** (Daily's 7 cards, Spin's 8 wedges) are pre-authored instances wired into a serialized `List<>` — layout and skins live in the prefab; **dynamic lists** (Online cells, sample ItemReceived pool) instantiate a disabled authored template because their counts follow host data.

## 8. Ads idiom — `AdFlow`

All rewarded-ad flows go through one Runtime helper built on `RewardHooks.ShowRewardedAd`:

- `Busy` guard against double-taps, released a frame after either callback.
- **A ~15s timeout failsafe releases `Busy` even when the host SDK swallows both callbacks** (e.g. a reward-interval throttle) — an ad button may fail, but it must never stay dead. A late SDK callback after the timeout still grants.
- Unset hook → the `RewardHooks` default logs an error and rewards immediately, so flows are testable without an SDK in editor and on device.
- Placement naming convention: `"<Feature>_<Action>"` — `OnlineReward_x2Speed`, `OnlineReward_x5Speed`, `OnlineReward_OpenAll`, `LuckySpin_AdSpin`, `DailyReward_OpenAll`. Placements are `public const` in each panel's API region and must be registered by the host.

## 9. Decision record

| # | Decision | Rationale |
|---|---|---|
| 1 | One embedded UPM package, later published to GitLab | Matches the existing `com.nabagame.*` git-package ecosystem; one version to maintain |
| 2 | Runtime/Editor asmdefs | Compile-time isolation + faster iteration; locks the dependency direction |
| 3 | Hard-dep only on core/ui/UniTask/Odin (+DOTween assumption) | Guaranteed present in every NabaGame project |
| 4 | Game enums never ship; string `Key` + `RewardItemData` catalog + `IRewardGranter` | Every game has different enums; string key + host mapping keeps the package portable. *Catalog/`IRewardGranter` half superseded by #16/#17; the "no game enums, opaque string `Key`" half stands* |
| 5 | ~~Hooks injected at `StartClass`, no DI framework~~ (superseded by #23 on 2026-08-20) | Was: explicit, compile-time visible injection. `SetInfo(rows)` carries data only, so services moved to statics |
| 6 | Ads via hook, sample adapter for `com.bmh.ads` | SDK-agnostic, fakeable; adapter cost is one small class per host |
| 7 | Hybrid communication: direct calls for commands, EventManager events for notifications | Traceable request/response; decoupled broadcasts on shared infra |
| 8 | PlayerPrefs + JsonUtility save | Team direction; no paid plugin dependency |
| 9 | Online Reward session-scoped | Matches the mockup ("rewards reset if you leave") |
| 10 | One popup per feature, tabbed RewardPanel retired | Matches mockups; removes the shared shell coupling all features together |
| 11 | Legacy free/ads-watch tab removed | Not part of the package scope |
| 12 | ~~Per-feature managers~~ + per-feature profiles (manager half superseded by #21 on 2026-08-20) | A host taking one feature receives nothing about the others — now true at the prefab level too |
| 13 | `Assets/_RewardDemo` in this repo = demo host (symlinked to `Samples~/RewardDemo`, single source) | Living reference implementation of every adapter and wiring step |
| 14 | Docs in English, namespace `NabaGame.Reward` | Package convention |
| 15 | ~~Configs are importer-shaped (`{Row}` POCO + `{Row}Data` SO + public `{row}s` list), catalog resolved via `hooks.Catalog`~~ (2026-08-19; **superseded by #16** on 2026-08-20) | Was: sheet-shaped rows + a single catalog make *Generate Assets* the whole integration. Research of the actual consumer (CONSUMER-STYLE.md) showed the importer is installed but unused there — the premise did not hold |
| 16 | **The package owns no data** (2026-08-20, supersedes #15): the host fills a serialized `[TableList] List<{Feature}Row>` (Inspector or code); rows carry `Sprite Icon` inline; no catalog, no config SOs, no `[CreateAssetMenu]` | Matches how the consumer team actually authors data (flat `[TableList]` rows filled in the Inspector, sheet importer unused); removes a whole asset-creation step from integration |
| 17 | ~~Grants are events, not an interface~~ (2026-08-20; **superseded by #22** on 2026-08-20): player actions raised grant-carrying events the host subscribed to | Was: "the package tells us what the player pressed, we handle it" via the bus. Replaced because the callback puts the grant path *on the data class the dev already fills* — one place to look, no forgotten subscription |
| 18 | ~~Panels ship `public void SetInfo()` / `public void Close()` aliases over `Show()`/`Hide()`, parameterless~~ (2026-08-20; **superseded by #25** on 2026-08-20) | Was: match the consumer's Inspector-dropdown habit. The aliases duplicated `OpenPanel`/`ClosePanel` for no behavior — `SetInfo` is now the company-standard *init* verb instead |
| 19 | Online Reward resolved rules (2026-08-20): session ends on app kill only (backgrounding pauses accrual, baseline reset on resume); x2/x5 buffs stack to ×7 (legacy rule); X5 costs 2 ads (`x5AdsRequired` serialized) and the partial ad counter is the only thing besides `Version` persisted in `NabaReward.Online`; OPEN ALL is ad-gated (`OnlineReward_OpenAll`) and claims every unclaimed slot; a fully-claimed grid cycles (claimed cleared, elapsed zeroed) | Matches the legacy Playtime behavior players already know, keeps the mockup's three-button layout honest (no ad counter on X2), and a cycling grid keeps the panel alive within one session |
| 20 | **Daily Open All is gated behind ads or IAP; IAP enters the package as the `PurchaseIap` hook** (2026-08-20, display rule tightened 0.8.1, mode switch made explicit post-0.8.1): two buttons stacked at the mockup's single-button position, **at most one visible, picked by the `openAllUseAds` Inspector bool (on = ads, off = IAP), never both** — the inactive mode's flow is also refused; supersedes the 0.8.1 "IAP config wins over ads" data rule — ads (N rewarded ads via `AdFlow`, progress `X/N` persisted as `OpenAllAdsWatched`, placement `DailyReward_OpenAll`) and IAP; the dev supplies only `openAllUseAds` / `openAllAdsRequired` / `openAllIapProductId` / `openAllIapPriceText` (display string, not store-fetched); Open All claims every remaining day, and day-7 completion is `StreakDay == 7` with reset on the next UTC day | The package triggers monetization itself so integration stays "fill the fields + wire 1 hook line"; an explicit bool beats the implicit IAP-wins inference (the consumer asked to flip modes without clearing the product id); a display-string price matches the consumer's actual habit; one visible button keeps the mockup layout honest |
| 21 | **The `{Feature}Manager` MonoBehaviours leave the package; the panel owns all feature logic** (2026-08-20, supersedes the manager half of #12): save, timers, ads, IAP, claim/spin state live on `{Feature}Panel`; the host writes its own ~15-line `Sample`-style manager holding the rows | One prefab to drag per feature instead of two objects to place and wire; reading the tiny host manager tells the dev exactly what their job is; a plain-C# state class was rejected as +30–50 forwarding lines per feature for zero behavior |
| 22 | **`Row.OnClaimed` replaces grant-carrying events** (2026-08-20, supersedes #17): `DailyRewardClaimedEvent`, `SpinResultEvent`, `OnlineRewardClaimedEvent` are deleted; the panel logs every grant and invokes the row's callback; a null callback `LogError`s "was NOT granted" | Everything the dev fills — including the grant reaction — lives in the one row class; no bus subscription to forget, and the audit log still betrays a missing handler |
| 23 | **`RewardHooks` is a static class with safe defaults, assigned once at boot** (2026-08-20, supersedes #5): `PlaySfx` no-ops, `ShowRewardedAd`/`PurchaseIap` LogError then proceed as rewarded/succeeded; a `SubsystemRegistration` reset restores defaults per play session | `SetInfo(rows)` takes only data; defaults keep a freshly dragged prefab working loudly instead of NRE-ing at 17 call sites; the reset is mandatory for domain-reload-off |
| 24 | **Leniency ladder** (2026-08-20): optional row data (`Key`/`Icon`/`Amount`) → one aggregated `LogWarning` naming index+field via `{Feature}Row.Warn`; structural breakage (empty list, <2 wedges, non-increasing unlocks) → throw; null `OnClaimed` → `LogError` at claim; unset ad/IAP hooks → `LogError` then proceed. List position replaces the old `Day`/`Wedge`/`Slot` index fields | A half-filled prefab must run and complain, never brick — the dev finishes filling data at their own pace; deleting the index fields deleted the most common validation error entirely |
| 25 | **Lifecycle naming** (2026-08-20, supersedes #18): `SetInfo(rows)` is the single init (`StartClass` retired — it was a repo-local convention; `SetInfo` is the company standard); `OpenPanel()`/`ClosePanel()` are the dev-facing activation APIs; the parameterless `SetInfo()`/`Close()` aliases are deleted | One verb per job ends the SetInfo/OpenPanel confusion; `OpenPanel`/`ClosePanel` keep their names because `BaseUIInspectorProcessor` string-matches them for the Odin inspector buttons |
| 26 | **Serialized UI references are optional by design** (2026-08-20): every dereference is guarded, templates LogError-and-skip, indexers are bounds-checked, `AdFlow.Busy` auto-releases after ~15s if the host SDK swallows both callbacks | A dev disabling or deleting a button mid-development is a designed workflow (the consumer has zero null-checked buttons); nothing may throw or deadlock over missing cosmetics |
| 27 | **Sample contract** (2026-08-20): one scene (`RewardSample.unity`), every sample type prefixed `Sample*`, art strictly from the ASMR_Tower set, `Assets/_RewardDemo` is a symlink view of `Samples~/RewardDemo` (single source, no mirror step) | The sample must drop into `_ASMR_Tower` and run after a trivial setup; the `Sample` prefix marks host-side responsibility at a glance and cannot collide with old package type names on upgrade |
| 28 | **Fixed-count boards are inspector-authored** (2026-08-20, 0.9.0): Daily's 7 cards and Spin's 8 wedges are pre-authored nested prefab instances wired into a serialized `List<>` on the panel, backgrounds authored per-instance; `SetInfo` *binds* rows onto the list (count mismatch warns + binds min + hides surplus; empty list LogErrors and skips; null entries skip silently; re-entry rebinds). The template+`cardSpacing`/`wedgeRadius` runtime-build path is deleted; `wheelSegmentCount` retired (the authored count *is* the segment count). Online cells and the ItemReceived pool stay template-instantiated (genuinely dynamic counts). Panels get no `OnValidate` auto-collect: `BaseUI.OnValidate` is private and auto-wires `uiPanel` — a derived `OnValidate` would silently shadow it | Runtime cloning + code-computed positions made layout a C# concern — rearranging 7 cards into a 3+3+big-day-7 grid meant editing the panel. Authored instances make layout and skins pure prefab work, and the bind loop fixes the old `built`-latch re-entry stranding |
