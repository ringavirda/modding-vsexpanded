# exlib as a generic modding framework - assessment and ranked recommendations

**Status** proposed 2026-09-05. An assessment with ranked recommendations; nothing here is ruled until
the owner answers R1-R6 in section 7. It extends
[2026-08-13-framework-hardening.md](2026-08-13-framework-hardening.md): every open F-item there was
re-verified against the source on 2026-09-05 and is folded into the ranking with its original number.
It does not change the roadmap's order; section 5 says where the work slots in.

**The question.** exlib is to be a generic modding framework other Vintage Story modders adopt, not only
the family's shared library. How capable is it for that audience today, what does it bring, what should
it add, remove or change, and in which order?

**Companion.** The testing side has its own assessment: [2026-09-05-exlib-testing-assessment.md](2026-09-05-exlib-testing-assessment.md).

**Method.** Nine read-only passes, one per area: form and process, the authoring API, networks, mod
infrastructure, the catalogues, the testing harness, the wiki read as a stranger, the eight reference
mods under `.compat/` (five C#, three JSON-only) as the audience, and iiex/siex as consumers. Every
load-bearing claim was then re-checked against the source. Citations are by symbol; counts are from
2026-09-05 and will drift.

## 1. Verdict

exlib is a strong family library and a credible substrate. Several of its parts have no equivalent in
the reference mods - the block-network graph, the filler-backed megablock, the versioned config store,
save migrations and healing, the code-first builders with parity tests, and the headless test harness -
and the reference mods show demand for exactly those parts: ElectricalProgressive hand-rolled its own
network graph (`ElectricalProgressive-Core/Utils/Network.cs`), four of five C# mods reinvented config
loading and only one versions it, none has migrations, none has a test harness.

It is not yet adoptable by a stranger, and the reasons are not missing features:

1. **No boundary.** 226 public types, zero `[Obsolete]`, zero `[EditorBrowsable]`, no page saying what
   is contract. 98 of the 226 are never referenced by iiex or siex, and about 50 are referenced nowhere
   outside exlib itself. The CHANGELOG promises semantic versioning with nothing to enforce it. A
   stranger cannot tell contract from incidental, and we cannot change anything without breaking someone.
2. **The family's content lives inside the framework and looks like the framework.** `CellRole` is a
   closed enum of blast-furnace vocabulary; `BEBehaviorMoltenCell` sits in `Blocks/Structures`;
   `HeatBalance` models a shaft furnace and a Bessemer converter; `ExlibConfig.MetalRecoveryFallback`
   defaults to `iiex:slag-block`; `ExSounds`, `ExParticles`, `ExBlockNames`, `ExMoldGate`,
   `ExMoldDrops` are family catalogues filed under `Helpers/`; `ExLiquids` seeds air, steam, exhaust and
   water; every worked example in the wiki is iron or steel. Roughly half of what a stranger downloads
   is ours.
3. **Silent failure at the consumer boundary.** The documented C# contribution route is erased at
   `AssetsFinalize` for five of six catalogues (only `MaterialRoleRegistry` has `RegisterContributor`);
   no catalogue loader logs a count; JSON binding is lenient; `ExBlockDef.Raw` writes keys the game
   never reads, without a warning; `MultiblockLayoutBuilder.Origin` is unchecked; a missing filler block
   no-ops; eighteen validators that name these exact failures run only in our xUnit suite.
4. **Onboarding stops before a visible result.** Getting-Started ends at a registered class with no
   blocktype, texture or lang entry; no sample exists; the headline feature (code-first definitions) and
   the load phases have no page; Multiblock-Structures.md and `IFillerHost`'s own comment describe an
   attribute generator that does not exist; `docs/moddb.html` names two retired mods; the wiki promises
   release zips that no CI workflow builds (the Cake build makes them locally, on demand); neither
   library is on NuGet.

And one gap of kind rather than degree: **the reference audience writes JSON and gates on other mods.**
Three of the eight reference mods contain no C# at all; the C# ones register most content through JSON
blocktypes; all eight gate compatibility with `dependsOn` or `IsModEnabled`; two integrate ConfigLib
for config UI; four built server-to-client config sync themselves; five carry Harmony patches (13 to 84
each). exlib's on-ramp is C# attributes and code-first builders, its config never reaches the client
(the only network channels in exlib are the highlight request and the machine-station inventory), and it
offers no compat, Harmony or ConfigLib helper.

Most of the fixes are small and half-specified in the hardening plan already. The expensive part is the
decision in R1-R3 about what exlib is for.

## 2. What exlib brings

| Capability | What a stranger gets | Demand in the reference mods |
|---|---|---|
| Block-network graph (`BlockNetwork`, `BlockNetworkModSystem`, `INetworkMember`) | a content-neutral connected-graph manager with merge, fracture, chunk-unload suspension, a two-tier error policy and highlight diagnostics; three worked networks (pipe, molten, MP energy) | ElectricalProgressive wrote its own |
| Megablocks (`StructureFillers`, `BlockFilledMegastructure`, `BlockEntityMultiblockStructure`) | per-cell collision through one shared filler block, completion monitoring, build projection, behaviours hosted per cell | industrialstory declares multiblocks in JSON with vanilla's weaker behaviour |
| Production machines (`BlockEntityProductionMachine`, `BEBehaviorProductionMachine`, `IProductionReadiness`) | a tick lifecycle with away catch-up and a readiness model; two overrides for a plain machine | every machine mod writes its own tick |
| Config (`ExConfigRegister<T>`, `ExConfigGenerator`) | versioned, range-checked, migratable, live-editable, source-generated accessors | 4 of 5 C# mods reinvented loading; 1 versions it; 2 depend on ConfigLib |
| Registration (`EntityRegistry`, `CommandRegistry`, `PreferenceRegistry`) | attribute-driven, assembly-scoped, one call per family | ElectricalProgressive makes 104 manual register calls |
| Migrations and healing (`BlockMigrationModSystem`, `BlockEntityHealModSystem`) | rename or remove codes in old saves; recreate lost block entities | none has either |
| Code-first definitions (`ExBlockDef`, `ExItemDef`, `MultiblockLayoutBuilder`) | typed builders injected as synthetic assets, with parity and golden tests | none; the community is JSON-first |
| Process registries (`ProcessRouteRegistry`, `ProcessJobRegistry`, `SpecSchema`) | a versioned, contributed-to catalogue any machine mod can be built against, JSON-first | Improved Metallurgy and industrialstory extend other mods' processes by patch |
| Testing (`ExpandedLib.Testing`: `TestWorld`, `StructureRig`, eighteen validators) | headless tests against the real game assemblies; definition parity; lang, code and wiki coverage guards | none |
| Legacy shims (`Legacy/`) | one source tree for 1.20, 1.21 and 1.22 | every mod supporting older versions forks |

## 3. What blocks adoption, ranked

**F-A The boundary** (F4.3, open). Counts in section 8. Nobody can build on a surface that may change
under them; we cannot prune without a deprecation rule.

**F-B Family content in the framework** (F7.3, open, and wider than the plan states). Beyond the plan's
five helpers: `CellRole`, `BEBehaviorMoltenCell` and `MoltenCellHost`, `HeatBalance` and
`HeatBalanceHud`, the vanilla tool taxonomy in `MetalToolEmitter` and `MetalFamilyEmitter`,
`ExlibConfig.MetalRecoveryFallback`, the seeded liquids, `MaterialRoleDef.Roles.IronOre`.
`PipeNetwork`, `MoltenNetwork`, `MpEnergyNetwork` and `MetalRegistry` are reusable by any industry mod
and stay, but marked as such.

**F-C Silent failures** (F5.2, F5.3, F5.4, F7.1, F8.1, F8.4, F8.7; all open). Per-item evidence in
section 8.

**F-D Onboarding and publication** (F3 partial; F4.1, F4.2, F4.4 open). Also: Getting-Started's
dependency example is one release behind; its csproj path and Testing-Harness.md's predate the per-mod
layout; Home.md's mod links point at the ModDB front page.

**F-E The idiom gap.** `_im`, `em` and `industrialstory` are JSON-only; `em` delegates all live config
to ConfigLib through `config/configlib-patches.json`; HydrateOrDiedrate mirrors its config into
`World.Config` for the client (`ConfigManager`) and SmithingPlus does the same (`ConfigLoader`);
interestingme's config is a ProtoBuf contract with a version field; Toolsmith has eight `IsModEnabled`
branches; interestingme probes the mod loader by reflection because it did not trust the method name
to exist across versions. exlib has none of these helpers, and each side loads config from its own file.

**F-F Our own mods bypass our own facilities.** 46 block entities in iiex and siex hand-write the
tree-attribute pair; one uses `ExBlockState` (F8.5). The Harmony bootstrap is copied verbatim into
exlib, iiex and siex. If we do not adopt a facility, a stranger will not.

**F-G Naming and shape** (F7.2, open). Five names for a keyed catalogue; three meanings of `Load` in
`ProcessRouteLoader` alone; `ExKeyedRegistry` adopted by two catalogues of six; three near-identical
loaders. Registration is explicit and assembly-scoped for entities, commands and preferences, but
migrations auto-discover across every loaded assembly (`BlockMigrationModSystem` walks
`AppDomain.CurrentDomain.GetAssemblies()`): two mental models in one library.

**F-H The testing kit is repo-bound at the edges.** `RepoPaths.DomainToMod` throws for an unknown
domain; `ReleasedCodes`, `ReleasedVersions` and `ReleasedCodeDebt` are ours; `BlockCodeEmitter` lives in
the test assembly behind an environment variable (F8.3); there is no template project; `IPlayer` cannot
be mocked on the 1.22 lane.

## 4. Recommendations

Ranked by payoff over effort. S is under a day, M is days, L is a week or more. "Serves" counts the
reference mods that built the thing themselves.

### 4.1 Before the first public release - the one-way doors

Each of these changes a public contract. Landing it after strangers have built on exlib costs them a
migration.

1. **Publish the boundary** (F4.3, F7.3, pruning). Partition the namespaces: `ExpandedLib.*` is
   contract; the family half moves under one namespace marked as ours (working name
   `ExpandedLib.Industry`; R2 decides where it lives); everything else becomes `internal` or carries
   `[EditorBrowsable(Never)]`, starting from the roughly 50 types referenced nowhere outside exlib. One
   wiki page, "Supported API", names the namespaces and the rule: one release of `[Obsolete]` before any
   public member goes. Effort M. The precondition for NuGet and for semantic versioning meaning anything.
2. **Fail loudly at the consumer boundary** (F5.2, F5.3, F5.4, F8.1, F8.4, F8.7). One summary line
   per catalogue at `AssetsFinalize` (files read, entries, errors); the asset location in every warning
   (`MaterialRoleLoader.Overlay` and `MetalCatalogueLoader.Populate` are the two without it); unknown
   JSON members rejected by name; `Raw` renamed `RootKey` and warned on keys outside the vsapi schema;
   `MultiblockLayoutBuilder.Origin` validated against the anchor; a notification when `ExDefinitions`
   replaces a def registered by a different assembly; a log line when `StructureFillers` finds no filler
   block. Effort M in total, each piece S. Serves everyone, JSON-only modders most.
3. **A contributor hook on every catalogue** (F7.1). Copy `MaterialRoleRegistry.RegisterContributor`
   and `InvokeContributors` to `MetalRegistry`, `ExLiquids`, `ProcessRouteRegistry`,
   `ProcessJobRegistry` and `BayOccupancyRegistry`; each loader invokes them after its clear. Effort M.
   Without it the wiki's C# route silently does nothing for five catalogues.
4. **Finish the builders where the fix is breaking** (F8.2, F8.6). `ExItemDef` to parity with
   `ExBlockDef` (30 public methods against 84; `Behavior`, `Handbook`, `SkipVariants`,
   `AttributeByType` and `TextureByType` are missing outright); enum overloads for render pass, face
   cull mode and draw type. Effort M. The additions could follow the release; the `Raw` rename in item 2
   cannot.
5. **Docs to a visible first result** (F3, F4.1, F4.2, F4.4). Fix the `fillerOffsets` instruction in
   Multiblock-Structures.md and the `IFillerHost` comment; fix `docs/moddb.html`; sweep the stale paths
   and the dependency version; add `Code-First-Definitions.md` and `Lifecycle.md` (phase, what exlib has
   done by then, what you may call); extend Getting-Started until a block is placeable; ship
   `samples/HelloExpanded` with one block, one config value, one lang key and one passing test. Effort
   M. Serves everyone.

### 4.2 The adoption layer - what every reference mod built for itself

6. **`ExMods`**: `IsLoaded(modid)`, `Version(modid)`, `WhenLoaded(modid, action)`, and a
   `World.Config` flag per loaded exlib-family mod so JSON mods can gate patches with `condition`, the
   way Improved Metallurgy gates on Toolsmith's flag. Effort S. Serves 8 of 8.
7. **`ExHarmony`**: the guarded bootstrap (`Harmony.HasAnyPatches`, `PatchAll`, unpatch on dispose)
   that exlib, iiex and siex each copy, plus category patching gated on `ExMods.IsLoaded` as
   HydrateOrDiedrate and SmithingPlus do by hand. Effort S. Serves 5 of 5 C# mods and removes our own
   duplication.
8. **Config to the client**: push the server's values at player join and after `/exmod config`
   edits; a ConfigLib bridge that exposes `[ExConfigRegister]` sections in ConfigLib's UI when ConfigLib
   is present. Effort M. Serves 4 of 5 C# mods and both JSON mods that use ConfigLib. Check first which
   exlib values the client reads at all.
9. **Adopt `ExBlockState` ourselves** (F8.5). Lift it into `BlockEntityProductionMachine` and
   `BlockEntityMultiblockMachine`, then convert the 46 hand-written pairs, starting with the 14 on bare
   `BlockEntity`. Effort M. The facility exists to close a bug class that still recurs.
10. **Checks in the game** (F5.1). `ExlibChecks.All(api)` at `AssetsFinalize`, config-suppressible,
    plus `/exmod verify`, running the validators that need no test runner (`DefinitionCatalogue`,
    `MultiblockCodes`, `RecipeCodes`, `LangCoverage`, `NetworkNodeContract`, `PinnedNetworkNodes`,
    `CodePrefixCollision`). Effort M. Serves JSON-only modders, who never run xUnit.
11. **Open `CellRole`**: string-keyed roles, the nine ferrous names declared by iiex. Effort M. Without
    it a non-furnace multiblock cannot use `MultiblockCellRoles` without editing exlib.

### 4.3 Structural - decide, then schedule

12. **A JSON-first megablock and machine path.** `BlockFilledMegastructure` already reads
    `fillerOffsets` from block attributes, but exlib does not register it as a block class, so a JSON
    mod cannot name it. Register it together with a concrete, non-abstract multiblock block entity,
    document the attribute, and a code-free modder gets per-cell collision and completion monitoring
    from a blocktype file. The production tick and the networks stay C#. Effort M for the megablock, L
    for the rest. Serves the three JSON-only mods and the JSON-registered content in the C# ones. R3.
13. **One loader, one naming law** (F7.2). Collapse `ProcessRouteLoader`, `ProcessJobLoader` and
    `BayOccupancyLoader` into one contributed-catalogue loader; adopt `ExKeyedRegistry` in all six
    catalogues; apply the verbs from conventions.md. Effort L. Before external adoption or never; it is
    churn afterwards.
14. **The testing kit as a product.** A template test project in the dev bundle;
    `RepoPaths.DomainToMod` falling back to `domain == mod`; `BlockCodeEmitter` moved to the generators
    (F8.3); a release workflow in CI that builds `exlib_<v>.zip` and `exlib-testing_<v>.zip`. Effort M.
15. **Distribution.** NuGet packages for `exlib` and `ExpandedLib.Testing` once item 1 has landed; a
    `dotnet new` template once item 5 has. Effort S at that point.

### 4.4 Remove or relocate

- `ExlibConfig.MetalRecoveryFallback` defaulting to `iiex:slag-block`: the fallback moves to iiex
  config. S.
- `ExSounds`, `ExParticles`, `ExBlockNames`, `ExMoldGate`, `ExMoldDrops`, `HeatBalance`,
  `HeatBalanceHud`: to iiex (siex depends on iiex, so both keep them) or to the marked namespace. S to
  M; R2.
- `BEBehaviorMoltenCell` and `MoltenCellHost`: out of `Blocks/Structures`, beside `MoltenNetwork`. S.
- The roughly 50 public types referenced nowhere outside exlib: `internal` unless the boundary page
  lists them. S to M, one pass; R6.
- The legacy shim stays: it is the only way a third party gets 1.20 and 1.21 from one tree, and a
  1.22-only consumer pays nothing for it.
- `HandbookUnitPatch` stays: it is gated on the imperial preference and is a no-op otherwise.

## 5. Where this slots into the roadmap

The roadmap's order stands: walk, machining line, steam, release, siex. Items 1 to 5 are a pre-release
gate for Q5 (first release = exlib + iiex), because each is a one-way door: shipping exlib to strangers
without a boundary, or with `Raw` in the builder, is a compatibility promise we cannot keep. Items 6 to
11 are independent of content and fit the gaps around the walk; each is an implementation task against
a settled spec. Items 12 to 15 wait for R1-R3.

## 6. What not to do

- No second mod. M1 closed the mod set; the family half stays inside exlib's zip under one namespace,
  or moves to iiex.
- No rewrite of the catalogue layer before R3 is answered; item 13 is churn if exlib stays a C#
  framework for a small audience.
- No JSON-first claim on the front page until item 12 exists.
- No NuGet before item 1; publishing 226 unbounded types is the boundary problem with a wider blast
  radius.

## 7. Rulings requested

Each has a default so a single "go" is enough.

- **R1 Boundary shape.** Namespace partition inside one dll with `[EditorBrowsable(Never)]`
  (recommended), or a second assembly. Default: namespaces.
- **R2 Where the family half lives.** A marked namespace inside exlib (recommended now: cheapest, and
  it keeps `MetalRegistry` and the three networks available to other industry mods), or iiex. Default:
  the namespace now, revisit at 1.0.
- **R3 Audience.** Commit to the JSON-first path (item 12, L), or say on the front page that exlib is
  a C# framework. Default: the megablock half of item 12 only, then reassess.
- **R4 Config sync and the ConfigLib bridge** (item 8). Build, or leave config per side. Default:
  build the sync; bridge only when a user asks.
- **R5 The pre-release gate.** Adopt items 1 to 5 as the Q5 gate. Default: yes.
- **R6 Pruning.** Convert the never-referenced public types to `internal` before the release,
  accepting one round of churn in iiex and siex. Default: yes.

## 8. Evidence

### Hardening-plan items re-verified 2026-09-05

| Item | State | Evidence |
|---|---|---|
| F3 wiki teaches a framework that no longer exists | partial | Multiblock-Structures.md and `IFillerHost` describe an attribute generator; `generators/` holds only `ExConfigGenerator` and `ExLangKeyGenerator` |
| F4.1 lifecycle page | open | no page; `ExecuteOrder` appears once in src (`ExDefinitionModSystem`, 0.04, server only) and nowhere in the wiki |
| F4.2 code-first definitions page | open | no page; `ExBlockDef` mentioned in passing on three pages |
| F4.3 API boundary | open | 0 `[Obsolete]`, 0 `[EditorBrowsable]`; `InternalsVisibleTo` names only the two test assemblies |
| F4.4 sample | open | no samples/, no template |
| F5.1 in-game checks | open | no `ExlibChecks`, no verify sub-command |
| F5.2 summary logs | open | no notification-level log in any of the six loaders |
| F5.3 asset location in errors | partial | route, job and bay loaders carry the source; `MaterialRoleLoader.Overlay` and `MetalCatalogueLoader.Populate` do not |
| F5.4 strict binding | open | `JsonObject` accessors and `ToObject<T>` throughout |
| F6.1 migration runtime guard | open | `BuildRemapTable`'s only source-side check is `GetBlock(oldCode) == null` |
| F6.2 mistyped network type | done | `BlockNetworkModSystem.TryCreateNetwork` logs and adds no node |
| F7.1 contributor hook | open | `RegisterContributor` exists on `MaterialRoleRegistry` only |
| F7.2 naming law | open | see F-G |
| F7.3 Helpers split | open | 19 files, one namespace |
| F8.1 Raw to RootKey | open | `Raw` and `RawByType` unchanged; no schema warning |
| F8.2 ExItemDef | open | 30 public methods against 84 |
| F8.3 BlockCodeEmitter | open | still in the test assembly, `EXLIB_WRITE_BLOCKCODES` |
| F8.4 Origin validation | open | `MultiblockLayoutBuilder` validates roles only |
| F8.5 ExBlockState in the bases | open | 46 hand-written pairs, 1 user |
| F8.6 enum overloads | open | strings only for render pass, face cull mode, draw type |
| F8.7 duplicate code | by design | `ExDefinitions` replaces on re-register; an accidental cross-mod collision is indistinguishable from a deliberate override |

### Surface counts

| Measure | Count |
|---|---|
| public types in mods/exlib/src | 226 |
| of which unreferenced by iiex and siex | 98 |
| of which unreferenced by exlib's tests and harness too | about 50 |
| `[Obsolete]`, `[EditorBrowsable]` | 0, 0 |
| block entities in iiex and siex overriding `ToTreeAttributes` | 46 |
| of which using `ExBlockState` | 1 |
| validator classes in ExpandedLib.Testing | 18 |
| catalogues with a C# contributor hook | 1 of 6 |
| network channels registered by exlib | 2 (highlight, station inventory) |

### What the reference mods build for themselves

| Need | Built by | exlib today |
|---|---|---|
| mod-presence and compat gating | all 8 (`IsModEnabled` branches, `HarmonyPatchCategory` gating, `dependsOn` and `condition` in every compat patch) | nothing |
| config with live edit and client sync | HydrateOrDiedrate, SmithingPlus, interestingme, ElectricalProgressive; `em` through ConfigLib | store yes; sync no; ConfigLib no |
| Harmony lifecycle | all 5 C# mods, 13 to 84 patches each | the bootstrap copied into three ModSystems |
| network channels | 4 of 5 C# mods | vanilla API used directly, no helper |
| GUI dialogs for machines | ElectricalProgressive (12), HydrateOrDiedrate (3) | none in exlib; two in iiex |
| block entity persistence | all C# mods | `ExBlockState`, unused by us |
| registration | ElectricalProgressive (104 calls), interestingme (37) | attribute-driven |
| config versioning | interestingme only | versioned, with migrations |
| handbook and lang tooling | `_im` and industrialstory hand-write handbook pages | validators, test-only |
| JSON-only content | `_im`, `em`, industrialstory (652 files, no C#) | C# on-ramp only; `fillerOffsets` is read from JSON but the class is unregistered |
