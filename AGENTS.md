# AGENTS.md

Guidance for coding agents working in this repository.

## Project scope

`reward-system` is a Unity mobile project built with Unity **2022.3.62f3**. It was forked from the `Paint And Seek` game (namespace `PainAndSeek`) and then stripped to **reward-core only** (2026-08-18): `Assets/_GameBase/` now holds just the reward feature and the minimal hubs it needs (`GameManager`, `CommonManager`, `GameController`, `UIManager`, `AudioManager`, `RuntimeDataManager`, `RewardManager`, `RedDotManager`, `RewardGranter`, profiles/configs/events for rewards). No gameplay, SlapTower, shop, quest, level-pass or item-catalogue code remains; `RewardGranter` grants Cash/Gem/Trophy/NoAds and logs an error for any other `RewardType`.

**Purpose: build the reusable reward package `com.nabagame.reward`** (decided 2026-08-19, superseding the earlier `_00 *` folder plan). The package is embedded at `Packages/com.nabagame.reward/` and covers three features matching the mockups in its `Documentation~/RefUI/`: Daily Reward, Online Reward (session-scoped timed grid), Lucky Spin. `Assets/_GameBase/` is now the **demo host**: it supplies the adapters (`IRewardGranter`, hooks) and scene wiring a real game would, and still contains the legacy reward monolith used as behavioral reference until each feature is rebuilt in the package. The empty `Assets/_00 *` folders are obsolete and will be deleted.

Package docs are authoritative for package work — read before touching it:

- `Packages/com.nabagame.reward/Documentation~/ARCHITECTURE.md` — dependency rules (asmdef: package never references game code or game enums), `RewardItem` + `IRewardGranter` model, `RewardHooks` injection, events, PlayerPrefs save, decision record.
- `.../Documentation~/CONVENTIONS.md` — package folder contract, namespace `NabaGame.Reward`, definition of done.
- `.../Documentation~/ROADMAP.md` — phase plan and current status.
- `.../Documentation~/RefUI/` — authoritative UI mockups; match them when building feature UI.

The reference implementation with the full game lives in `D:\Fork\paint-and-seek` (Unity 6, read-only) — its `Library/PackageCache` holds the NabaGame package sources.

## Safety

- Git is strictly read-only. Never run a Git command that changes repository state.
- Game-owned files live under `Assets/_GameBase/`.
- Treat packages, plugins, SDKs, `Assets/_ThirdParty/`, `Assets/MaxSdk/`, and `Assets/BBPackages/` as read-only unless explicitly requested. **Exception:** `Packages/com.nabagame.reward/` is this repo's own first-party package and is fully editable.
- Do not edit generated `.sln`, `.csproj`, `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`, or `UnityConstants.cs`.
- Do not edit scenes, prefabs, ScriptableObjects, materials, or other serialized assets unless the task explicitly requires asset wiring.
- Preserve unrelated user changes.
- Verify UI changes in Unity Play mode when possible and report when verification was not run.

## Ownership

- `GameManager`: data holder only — raw ScriptableObjects, collections, configs, settings, profile, pooling, remote config, ads. It owns no feature managers.
- `GameController`: authoritative scene runtime state. Hosts the scene-local feature managers `RewardManager` and `RedDotManager` as children, started from `GameController.StartClass()` (which then calls `UIManager.StartClass()`) and accessed via `GameController.Instance.<camelCase field>`. These managers must never poll in `Update()` — they schedule `UniTask.Delay`/`TimeScheduler` timers and raise events.
- `UIManager`: UI composition root; owns serialized panel references, UI startup, popup coordination, and global UI state.

`GameController.Instance` always exists at runtime. Never guard it — no `if (GameController.Instance)`, no `if (GameController.Instance && GameController.Instance.petPlayer)`, no `?.` on it. Call it directly and let a missing reference throw. The same goes for its child managers; a scene that is missing one is a wiring bug to fix in the scene, not a case to branch on in code.

UI may read display data and call narrow public commands. It must not calculate scoring, validate paint actions, select roles, control physics, or own win/lose rules.

Do not refactor gameplay during a UI task. If integration is missing, prefer a small read-only property, command method, callback, or event. Report every gameplay file changed.

## Fail loudly

- Never swallow unknown or invalid data into a fallback. A `switch`/`_`/`default` arm over a game enum, ID, or type must `throw` (`ArgumentOutOfRangeException`/`InvalidOperationException` naming the value) — never map an unhandled case to some "reasonable" valid case. Same for `try/catch` that hides the cause, `?.`/null-guards on required references, and `TryGet` results ignored.
- If data (sheet, ScriptableObject, profile/save) or code is inconsistent, the Unity Console must show an error that names the offending type/id/asset. A wrong value rendered quietly (0/N, missing icon, wrong profile updated) is worse than an exception: it ships.
- When a mismatch is expected and recoverable by design, handle it explicitly and still `Debug.LogError` it — a silent default is never acceptable.
- Rationale: `MapAdUnlockFlow` mapped every non-Wing/Pet type to the Skin profile; a new Weapon item then read/wrote the wrong profile with zero console output.

## Initialization: `StartClass()` only

`StartClass()` is the manual initialization convention for every manager, panel, widget, and adapter. `SetInfo()` is obsolete.

```text
GameController.StartClass()
  -> RewardManager.StartClass()
  -> RedDotManager.StartClass()
  -> UIManager.StartClass()
       -> Panel.StartClass()
       -> Widget.StartClass()
```

- Parent classes call child `StartClass()` methods in an explicit order.
- Avoid Unity `Start()` for feature initialization and never depend on `Start()` ordering.
- Use `Awake()` only for unavoidable self-contained setup needed before external initialization.
- Use `OnEnable()`/`OnDisable()` only for behavior genuinely tied to active state.
- Package-required singleton `Init()` overrides may remain, but new project initialization belongs in `StartClass()`.
- `OnValidate()` is for editor reference wiring only, never runtime flow.
- `StartClass()` must not duplicate button listeners or event subscriptions when called again.
- When touching a UI class that still uses `SetInfo()`, migrate it and its direct callers to `StartClass()` when safe within the task.

## UI rules

UI scripts belong under `Assets/_GameBase/Scripts/UI/`; panels belong in `UI/Panel/`.

### Panels and widgets

- Panels/popups inherit `NabaGame.UI.BaseUI`.
- Use `Show()`/`Hide()` as visibility truth. `Open()`/`Close()` may wrap them for presentation work.
- `StartClass()` binds references/listeners and renders initial state.
- Panels own labels, icons, fills, selection visuals, animations, and button availability only.
- Button handlers may play feedback and call one public gameplay API; they must not implement the gameplay result.
- Track blocking popups in `UIManager` so navigation, ads, and input blocking share one query.

### Authoring

- Author UI hierarchy in scenes/prefabs and assign references with `[SerializeField]`.
- Never create normal UI at runtime with `new GameObject()` plus `AddComponent<Image/Text/...>()`.
- For dynamic lists, author a disabled template and instantiate it.
- For a reusable single widget, author it disabled and toggle it.
- `OnValidate()` may auto-find stable child references for authoring convenience.
- Avoid `GameObject.Find`, hierarchy-name lookup, and repeated `GetComponent` during UI updates.

### UI/gameplay boundary

```text
button/input -> panel -> GameController or feature manager
state change -> event/callback -> panel refresh
config/profile -> panel formatting -> visible UI
```

- Never write authoritative gameplay fields directly from UI.
- Never perform physics, targeting, paint validation, scoring, or game-state transitions inside UI.
- Do not poll gameplay every frame when an event or explicit refresh can update the panel.
- Prefer narrow APIs over exposing an entire gameplay component.
- Keep any unavoidable gameplay edit to the smallest integration bridge.

## Events and lifetime

- Use `NabaGame.Core.Runtime.EventManager.EventManager` for cross-system updates.
- Events are plain `GameEvent` payloads under `Assets/_GameBase/Scripts/GameEvent/`.
- The authoritative owner raises an event after changing state; UI only renders the result.
- Subscribe from `StartClass()` without allowing duplicate subscriptions.
- Unsubscribe when the UI listener can die before the publisher.
- Use direct calls between components owned by the same panel.
- Kill panel-owned tweens when closing/destroying the panel.
- Use unscaled time for UI intended to continue while gameplay is paused.
- Use `UniTask` for async flows and prevent repeated clicks while an action is running.

## Style

- New game code uses namespace **`PainAndSeek`**. Preserve this spelling.
- Do not use leftover namespaces such as `ArrowMaze`, `GangWar`, `StealBrainrot`, or `SuperStylist` for new code.
- Identifiers/comments are English; conversation with the user may be Vietnamese.
- Use PascalCase for types/methods/properties and camelCase for locals/private serialized fields.
- Keep one public type per matching file and avoid broad serialized-field renames.
- Comments explain hidden constraints, not obvious behavior.
- Do not extract a method whose body is 3 lines or fewer or holds one small logic — inline the call at each site. Exceptions: Unity messages, `[Button]` debug methods, and handlers passed as a method group (e.g. `onClick.AddListener`/`RemoveListener`).
- Cache references; do not allocate or search hierarchy in hot UI paths.
- Validate dependent serialized values with Odin attributes or `OnValidate()`.
- Keep changes scoped; do not clean unrelated legacy code.

## UI task workflow

1. Inspect `UIManager`, the target panel/prefab, and the narrow gameplay API supplying its data.
2. Confirm the task can remain UI-only.
3. Implement serialized references and `StartClass()` initialization.
4. Forward actions through public APIs and refresh through events/callbacks.
5. Register the panel in `UIManager.StartClass()` and popup tracking when relevant.
6. Wire serialized assets only when explicitly required, with minimal changes.
7. Verify initialization, repeated open/close, listener duplication, button spam, pause behavior, and mobile aspect ratios.

## Completion checklist

- [ ] Gameplay files are untouched unless a minimal integration bridge was required and reported.
- [ ] Initialization uses `StartClass()`, not `SetInfo()` or a new Unity `Start()`.
- [ ] UI displays state and forwards intent; it owns no gameplay rules.
- [ ] Reinitialization cannot duplicate listeners.
- [ ] UI is prefab/scene-authored; tweens, async actions, and subscriptions have safe lifetimes.
- [ ] No generated, third-party, package, unrelated file, or Git state was modified.
- [ ] Play-mode verification was completed or explicitly reported as not run.

## Key paths

- `Assets/_GameBase/Scripts/Manager/UIManager.cs`
- `Assets/_GameBase/Scripts/UI/Panel/`
- `Assets/_GameBase/Prefabs/UI/`
- `Assets/_GameBase/Scripts/GameEvent/`
- `Assets/_GameBase/Scripts/Manager/GameController.cs`
