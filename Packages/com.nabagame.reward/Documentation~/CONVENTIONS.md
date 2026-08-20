# Package Conventions

Rules for code and content inside `com.nabagame.reward`. ARCHITECTURE.md says *what* the package is; this file says *how* work inside it must be done.

## Folder contract

```
Packages/com.nabagame.reward/
├── package.json
├── README.md                     front door: features, requirements, install, quick start
├── CHANGELOG.md                  Keep a Changelog + SemVer; every released change gets a line
├── Runtime/
│   ├── NabaGame.Reward.asmdef    refs: com.bmh.core.runtime, com.nabagame.ui.runtime, UniTask, Unity.TextMeshPro
│   ├── Core/                     shared primitives: RewardHooks (static), TimeScheduler, AdFlow,
│   │                             RewardProfileStore, RewardUi guards, amount formatting
│   └── Features/
│       ├── DailyReward/          row class (the dev's one data file), profile, panel + widget, events
│       ├── OnlineReward/
│       └── LuckySpin/
├── Editor/                       only if editor tooling ever needs it (none exists today);
│                                 NabaGame.Reward.Editor.asmdef when created
├── Samples~/                     importable via Package Manager; ALSO the demo host via the
│   └── RewardDemo/               Assets/_RewardDemo symlinks — single source, no mirror step.
│                                 One scene, Sample*-prefixed scripts, ASMR_Tower art only
└── Documentation~/               never imported by Unity
    ├── ARCHITECTURE.md, CONVENTIONS.md, CONSUMER-STYLE.md, INTEGRATION-GUIDE.md, ROADMAP.md
    ├── FEATURES/<feature>.md     one spec per feature
    └── RefUI/                    authoritative UI mockups
```

- Feature folders are siblings and never reference each other. `Core/` never references a feature.
- Prefabs, sprites, and audio clips a feature *owns* live under its feature folder. Sample/demo-only content lives in `Samples~` — never in `Runtime/`.
- Editor-only code goes in `Editor/`, never behind `#if UNITY_EDITOR` inside Runtime files, except where Unity requires it (e.g. `OnValidate` bodies).
- **Everything in `Samples~/RewardDemo/Scripts` is prefixed `Sample`** (`SampleRewardBoot`, `SampleDailyRewardManager`, `SampleItemReceivedPanel`, …): the prefix marks "this is the host's job, copy and adapt it", and it can never collide with a package type during an upgrade.

## Hard boundaries

- **No game types.** Nothing in the package may reference `Assembly-CSharp` (enforced by asmdef). No `RewardType`, `RewardID`, `GameController`, `UIManager`, `AudioManager`.
- **No new dependencies** beyond the list in README.md without a decision recorded in ARCHITECTURE.md §9 and a CHANGELOG entry.
- **No save plugin, no ads SDK.** PlayerPrefs (§6 of ARCHITECTURE.md) and the ads hook (§8) only.
- **No `Update()` polling** — `TimeScheduler` or UniTask loops with explicit lifetime (countdown loops gate on `IsVisible()`).
- **No runtime-constructed UI** — prefab-authored: fixed-count boards are pre-authored instances wired into a serialized `List<>` (decision #28), genuinely dynamic lists instantiate a disabled authored template.

## Naming and style

- Namespace: `NabaGame.Reward` (Runtime), `NabaGame.Reward.Sample` (sample), `NabaGame.Reward.Editor` (Editor). Never `PainAndSeek` inside the package.
- PascalCase types/methods/properties, camelCase locals and private serialized fields; one public type per file. Exception: the host manager's dev-filled **`[TableList] public List<{Feature}Row> rows`** is a public camelCase field — it is the host-facing data contract (ARCHITECTURE §2); the sample managers model it.
- Public type names must stay distinctive (feature-prefixed): consumer code is global-namespace and already defines `RewardType`, `GameMode`, `OfferType`, `UIManager`, `RewardCheckPointData` (CONSUMER-STYLE.md).
- Public API stays `void` + callbacks + events — no `async UniTask<T>` signatures (UniTask is internal-only; the consumer team does not use it).
- The package ships no `[CreateAssetMenu]` data types — data authoring is the host's business.
- **Initialization is `SetInfo(...)` only** — the company-standard init verb (`StartClass` is retired, decision #25). No feature logic in Unity `Start()`; `Awake` only for self-contained setup; `SetInfo` re-entry must not duplicate listeners (`RemoveListener` before `AddListener`; `RewardUi.Bind` does both for buttons) and must rebind cleanly — on a fixed board a later `SetInfo` with more or fewer rows shows/hides the right authored entries.
- **Panel activation is `OpenPanel()` / `ClosePanel()`** — never rename them: the demo host's `BaseUIInspectorProcessor` matches those exact strings for the Odin inspector buttons. No parameterless `SetInfo()`/`Close()` aliases.
- **Panel regions are a fixed vocabulary, in this order: `API`, `Logic`, `UI`, `Debug`.** `API` comes first and must be self-sufficient — a consuming dev reads only that region (init, open/close, red-dot queries, reset, placement consts). Same four names in every panel; widgets and plain data classes get no regions.
- **Every serialized UI reference is optional** (decision #26): guard every dereference (`if (button)`, `if (label)`), `LogError`-and-skip on a missing template or an empty authored board list, silently skip null authored-list entries, bounds-check every cell/wedge index, keep `HandleClicked`-style callbacks null-safe. A disabled or deleted button silently disables its feature — it never throws and never deadlocks a flow.
- Comments explain hidden constraints only. Identifiers and comments in English. **One deliberate exception:** the `// ...` Vietnamese comments (under 7 words) on every serialized field of the three feature panels are dev-facing field guides for the consuming team — never translate, rewrite, or delete them.
- Do not extract a method whose body is 3 lines or fewer; inline it (exceptions: Unity messages, `[Button]` debug methods, method-group handlers, and a guard helper used across many call sites like `RewardUi.Bind`).
- Odin attributes are allowed and encouraged (`[TableList]`, `[ShowInInspector, ReadOnly]` for the panel's runtime row view, `[Button, DisableInEditorMode]` for debug).

## Fail loudly — with the leniency ladder

Inherited from the repo's AGENTS.md, adjusted for a package whose consumers fill data incrementally (decision #24):

- **Warn, don't throw, on incomplete row data**: missing `Key`/`Icon`, `Amount <= 0`, invalid `Weight` produce one aggregated `Debug.LogWarning` naming every index and field (`{Feature}Row.Warn(rows, context)`); the feature keeps running. The host manager's `OnValidate` calls the same helper so gaps surface before Play.
- **Throw only on structural breakage** the machine cannot run with: null/empty rows list, fewer than 2 spin wedges, non-increasing `UnlockAfterSeconds` — `InvalidOperationException` naming the index and value.
- **Every grant is `Debug.Log`ged** (key + amount) before `OnClaimed` is invoked; a null `OnClaimed` additionally `Debug.LogError`s "'key' xN was NOT granted" naming the row index. The host handler must `LogError` on an unknown `Key` — never a silent default.
- **Unset hooks never throw**: `RewardHooks` defaults `LogError` naming the hook, then proceed as rewarded/succeeded so a fresh prefab still runs.
- Version-mismatched save payloads log an error naming the key and reset — never silently reinterpret. Persisted indices are range-checked against the current row list before use.
- Every event raise allocates a fresh payload instance; never cache and mutate one.

## Versioning

- SemVer. Breaking the hooks contract, row shapes, panel API, save payload, or **removing any public package type** = **major** (in 0.x: the minor bump is the breaking bump). New feature or additive API = **minor**. Fix = **patch**.
- Every release updates `package.json` version + CHANGELOG.md on the same commit.
- Save payload changes also bump the profile's `Version` field and ship a migration (or an explicit, logged reset).

## Integration checklist — definition of done per feature

A feature is done only when all of these hold:

- [ ] Compiles inside the package asmdef with only the documented dependencies (no `Assembly-CSharp` reference crept in).
- [ ] Works end-to-end in the demo host (`Assets/_RewardDemo`) via the public contract only — rows in through `SetInfo`, the grant landing in the host's `OnClaimed` handler, never inside the package.
- [ ] `Samples~` contains the `Sample*` manager, adapters, and filled row list needed to run it in a fresh host — one scene, no extra setup.
- [ ] INTEGRATION-GUIDE.md steps reproduce it from zero, verified by actually following them.
- [ ] Feature spec in `FEATURES/` matches the shipped behavior; RefUI mockup matched (or deviations recorded in the spec).
- [ ] Verification script in the feature spec passes: claim flows, cooldowns, kill-app/reopen, editor ad path, repeated open/close without listener duplication, button spam, **and the null-button pass** (disable/delete each serialized reference, including an authored card/wedge instance — nothing throws or sticks).
- [ ] CHANGELOG entry written.
