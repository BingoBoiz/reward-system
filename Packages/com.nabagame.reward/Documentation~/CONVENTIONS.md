# Package Conventions

Rules for code and content inside `com.nabagame.reward`. ARCHITECTURE.md says *what* the package is; this file says *how* work inside it must be done.

## Folder contract

```
Packages/com.nabagame.reward/
├── package.json
├── README.md                     front door: features, requirements, install, quick start
├── CHANGELOG.md                  Keep a Changelog + SemVer; every released change gets a line
├── Runtime/
│   ├── NabaGame.Reward.asmdef    refs: com.bmh.core.runtime, com.nabagame.ui.runtime, UniTask
│   ├── Core/                     shared primitives: RewardItem + RewardItemData catalog, IRewardGranter, RewardHooks,
│   │                             TimeScheduler, AdFlow, PlayerPrefs profile base, package events
│   └── Features/
│       ├── DailyReward/          manager, profile, config SO, panel + widget scripts, prefabs
│       ├── OnlineReward/
│       └── LuckySpin/
├── Editor/
│   └── NabaGame.Reward.Editor.asmdef   (refs Runtime asmdef; editor tooling only)
├── Samples~/                     importable via Package Manager, lands in the host's Assets/
│   └── Integration/              demo scene, sample adapters (granter, audio, com.bmh.ads),
│                                 sample RewardItemData + feature tables (importer-shaped)
└── Documentation~/               never imported by Unity
    ├── ARCHITECTURE.md, CONVENTIONS.md, INTEGRATION-GUIDE.md, ROADMAP.md
    ├── FEATURES/<feature>.md     one spec per feature
    └── RefUI/                    authoritative UI mockups
```

- Feature folders are siblings and never reference each other. `Core/` never references a feature.
- Prefabs, sprites, and audio clips a feature *owns* live under its feature folder. Sample/demo-only content lives in `Samples~` — never in `Runtime/`.
- Editor-only code (config inspectors, bake buttons, validators) goes in `Editor/`, never behind `#if UNITY_EDITOR` inside Runtime files, except where Unity requires it (e.g. `OnValidate` bodies).

## Hard boundaries

- **No game types.** Nothing in the package may reference `Assembly-CSharp` (enforced by asmdef). No `RewardType`, `RewardID`, `GameController`, `UIManager`, `AudioManager`.
- **No new dependencies** beyond the list in README.md without a decision recorded in ARCHITECTURE.md §9 and a CHANGELOG entry.
- **No save plugin, no ads SDK.** PlayerPrefs (§6 of ARCHITECTURE.md) and the ads hook (§8) only.
- **No `Update()` polling** in managers — `TimeScheduler` or UniTask loops with explicit lifetime.
- **No runtime-constructed UI** — prefab-authored, disabled templates for lists.

## Naming and style

- Namespace: `NabaGame.Reward` (Runtime), `NabaGame.Reward.Editor` (Editor). Never `PainAndSeek` inside the package.
- PascalCase types/methods/properties, camelCase locals and private serialized fields; one public type per file. Exception: importer-shaped tables (`{Row}Data`) expose a **public camelCase `List<{Row}> {row}s`** because the Googlesheet Importer finds it by that exact name (ARCHITECTURE §2).
- Initialization is `StartClass(...)` only — no feature logic in Unity `Start()`; `Awake` only for self-contained setup; `StartClass` re-entry must not duplicate listeners.
- Comments explain hidden constraints only. Identifiers and comments in English.
- Do not extract a method whose body is 3 lines or fewer; inline it (exceptions: Unity messages, `[Button]` debug methods, method-group handlers).
- Odin attributes are allowed and encouraged for config SOs (`[TableList]`, validation attributes); Odin is a guaranteed dependency.

## Fail loudly

Inherited from the repo's AGENTS.md, restated for the package boundary:

- Unknown `RewardItem.Key`, malformed config rows, and version-mismatched save payloads must surface in the Console **naming the offending key/asset** — throw where the flow cannot continue, `Debug.LogError` where it recoverably can. Never map bad data to a "reasonable" default.
- A missing required hook throws `InvalidOperationException` from `StartClass`, naming the hook.
- Config SOs validate in the editor (Odin attributes / `OnValidate` / an Editor validator) so bad tables are caught before Play.

## Versioning

- SemVer. Breaking the hooks contract, `RewardItem`/row shapes, save payload, or any public manager API = **major**. New feature or additive API = **minor**. Fix = **patch**.
- Every release updates `package.json` version + CHANGELOG.md on the same commit.
- Save payload changes also bump the profile's `Version` field and ship a migration (or an explicit, logged reset).

## Integration checklist — definition of done per feature

A feature is done only when all of these hold:

- [ ] Compiles inside the package asmdef with only the documented dependencies (no `Assembly-CSharp` reference crept in).
- [ ] Works end-to-end in the demo host (`Assets/_GameBase`) via the public contract only (config + hooks + events).
- [ ] `Samples~` contains the adapters and sample data needed to run it in a fresh host.
- [ ] INTEGRATION-GUIDE.md steps reproduce it from zero, verified by actually following them.
- [ ] Feature spec in `FEATURES/` matches the shipped behavior; RefUI mockup matched (or deviations recorded in the spec).
- [ ] Verification script in the feature spec passes: claim flows, cooldowns, kill-app/reopen, editor ad path, repeated open/close without listener duplication, button spam.
- [ ] CHANGELOG entry written.
