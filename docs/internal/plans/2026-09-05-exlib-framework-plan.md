# exlib framework plan - boundary, loud failures, adoption layer, JSON path

> **For agentic workers:** execute task by task with a fresh implementer per task; the gate in each
> task is the check. No review pass per task; one reviewer only for tasks marked *contract*.

**Status** complete 2026-09-06 (every task landed, uncommitted; written 2026-09-05). Implements
[2026-09-05-exlib-framework-assessment.md](2026-09-05-exlib-framework-assessment.md) items 1 to 13
and the relocations in its section 4.4, under the default rulings R1 (namespace partition in one
dll), R2 (family half under `ExpandedLib.Industry`), R3 (the megablock half of the JSON path), R4
(config sync, no ConfigLib bridge), R5 (items 1 to 5 gate the first public release), R6 (prune before
release). Companion plans: [2026-09-05-exlib-convenience-plan.md](2026-09-05-exlib-convenience-plan.md)
and [2026-09-05-exlib-testing-plan.md](2026-09-05-exlib-testing-plan.md). Each task states its own
status in its checkbox list; a stage is done when its gate line is checked.

**Goal:** make exlib a framework a stranger can adopt: a published API boundary, failures that name
their cause, catalogues that keep C# contributions, builders that cover what JSON covers, a JSON
megablock, and the helpers every reference mod built for itself.

**Architecture:** one dll, three tiers of namespace: `ExpandedLib.*` is the supported contract,
`ExpandedLib.Industry.*` is the family's content layer (public, reusable, no stability promise),
and everything else is `internal` or `[EditorBrowsable(Never)]`. New helpers are static classes or
small ModSystems in `ExpandedLib` with one job each. Catalogue loaders share one contributor contract.

**Tech stack:** C# 14, .NET 10 primary (net10.0, VS 1.22) with legacy net8.0 (1.21) and net7.0
(1.20) through `-p:Legacy=true`; xUnit + NSubstitute through `ExpandedLib.Testing`; CSharpier.

**Spec:** the assessment above, plus `docs/design/mechanics/framework-composition.md` and
`process-extension.md` for the rules that must not move.

## Progress

- **Stage A landed 2026-09-05** (uncommitted): A1 moved 40 files into seven `ExpandedLib.Industry.*`
  namespaces with names and registered codes unchanged; A2 made `CellRole` a string-keyed record
  struct (`CellRole.Of(key, single)`, the single-cell flag in a registry the read-back path never
  touches, roles emitted in ordinal key order, four furnace goldens reblessed with identical objects)
  and moved the slag fallback to iiex through `MetalRegistry.DefaultRecoveryFallback`; A3 made eight
  types internal and hid fifteen behind `[EditorBrowsable(Never)]`; A4 published `Supported-API.md`
  with two guards. Deviations from the task text: the `Legacy` shims stay public (iiex binds to them
  on 1.20 and 1.21), the three legacy-only construction types are listed as such, and `MoltenCellHost`
  stays public (extension methods used by iiex). The Chargeable-Firebox exclusivity moved from the
  layout builder to an iiex invariant test. Stage gate: current lane green; all-versions lane green
  after the legacy visibility fix; `format -Check` cannot run on an uncommitted tree and was skipped.
- **Stage B landed 2026-09-05** (uncommitted, nine lanes green, exlib 2069 tests): B1 every catalogue
  loader returns a `CatalogueLoadReport`, names its file in every warning and rejects unknown keys
  (`JsonKeyAudit` for the hand parsers, `MissingMemberHandling.Error` for `ToObject<T>`); B5 every
  registry has `Contributors` invoked after each load; B2 `Raw` is `[Obsolete]` for `RootKey`,
  `KnownRootKeys` reflects the loader's target types (`Vintagestory.ServerMods.NoObf.BlockType` and
  `ItemType`, vendored under vsessentialsmod) and the injector warns on unknown root keys; B3 the
  origin check is opt-in through `MultiblockLayoutBuilder.Core(symbol)` because the multiblock DSL
  has no reserved anchor glyph, so it is dormant for the nine shipped layouts until H2 gives the grid
  core one anchor rule and the layouts declare it; B4 the three notifications (re-registration from
  another assembly, missing filler block, missing `[assembly: ExDomain]`). The Obsolete table names
  0.8.0 and 0.9.0 as the next two releases; that is an assumption for the owner to confirm.
- **Stages G and H added 2026-09-05** from the owner's layout and unification directives and an
  architect audit.
- **Stage G landed 2026-09-05** (uncommitted, nine lanes green, exlib 2070 tests): G1 twelve
  activity folders with one namespace each (`Registries`, `Config`, `Definitions`, `Blocks`,
  `Migrations`, `Structures`, `Machines`, `Networks`, `Catalogues`, `Helpers`, `Legacy`, plus
  `Industry`), tests mirroring them, generators and wiki following, a guard pinning the namespace set;
  `MeasurePreference` went to `Helpers/Measure` beside `ExMeasure`. G2 the harness in six folders
  under one namespace (`Doubles` folded in). The 36 doc-reference warnings the moves left were swept
  (incremental build warning-free; a from-scratch build still shows 24 nullable warnings in iiex and
  siex that predate this work).
- **H1 and H2 landed 2026-09-05** (uncommitted, nine lanes green, exlib 2088 tests): `LayoutAttribute`
  is the shared never-throwing reader of the three multiblock attribute schemas; `CellGrid`,
  `GridPlane`, `GridOptions`, `SymbolLegend<T>` and `DuplicatePolicy` replace the four grid parsers;
  `MultiblockLayoutBuilder` gained `Slice` and `Face`; `FillerLayoutBuilder` mixes planes; the
  harness's `SceneGrid` (space advances the column) and `SceneDiagram` forward to the core, with both
  space rules pinned by tests; `LayoutTable.From`/`.Rotated` lifted from `FurnaceLayoutRig`.
- **C2 landed 2026-09-05** (uncommitted, nine lanes green, exlib 2093 tests): `BlockFilledMegastructure`
  registered as `ExFilledMegastructure`, `BlockEntityMultiblock` as `ExMultiblock`, and a
  `multiblockLayout` block attribute (origin, legend, layers, core) that `JsonMultiblockLayout`
  replays through `MultiblockLayoutBuilder` at `OnLoaded` into the same `multiblockStructure` and
  derived `fillerOffsets` a code-first definition emits; a malformed layout logs one Error and the
  structure stays incomplete. Two new registered class strings; lang keys
  `exlib:multiblock-incomplete` and `-complete` in en, ru, uk.
- **C1 landed 2026-09-06** (uncommitted, nine lanes green, exlib 2108 tests): `ExItemDef` gained
  `Behavior` (three overloads), `Handbook`, `HandbookExclude`, `SkipVariants`, `AttributeByType`,
  `RootKeyByType`, `ShapeByType`, `TextureByType`, `VariantGroupFromProperties` and the positional
  transform overloads; a parity test keeps the item builder aligned with the block builder through an
  allow-list of block-only names; `ExBlockDef` gained `RenderPass`, `FaceCullMode` and `DrawType`
  enum overloads (those keys are block-only in the loader, so the item builder has none).
- **D5 and convenience V2 landed 2026-09-06** (uncommitted, nine lanes green, exlib 2128 tests):
  `BlockEntityStateHost` gives `ExBlockEntity` and the four bases one declared-state accessor
  (renamed from `State` to `Persisted` in H3 because content block entities already own `State`);
  `ExBlockState.Tree` for nested trees; `[Persist]` with `PersistScan` (compiled accessors cached per
  type, base members first, `Legacy` read when the new key is absent) and `IPersistable`.
- **H3 landed 2026-09-06** (uncommitted, nine lanes green, exlib 2131 tests):
  `ExRightClickConstructable` publishes `IProductionReadiness` (`gatesProduction` opt-out in JSON and
  through `ConstructionStages.GatesProduction`); the boiler, engine and transmission dropped their
  hand-written `CanRunProduction => IsConstructed` gates; the converter keeps one cross-block gate
  with a comment; the declared-state accessor is `Persisted`. A builder's `git stash` detour during
  this task lost nothing (the tree equalled its own last stash) and produced the standing rule that
  no implementer touches the index or the tree with git.
- **D1 and D2 landed 2026-09-06** (uncommitted, nine lanes green after the glibc tunable, exlib 2153
  tests): `ExMods` (`IsLoaded`, `Version`, `AtLeast` through `GameVersion.IsAtLeastVersion`,
  `WhenLoaded`, `FlagKey`) with `ExModsModSystem` setting `exlib:mod:<id>` world-config flags at
  ExecuteOrder 0.0 on both sides (each side's mod loader is local, so no sync is needed);
  `ExHarmony.PatchOnce` (`PatchAllUncategorized`), `PatchCategoryWhenLoaded` (tracks applied pairs
  because Harmony 2.4.2's `PatchCategory` is not idempotent) and `UnpatchAll`; the three copied
  bootstraps replaced. The 1.20 lane needs `GLIBC_TUNABLES=glibc.rtld.execstack=2` on glibc 2.41+
  (MonoMod's native helper); `scripts/exmod.ps1` sets it.
- **D3 landed 2026-09-06** (uncommitted, nine lanes green, exlib 2160 tests): `IExConfigAccess`
  gained `ExportJson`/`ImportJson` (import clamps like `Load` and never saves); `ConfigSyncPacket` on
  channel `exlib.config`; `ExConfigSyncModSystem` sends every registered section at `PlayerJoin` and
  `/exmod config set` broadcasts the changed one; the generated accessors read through the live
  register, so no refresh step exists.
- **D4 landed 2026-09-06** (uncommitted, nine lanes green, exlib 2169 tests): `ExpandedLib.Checks`
  (`ICheckSource`, `CheckResult`, `ExlibChecks`, `AssetCheckSource`, seven checks) runs at
  `AssetsFinalize` unless `ExlibConfig.RunChecksOnLoad` is false, and `/exmod verify [domain]` runs it
  on demand; the harness's `CodePrefixCollision` and `LangCoverage` wrap the library through
  `AssemblyCheckSource`, while `MultiblockCodes`, `PinnedNetworkNodes`, `RecipeCodes` and
  `DefinitionCatalogue` keep their richer test-facing shapes beside independent library versions;
  `NetworkNodeContractCheck` selects nodes by declared JSON contract rather than by C# class;
  `TestWorld` gained `World.Blocks`, `World.Items` and `Api.Assets`.
- **H4 and H6 landed 2026-09-06** (uncommitted, nine lanes green, exlib 2180 tests):
  `ReflectionScan.GetCandidateTypes` and `ForEachAttributed` serve the command and preference
  registries and the migration discovery (`EntityRegistry` keeps its loop: it registers types, not
  instances); `ExOrientation.SegmentedCode` behind the layout builder and the facings;
  `ExMeshCache.GetOrCreateRef`/`DisposeGroup` with the molten barrel converted; `ExHighlightSlots`
  behind the network highlight (the structure highlight keeps vanilla's shared slot on purpose, so
  vanilla's clear still clears it). H5 narrowed to `ConfigSubCommand` and `RecipesSubCommand` under
  `RegistrySubCommand<T>`; the measure command is bound to one preference and stays separate. A test
  pinning the migration discovery order is deferred to the testing plan's T10.
- **H5 landed 2026-09-06** (uncommitted, nine lanes green, exlib 2186 tests): `RegistrySubCommand<T>`
  owns the code-then-tail sub-command tree and dispatch; `ConfigSubCommand` and `RecipesSubCommand`
  derive from it with their messages unchanged; `MeasureSubCommand` stays a plain `IExSubCommand`
  and Commands.md says why. Stage H is complete; E1 in flight.
- **E1 landed 2026-09-06** (uncommitted, nine lanes green, exlib 2195 tests):
  `ContributedCatalogueLoader<TSet, TRegistry>` behind the route, job and bay loaders (their static
  entry points forward), each file parsed once, sources threaded per item (the three `SourceOf`
  fallbacks deleted); `ProcessRouteRegistry` on `ExKeyedRegistry`; the naming law on
  conventions.md with `CatalogueNamingTests` scoped to the six catalogue registries;
  `ExLiquids.Load` obsolete for `LiquidCatalogueLoader.Load`; `PublicSurfaceTests` made
  arity-aware for two-parameter generics. Left on the framework plan: F3 (after the convenience
  helpers and the smoke lane) and E2 (last).
- **F3 landed 2026-09-06** (ten lanes green: the three suites plus the sample's two tests on 1.22;
  the full tree boots in the smoke lane): `samples/HelloExpanded` (163 lines of C# for the mod, 79
  for its tests) with a block, a `[Persist]` counter on a production machine, a config value, an
  `/exmod hello` sub-command, `ExInteraction`, `ExInfo` and `ExBlockAccess` in use; Getting-Started
  walks it file by file with the real log lines; the smoke lane boots it by default. Found on the way:
  `ExBlockDef.MineTool` writes a `mineTool` root key the loader does not read (the known-key warning
  reports it); E2 resolves it.
- **E2 landed 2026-09-06** (ten lanes green, smoke green, full rebuild at zero warnings from 48):
  `MineTool` is an obsolete no-op (vanilla has no per-block tool key; `Material` and `MiningTier`
  already carry the intent) and its three iiex calls and the sample's are gone; `HandbookExclude`
  now writes `attributes.handbook.exclude`, the key the handbook reads (the top-level form never
  worked); the dead `temperatureDamage` root key left seven item definitions; `KnownRootKeys` reflects
  non-public `[JsonProperty]` members and the audit ignores the loader's generic `*ByType` suffix;
  the affected goldens reblessed for those keys only; Supported-API and the three changelogs
  complete; 399 wiki links checked, none broken. The framework plan is done except nothing: F1-F3,
  A-H and E are landed.
- **MSBuild node reuse turned off 2026-09-05** (`Directory.Build.rsp` with `-nodeReuse:false`,
  `MSBUILDDISABLENODEREUSE=1` in `scripts/exmod.ps1`) after 74 idle worker nodes held 11 GB; the
  reuse-enabled ones turned out to be the VS Code C# Dev Kit's design-time builds, fixed through
  `~/.vscode-server/server-env-setup`; documented in `docs/internal/testing.md`.

## The convenience rule (owner, 2026-09-05)

Modders are people and people are lazy: a capability that takes work to adopt is not adopted. Every
public capability in this plan and its companions ships three rungs, and its wiki page shows the
shortest first:

1. a zero-config default - it works with nothing declared (a base class or a ModSystem does it);
2. a declarative form - an attribute, a JSON key or a DSL call names the intent in one line;
3. the explicit API - for the case the first two rungs cannot express, without leaving the framework.

If the shortest usage on a page is more than a handful of lines, the page is reporting a missing
rung, and the rung is built before the feature ships. Fewer namespaces and fewer calls beat
symmetry; Stage G below reorganises the tree to that rule.

## Global constraints

- Plain ASCII in every repo file. Comments describe the code as it is; no narration, no history, no
  "Note that". Every public member gets XML docs: parameters with units and ranges, return value,
  every exception.
- Never commit; the owner stages. Every task leaves the tree building and its suite green.
- Save compatibility: registered block-entity class strings, block and item codes, config file names
  and section ids do not change. A namespace move is save-safe; a registration code change is not.
- Goldens change only when the emitted JSON changes on purpose: `EXLIB_WRITE_GOLDENS=1` scoped to
  the domain, and the golden diff is read before the task closes.
- New lang keys go in `mods/exlib/assets/exlib/lang/en.json` and in every other locale file in that
  folder (the LangParity guard fails otherwise).
- A public API change updates its wiki page in the same task (`mods/exlib/wiki/`, WikiParity guard).
- New public API compiles on all three game versions; a member missing on 1.20 or 1.21 gets a shim
  in `mods/exlib/src/Legacy/` guarded by the `GAME_GE_*` constants, never an `#if` in the feature.
- Gate per task: `dotnet build VintageStory.sln -clp:ErrorsOnly` then
  `bash scripts/exmod.sh test latest` (add `-Filter <Class>` while iterating). Stage gate:
  `bash scripts/exmod.sh test all` and `bash scripts/exmod.sh format -Check`.
- Two implementers never run in the working tree at once on code tasks; docs tasks may overlap
  with one code task.

---

## Stage A - boundary and relocation (contract; one reviewer at the end of the stage)

### Task A1: the Industry namespace

**Files:**
- Create: `mods/exlib/src/Industry/{Pipes,Molten,MechanicalPower,Metals,Heat,Helpers,Materials}/`
- Move (plain mv, never git mv: the index is the owner's; keep file names):
  - to `Industry/Pipes`: `Networks/PipeNetwork.cs`, `Networks/PipeNetworkState.cs`,
    `Networks/IPipeNode.cs`, `Networks/IBurstablePipe.cs`, `Networks/IChimneyVentable.cs`,
    `Networks/IPipeVentStrategy.cs`, `Networks/IThroughputLimitedPipe.cs`,
    `Blocks/Networks/BlockPipe.cs`, `Blocks/Networks/BlockPipePassthrough.cs`,
    `Blocks/Networks/BlockEntityPipe.cs`, `Blocks/Networks/BlockEntityPipePassthrough.cs`,
    `Blocks/Networks/ChimneyVent.cs`
  - to `Industry/Molten`: `Networks/MoltenNetwork.cs`, `Networks/IMoltenCell.cs`,
    `Blocks/Structures/BEBehaviorMoltenCell.cs`, `Blocks/Structures/MoltenCellHost.cs`,
    `Metals/MoltenMetal.cs`, `Metals/MoltenContents.cs`, `Metals/MoltenCharge.cs`,
    `Metals/MoltenChisel.cs`, `Metals/IChiselableMolten.cs`
  - to `Industry/MechanicalPower`: `Networks/MpEnergyNetwork.cs`, `Networks/MpEnergyNetworkState.cs`,
    `Networks/MpEnergyNodes.cs`, `Blocks/Structures/BEBehaviorMPFillerPort.cs`,
    `Blocks/Machines/BEBehaviorMPSubmachineBase.cs`, `Helpers/MPAnim.cs`
  - to `Industry/Metals`: `Metals/MetalDef.cs`, `Metals/MetalRegistry.cs`,
    `Metals/MetalCatalogueLoader.cs`, `Metals/MetalFamilyEmitter.cs`, `Metals/MetalToolEmitter.cs`
  - to `Industry/Heat`: `Heat/HeatBalance.cs`, `Heat/HeatBalanceHud.cs`
  - to `Industry/Helpers`: `Helpers/ExSounds.cs`, `Helpers/ExParticles.cs`,
    `Helpers/ExBlockNames.cs`, `Helpers/ExMoldGate.cs`, `Helpers/ExMoldDrops.cs`
  - to `Industry/Materials`: the `Roles` constants class from `Materials/MaterialRoleDef.cs`
    (split into its own file `Industry/Materials/Roles.cs`; `MaterialRoleDef` stays in core)
- Modify: every `namespace` line in the moved files to `ExpandedLib.Industry.<Folder>`; every
  `using` in `mods/exlib`, `mods/iiex`, `mods/siex` and the three test projects that referenced the
  old namespace; `mods/exlib/wiki/Block-Networks.md`, `Helpers-and-Renderers.md`,
  `Multiblock-Structures.md` (namespace lines in their snippets).
- Test: `mods/exlib/tests/Invariants/IndustryBoundaryTests.cs`

**Interfaces:**
- Produces: the namespaces `ExpandedLib.Industry.Pipes`, `.Molten`, `.MechanicalPower`, `.Metals`,
  `.Heat`, `.Helpers`, `.Materials`. Type names, members and registered codes are unchanged.

- [ ] **Step 1: write the guard test** in `IndustryBoundaryTests.cs`:

```csharp
[Fact]
public void Every_industry_type_lives_under_the_Industry_namespace() {
  string[] names = ["PipeNetwork", "MoltenNetwork", "MpEnergyNetwork", "MetalRegistry",
    "HeatBalance", "ExSounds", "ExParticles", "ExBlockNames", "ExMoldGate", "ExMoldDrops",
    "BEBehaviorMoltenCell", "BEBehaviorMPFillerPort", "MPAnim", "Roles"];
  var asm = typeof(ExpandedLib.ExpandedLibModSystem).Assembly;
  foreach (string n in names) {
    var t = asm.GetTypes().Single(x => x.Name == n);
    Assert.StartsWith("ExpandedLib.Industry.", t.Namespace);
  }
}
```

- [ ] **Step 2: run it, expect failure** (types still in their old namespaces).
- [ ] **Step 3: move the files and rewrite namespaces and usings.** The compiler is the oracle:
  build until `dotnet build VintageStory.sln -clp:ErrorsOnly` is clean across exlib, iiex, siex and
  the three test projects.
- [ ] **Step 4: check save safety.** `grep -rn "BlockEntityRegister\|BlockRegister\|BlockEntityBehaviorRegister" mods/exlib/src/Industry` must show the same attribute arguments as before the move (the registered code is the attribute argument or the bare class name, never the namespace). Run `bash scripts/exmod.sh test latest -Filter ReleasedCode` and `-Filter EntityClass`.
- [ ] **Step 5: gate.** `bash scripts/exmod.sh test latest` green; goldens unchanged (`git status mods/*/tests/goldens` empty).

### Task A2: the family's defaults leave the core

**Files:**
- Modify: `mods/exlib/src/ExlibConfig.cs` (remove `MetalRecoveryFallback`), `mods/iiex/src/IiexConfig.cs`
  (add it with the same default `iiex:slag-block` and the same version migration comment
  discipline), every reader of `ExlibValues.MetalRecoveryFallback` (grep; point at `IiexValues`).
- Modify: `mods/exlib/src/Blocks/Structures/CellRole.cs`, `MultiblockCellRoles.cs`, `CellRole.cs`'s
  `SingleCellAttribute` and `CellRoles`, `mods/exlib/src/Definitions/MultiblockLayoutBuilder.cs`
  (role arity validation), `mods/exlib/src/Definitions/StructureLayout.cs`; the 63 `CellRole.`
  sites in iiex and siex; `mods/exlib/wiki/Multiblock-Structures.md` (roles section).
- Create: `mods/iiex/src/BlockStructures/Furnaces/FurnaceCellRoles.cs`.
- Test: `mods/exlib/tests/Structures/CellRoleTests.cs` (new), existing
  `MultiblockCellRolesTests.cs` adjusted to declare its own roles.

**Interfaces:**
- Produces:
  ```csharp
  namespace ExpandedLib.Blocks.Structures;
  /// A cell role is a string key declared by the mod that owns the machine; exlib declares none.
  public readonly record struct CellRole(string Key) {
    public static CellRole Of(string key);   // throws ArgumentException on empty or whitespace
    public override string ToString() => Key;
  }
  public static class FurnaceCellRoles {      // in iiex
    public static readonly CellRole Chargeable = CellRole.Of("chargeable"); // and the other eight
  }
  ```
  `[SingleCell(string role)]` takes the key string; `MultiblockCellRoles` and `CellRoles` are
  keyed by `CellRole`; every former `CellRole.X` enum use becomes `FurnaceCellRoles.X`.

- [ ] **Step 1: tests first.** In `CellRoleTests.cs`: `Of_rejects_blank`, `Two_roles_with_one_key_are_equal`, and a `MultiblockCellRolesTests` case that declares a role named `"kiln-door"` from the test itself and finds it through `MultiblockCellRoles`.
- [ ] **Step 2: run, expect compile failure** (record struct absent).
- [ ] **Step 3: implement**, then move the nine names into `FurnaceCellRoles` and fix the 63 sites.
- [ ] **Step 4: move the slag fallback**; `git grep MetalRecoveryFallback` shows no exlib reader.
- [ ] **Step 5: gate**: `bash scripts/exmod.sh test latest`. Config goldens for `ex_values.json` (if any) are reblessed for the removed key only.

### Task A3: prune and mark

**Files:**
- Modify: the types listed below in `mods/exlib/src`; `mods/exlib/src/InternalsVisibleTo.cs` (add
  `SteelIndustryExpanded.Tests` if a test needs it).
- Create: `mods/exlib/tests/Invariants/PublicSurfaceTests.cs`.

**Rules for the pass** (each name below was referenced nowhere outside `mods/exlib/src` on 2026-09-05):
- Make `internal`: `AssetCatalogueLoader`, `ExConfigFiles`, `LegacyAnimUtil`, `LegacyApi120`,
  `LegacyLinq`, `MoltenCellHost`, `FillerSlab` unless the layout DSL exposes it in a public signature,
  `NetworkHighlightRequest`, `ExPreferencesConfig`.
- Mark `[EditorBrowsable(EditorBrowsableState.Never)]` (public by necessity, not contract):
  `ExpandedLibModSystem`, `ExDefinitionModSystem`, `ChunkColumnSweeperModSystem`,
  `NetworkHighlightModSystem`, `ExmodCommand`, `ConfigSubCommand`, `HealSubCommand`,
  `MeasureSubCommand`, `NetworkSubCommand`, `RecipesSubCommand`, `HandbookUnitPatch`,
  `Structurefiller` (generated), `ExlibConfig`, `MeasurePreference`.
- Keep public and document on the Supported API page (contract reached through attributes, schema
  or return types): `BlockBehaviorRegisterAttribute`, `CollectibleBehaviorRegisterAttribute`,
  `CommandRegisterAttribute`, `SubCommandRegisterAttribute`, `PreferenceRegisterAttribute`,
  `ExDomainAttribute`, `ExDefDomainAttribute`, `IExCommand`, `IExSubCommand`, `IExPreference`,
  `IBurstablePipe`, `IPipeVentStrategy`, `IThroughputLimitedPipe`, `ExConfigEditResult`,
  `MetalAlloyIngredient`, `MetalAlloySpec`, `BayOccupancy`, `BayOccupancySet`, `ConstructionStages`,
  `ExConstructionIngredient`, `ExPreferences`, `MachineStationInventory`.
- A type that cannot become internal because a public member exposes it stays public and is
  listed on the page.

- [ ] **Step 1: the guard.** `PublicSurfaceTests.Every_public_type_is_listed_or_hidden` reads
  `mods/exlib/wiki/Supported-API.md` (Task A4), collects every public type of `exlib.dll` outside
  `ExpandedLib.Industry`, and asserts each is named on the page or carries
  `[EditorBrowsable(Never)]`. Run it: it fails until A3 and A4 are both done.
- [ ] **Step 2: apply the rules**, compiler as oracle, then `bash scripts/exmod.sh test latest`.
- [ ] **Step 3: gate** with the guard green after A4.

### Task A4: the Supported API page and the deprecation rule

**Files:**
- Create: `mods/exlib/wiki/Supported-API.md`
- Modify: `mods/exlib/wiki/_Sidebar.md`, `Home.md` (one line under "Where to start"),
  `mods/exlib/CHANGELOG.md` (Unreleased: the namespace move, the pruning, the rule),
  `mods/exlib/README.md` (one paragraph "What is supported").

**Content of the page:** a table per namespace (`ExpandedLib`, `.Blocks`, `.Blocks.Structures`,
`.Blocks.Machines`, `.Blocks.Networks`, `.Blocks.Construction`, `.Blocks.Migrations`,
`.Networks`, `.Definitions`, `.Registries.*`, `.Processes`, `.Materials`, `.Fluids`, `.Storage`,
`.Helpers`, `.Renderers`, `.Checks`) listing every public type with one phrase; a section
"ExpandedLib.Industry" stating: public, reusable, changes without notice, documented on the
family's own pages; the rule: a public member is removed only after one full release marked
`[Obsolete]` with the replacement named in the message; `[EditorBrowsable(Never)]` means "public
because the engine needs it, not for you".

- [ ] **Step 1: write the page** from the actual type list (`grep -rhoE 'public (static |abstract |sealed |partial )*(class|interface|struct|enum|record|record struct) [A-Za-z<>]+' mods/exlib/src --include=*.cs`).
- [ ] **Step 2: run** `bash scripts/exmod.sh test latest -Filter PublicSurface` green.
- [ ] **Stage A gate:** `bash scripts/exmod.sh test all`, `format -Check`; one reviewer reads the diff of A1-A3 for accidental public-surface loss and any changed registration code.

---

## Stage B - fail loudly

### Task B1: catalogue diagnostics

**Files:**
- Modify: `mods/exlib/src/Industry/Metals/MetalCatalogueLoader.cs`, `mods/exlib/src/Fluids/ExLiquids.cs`
  (its loader), `mods/exlib/src/Materials/MaterialRoleLoader.cs`,
  `mods/exlib/src/Processes/ProcessRouteLoader.cs`, `ProcessJobLoader.cs`,
  `mods/exlib/src/Storage/BayOccupancyLoader.cs`, `mods/exlib/src/Registries/AssetCatalogueLoader.cs`,
  `mods/exlib/src/ExpandedLibModSystem.cs` (AssetsFinalize).
- Create: `mods/exlib/src/Registries/CatalogueLoadReport.cs`.
- Test: `mods/exlib/tests/Registries/CatalogueLoadReportTests.cs`, one case per loader in the
  existing loader test classes.

**Interfaces:**
- Produces:
  ```csharp
  namespace ExpandedLib.Registries;
  /// One catalogue's load outcome: files read, entries accepted, errors, each error naming its asset.
  public sealed record CatalogueLoadReport(string Catalogue, int Files, int Entries,
    IReadOnlyList<string> Errors) {
    /// "[exlib] metals: 3 file(s), 12 entr(ies), 0 error(s)" and one Error line per entry.
    public void Log(ILogger logger);
  }
  ```
  Every `*Loader.Load(ICoreAPI)` returns a `CatalogueLoadReport`; `ExpandedLibModSystem.AssetsFinalize`
  logs each. Every warning or error string starts with the asset location
  (`asset.Location` for `AssetCatalogueLoader`, the `source` string the route/job/bay loaders already
  carry, and the file path threaded into `MaterialRoleLoader.Overlay` and `MetalCatalogueLoader.Populate`).
- Strict binding: `AssetCatalogueLoader.SafeToObject<T>` deserialises with
  `MissingMemberHandling.Error` and reports the offending key and file; the `JsonObject`-based
  parsers (route, job, bay) compare each object's keys against the schema's known set and report
  unknown keys as errors naming file and key.

- [ ] **Step 1: tests.** For each loader: a fixture asset with one misspelt key (`"thicknes"`) yields a report with one error containing the file name and `thicknes`; a valid set yields `Files == n`, `Errors.Count == 0`; `Log` writes one summary line.
- [ ] **Step 2: run, expect failure.**
- [ ] **Step 3: implement**; keep per-file catch-and-skip semantics (one bad file never blocks the others).
- [ ] **Step 4: gate** `bash scripts/exmod.sh test latest`. Wiki: `Extending-Processes.md` gets a "What the log tells you" section quoting the summary line.

### Task B2: `RootKey` and the known-key warning

**Files:**
- Modify: `mods/exlib/src/Definitions/ExBlockDef.cs` (`Raw` -> `RootKey`, `RawByType` ->
  `RootKeyByType`, old names kept with `[Obsolete("Use RootKey; Raw writes a top-level key")]`),
  `mods/exlib/src/Definitions/ExItemDef.cs` (same), `mods/exlib/src/Definitions/ExDefinitionModSystem.cs`
  (emit-time check), every in-repo caller of `Raw` (grep iiex, siex, tests).
- Create: `mods/exlib/src/Definitions/KnownRootKeys.cs`.
- Test: `mods/exlib/tests/Definitions/KnownRootKeysTests.cs`.

**Interfaces:**
- Produces:
  ```csharp
  namespace ExpandedLib.Definitions;
  /// The top-level keys the game's object loader reads for a block or item type, taken by reflection
  /// from the loader's target types so the set follows the installed game version.
  public static class KnownRootKeys {
    public static IReadOnlySet<string> Block { get; }   // from Vintagestory.ServerMods.NoObf.BlockType
    public static IReadOnlySet<string> Item { get; }    // from Vintagestory.ServerMods.NoObf.ItemType
    public static bool IsKnownBlockKey(string key);
    public static bool IsKnownItemKey(string key);
  }
  ```
  `ExDefinitionModSystem` logs a Warning naming the def's code and the key for every root key of
  every registered def that is not known.

- [ ] **Step 1: tests.** `Block_contains_shape_and_variantgroups`, `Item_contains_attributes_and_creativeinventory`, `A_def_with_an_unknown_root_key_is_reported` (through a small internal `Audit(def)` returning the offending keys).
- [ ] **Step 2: run, expect failure.**
- [ ] **Step 3: implement**; rename in-repo callers to `RootKey`.
- [ ] **Step 4: gate**; goldens unchanged (the emitted JSON is the same). Wiki `Code-First-Definitions.md` (Stage F) documents `RootKey` and the warning; CHANGELOG lists the rename.

### Task B3: layout origin validation

**Files:**
- Modify: `mods/exlib/src/Definitions/MultiblockLayoutBuilder.cs`
- Test: `mods/exlib/tests/Definitions/MultiblockLayoutBuilderTests.cs`

**Behaviour:** `Build()` throws `InvalidOperationException` naming the layout, the declared origin and
the anchor's grid position when `Origin(xLeft, zTop)` is not the negation of the anchor cell's
position in the grid (the anchor is the cell carrying the anchor symbol). The filler DSL's own
validation is the model.

- [ ] **Step 1: test** `Build_rejects_an_origin_that_does_not_match_the_anchor` and `Build_accepts_the_matching_origin`.
- [ ] **Step 2: run, expect failure.**
- [ ] **Step 3: implement** in `Build()`.
- [ ] **Step 4: gate**; every shipped layout still builds (the definition goldens prove it).

### Task B4: the three quiet spots

**Files:**
- Modify: `mods/exlib/src/Definitions/ExDefinitions.cs`, `mods/exlib/src/Blocks/Structures/StructureFillers.cs`,
  `mods/exlib/src/Registries/Entities/EntityRegistry.cs`.
- Test: `mods/exlib/tests/Definitions/ExDefinitionsTests.cs`, `mods/exlib/tests/Structures/StructureFillersTests.cs`.

**Behaviour:**
- `ExDefinitions.Register*`: when a def replaces one whose provider type lives in a different
  assembly, log a Notification: `"[exlib] <location> re-registered by <asm2> (was <asm1>)"`; the
  registry keeps a `ProviderAssembly` per entry to know.
- `StructureFillers.CanPlace/PlaceFillers/RemoveFillers`: when `world.GetBlock(FillerCode)` is null,
  log an Error once per session naming `FillerCode` instead of returning silently.
- `EntityRegistry`: when a cross-mod `Class<T>()` resolution falls back to the class name because
  the assembly declares no `[assembly: ExDomain]`, log a Warning naming the assembly.

- [ ] **Step 1: tests** for each (a recording `ILogger` substitute; `TestWorld` for the filler case).
- [ ] **Step 2-4:** implement, gate.

### Task B5: contributor hooks on every catalogue

**Files:**
- Create: `mods/exlib/src/Registries/CatalogueContributors.cs`
- Modify: `mods/exlib/src/Industry/Metals/MetalRegistry.cs` and `MetalCatalogueLoader.cs`,
  `mods/exlib/src/Fluids/ExLiquids.cs`, `mods/exlib/src/Processes/ProcessRouteRegistry.cs` and
  `ProcessRouteLoader.cs`, `ProcessJobRegistry.cs` and `ProcessJobLoader.cs`,
  `mods/exlib/src/Storage/BayOccupancyRegistry.cs` and `BayOccupancyLoader.cs`,
  `mods/exlib/src/Materials/MaterialRoleRegistry.cs` (adopt the shared type).
- Test: `mods/exlib/tests/Registries/CatalogueContributorsTests.cs`.

**Interfaces:**
- Produces:
  ```csharp
  namespace ExpandedLib.Registries;
  /// Code contributions to one catalogue, re-invoked after every load so a C# entry survives the
  /// clear that precedes each AssetsFinalize read.
  public sealed class CatalogueContributors {
    public void Register(Action<ICoreAPI> contributor);
    public void Clear();
    public int Count { get; }
    public void Invoke(ICoreAPI api);   // called by the owning loader after its overlay
  }
  ```
  Each registry exposes `public static CatalogueContributors Contributors { get; }`;
  `MaterialRoleRegistry.RegisterContributor` becomes a forwarder to it (kept, not obsoleted).
  `ProcessExtensions.Shared.AddStages/AddJobs` register through the contributors so a route or job
  added from C# survives the next load.

- [ ] **Step 1: tests.** For each catalogue: register a contributor adding one entry, run `Load(api)` twice, assert the entry is present after both loads.
- [ ] **Step 2-4:** implement, gate. Wiki `Extending-Processes.md` "From C#" section names `Contributors` for every catalogue.

- [ ] **Stage B gate:** `bash scripts/exmod.sh test all`, `format -Check`.

---

## Stage G - the tree, organised (runs right after Stage B, before C; contract)

The layout rule is on the design page (`docs/design/conventions.md`, "How exlib is laid out"). Later
tasks in this plan and its companions name files by their pre-G paths; map them with the table below.

### Task G1: `mods/exlib/src` and `mods/exlib/tests`

**Moves (plain mv; namespace = top-level folder; sub-folders add no namespace segment):**

| From | To | Namespace |
|---|---|---|
| `Registries/Entities/*`, `Registries/Commands/*`, `Registries/Preferences/*`, `Registries/Recipes/*`, `Registries/ExKeyedRegistry.cs`, `Registries/ReflectionScan.cs`, `Commands/*`, `Preferences/MeasurePreference.cs` | `Registries/{Entities,Commands,Preferences,Recipes}/` (sub-folders keep their files; the six `/exmod` implementations go to `Registries/Commands/`) | `ExpandedLib.Registries` |
| `Registries/Config/*` | `Config/` | `ExpandedLib.Config` |
| `Registries/AssetCatalogueLoader.cs`, `CatalogueLoadReport.cs`, `CatalogueContributors.cs`, `JsonKeyAudit.cs`, `Processes/*`, `Materials/*`, `Fluids/*`, `Storage/*` | `Catalogues/` root for the shared four, `Catalogues/{Processes,Materials,Fluids,Storage}/` | `ExpandedLib.Catalogues` |
| `Blocks/ExBlockEntity.cs`, `ExBlockState.cs`, `Blocks/Behaviors/*`, `Blocks/Construction/*` | `Blocks/`, `Blocks/Construction/` | `ExpandedLib.Blocks` |
| `Blocks/Migrations/*`, `Blocks/Healing/*`, `Blocks/ChunkColumnSweeperModSystem.cs` | `Migrations/` | `ExpandedLib.Migrations` |
| `Blocks/Structures/*` | `Structures/` | `ExpandedLib.Structures` |
| `Blocks/Machines/*` | `Machines/` | `ExpandedLib.Machines` |
| `Blocks/Networks/*` (six files) | `Networks/` (joins the four model files) | `ExpandedLib.Networks` |
| `Renderers/*`, `Patches/HandbookUnitPatch.cs` | `Helpers/Rendering/`, `Helpers/Measure/` (with `ExMeasure.cs`, `MeasurePreference.cs` if it reads better there than in Registries - decide by what `ExMeasure` needs) | `ExpandedLib.Helpers` |
| `Definitions/*`, `Legacy/*`, `Generated/*`, `Industry/**` | unchanged | unchanged |

Then: every `using` in mods/exlib, mods/iiex, mods/siex and the three test projects follows (the
compiler is the oracle); `mods/LegacyUsings.cs` and any global usings follow; the wiki's namespace
lines follow (`Supported-API.md` is regenerated per namespace table; `Code-First-Definitions.md`,
`Lifecycle.md`, `Block-Networks.md`, `Registries.md`, `Config-System.md`, `Multiblock-Structures.md`,
`Production-Machines.md`, `Extending-Processes.md`, `Helpers-and-Renderers.md`,
`Migrations-and-Healing.md` snippets); `mods/exlib/tests` folders mirror the new tree (`Config/`,
`Registries/`, `Catalogues/{Processes,Materials,Fluids,Storage}/`, `Blocks/`, `Migrations/`,
`Structures/`, `Machines/`, `Networks/`, `Definitions/`, `Helpers/`, `Checks/` when D4 lands,
`Harness/`, `Invariants/`, `Localization/`, `Generators/`; `Recipes/` merges into `Registries/`).
`IndustryBoundaryTests` and `PublicSurfaceTests` keep passing; `Supported-API.md` gains a sentence per
namespace saying what activity it serves. Registered codes, save keys and goldens are unchanged (the
registered key never carries a namespace).

- [ ] Guard first: extend `IndustryBoundaryTests` with `Every_contract_namespace_is_a_top_level_folder`: the set of namespaces of public types outside `Industry` equals `{ExpandedLib, .Registries, .Config, .Definitions, .Blocks, .Migrations, .Structures, .Machines, .Networks, .Catalogues, .Checks, .Helpers, .Legacy}` minus the ones that hold no public type yet; it fails until G1 is done.
- [ ] Move, rewrite namespaces and usings, build clean, `bash scripts/exmod.sh test all` green, goldens unchanged.

### Task G2: `mods/exlib/testing`

One namespace `ExpandedLib.Testing` (the `Doubles` sub-namespace is folded in). Folders: `World/`
(`TestWorld`, `TestBlocks`, `TestLang`, `VsAssemblyResolver`), `Scenes/` (`Scene`, `SceneDiagram`),
`Rigs/` (`StructureRig`, and the rigs the testing plan adds), `Doubles/` (the eight doubles and the
ones the testing plan adds), `Checks/` (`DefinitionParity`, `DefinitionGoldens`, `DefinitionAssets`,
`DefinitionCatalogue`, `DefinitionCodes`, `DefinitionJson`, `NetworkNodeContract`, `MultiblockCodes`,
`RecipeCodes`, `LangCoverage`, `LangCallSites`, `WikiParity`, `HandbookSync`, `MegablockFrames`,
`ShapeExtents`, `CodeLiterals`, `CodePrefixCollision`, `CostSelectorOverlap`, `PinnedNetworkNodes`,
`PressureVesselGate`, `ReferencedCodes`), `Repo/` (`RepoPaths`, `ReleasedCodes`, `ReleasedVersions`,
`ReleasedCodeDebt`, `BlockCodeEmitter`), root: `ReflectionHelpers`. Usings in the three test suites
follow; `Testing-Harness.md` and `Testing-API-Reference.md` get a "Where things are" table.

- [ ] Move, build clean, `bash scripts/exmod.sh test latest` green.


---

## Stage H - one system with derivations (audit 2026-09-05; runs after G, interleaved with C and D)

The owner's rule: two systems that describe the same thing become one core with derivations. An
architect pass on 2026-09-05 audited nine candidates; these are the accepted ones, in order of
payoff over effort. Rejected as look-alikes (stores, ports and connectors, emitters, `GameTime`
versus `GraceTimer`, highlight versus projection) stay separate. Paths are post-G1.

### Task H1: the multiblock attribute readers share one core (first; S)

`Structures/MultiblockCellRoles.cs`, `MultiblockConnectors.cs`, `MultiblockFacings.cs` have the same
skeleton (a `None` sentinel, `IsEmpty`, a never-throwing `FromAttributes(JsonObject?)`) and
`MultiblockCellRoles.Coord` is copied verbatim into `MultiblockConnectors`. Add
`Structures/LayoutAttribute.cs`:
```csharp
internal static class LayoutAttribute {
  /// A coordinate read that returns null for a missing or non-integer token, never throws.
  public static int? Coord(JToken? token);
  /// Every (cell, key) pair under attrs[key], in file order; empty for a missing or malformed node.
  public static IEnumerable<((int X, int Y, int Z) Cell, string Key)> CellsByKey(JsonObject? attrs, string key);
}
```
The three public types and their `None` sentinels stay (the attribute schemas are save-visible).
Proof: `tests/Structures/MultiblockCellRolesTests.cs`, `MultiblockConnectorsTests.cs`,
`tests/Definitions/MultiblockFacingsTests.cs`, goldens unchanged.
- [ ] Implement behind the existing tests; gate.

### Task H2: the grid-and-legend core (after G1; M)

Four parsers turn rows of symbols into cells: `Definitions/StructureLayout.Parse`,
`ParseVertical`, `ParseFrontal` (one loop three times, differing in which axis the row and column
feed), and `testing/Scenes/SceneDiagram` (a fourth copy where a space advances the column and an
unmapped glyph is skipped rather than refused). `FillerLayoutBuilder.Build` hand-dispatches over the
three entry points and refuses to mix planes; `MultiblockLayoutBuilder` can only draw `Layer`.
Two anchor rules exist (`Core` plus `Origin` negation versus the `'O'`/`'0'` glyph).

Core in `Structures/CellGrid.cs` and `Structures/SymbolLegend.cs`:
```csharp
namespace ExpandedLib.Structures;
/// Which plane a text grid draws: rows and columns map to world axes, depth to the third.
public enum GridPlane { Horizontal, SliceX, FaceZ }
public sealed record GridOptions(bool SpaceAdvancesColumn = false, char Empty = '.', char? Anchor = null);
/// Rows of symbols with an origin, an optional anchor glyph and a duplicate check; parsed once.
public sealed class CellGrid {
  public CellGrid(GridPlane plane, int originA, int originB, GridOptions? options = null);
  public CellGrid Add(int depth, string grid);
  public IReadOnlyList<LayoutCell> Cells { get; }
  public bool Drawn(char symbol);
  public (int X, int Y, int Z)? AnchorCell { get; }
}
/// Symbol to payload, with a duplicate policy and the "declared but never drawn" check.
public sealed class SymbolLegend<TPayload> {
  public SymbolLegend(DuplicatePolicy policy);   // Throw for definitions, Replace for fillers and scenes
  public SymbolLegend<TPayload> Map(char symbol, TPayload payload);
  public IReadOnlyList<string> Unused(CellGrid grid);
}
```
Derivations: `MultiblockLayoutBuilder` keeps its fluent surface over `CellGrid(Horizontal)` and
`SymbolLegend<string>` plus its role and connector side-tables, and gains `Slice` and `Face`;
`FillerLayoutBuilder` holds a `CellGrid` in the plane of its first call and a
`SymbolLegend<FillerCellSpec>`; the harness gets `Scenes/SceneGrid` over
`CellGrid(Horizontal, new GridOptions(SpaceAdvancesColumn: true))` and `SymbolLegend<Action<BlockPos>>`,
and `SceneDiagram` becomes a forwarder for its three call sites. Deleted: `ParseVertical`,
`ParseFrontal`, the plane-mixing refusal, `SceneDiagram`'s loop, the second anchor rule. Also lift
`FurnaceLayoutRig.LayoutOf`/`RotatedLayoutOf` (mods/iiex/tests/Fixtures) into the harness as
`Checks/LayoutTable.From(ExBlockDef)` and `.Rotated(def, angle)`; they read the emitted table and are
content-free. Task C2's JSON megablock takes its layout as an ASCII grid through the same core.
- [ ] First: a test pinning the space rule for scenes and the no-space rule for definitions; then
  implement; `tests/Definitions/StructureLayoutTests.cs` (eleven cases), `StructureFootprintTests.cs`,
  `MultiblockLayoutBuilderTests.cs` and every golden carrying `multiblockStructure` or `fillerOffsets`
  (10 and 23 files) unchanged; the three scenario suites green.

### Task H3: construction publishes readiness (S; medium risk)

`ExRightClickConstructable` has `IsComplete` but is not an `IProductionReadiness` publisher, so seven
machines forward it by hand (`ConstructedAnimator.IsConstructed`, then `_animator?.IsConstructed`
in `BlockEntityTransmission`, `BlockEntityBurdenmaker`, `BlockEntityConverterBessemer`, and ad hoc
gates in `BlockEntityEngineCornish`). Make the behaviour implement `IProductionReadiness`
(`IsReadyToProduce => IsComplete`, `StopsProductionWhenNotReady => true`) with an opt-out
`GatesProduction` read from its JSON properties, default true. The conjunction-versus-veto rule of
`framework-composition.md` is untouched.
- [ ] First audit the seven machines for one that must tick before construction completes (the boiler
  and the engine first); gate any such with `gatesProduction: false`; then implement;
  `tests/Machines/ProductionReadinessTests.cs`, `tests/Structures/MultiblockProcessTests.cs`,
  `mods/iiex/tests/Blocks/Forming/RollingMillClockTests.cs` and the scenario suites are the proof.

### Task H4: `ReflectionScan` is the one scanner (S)

`EntityRegistry`, `CommandRegistry`, `PreferenceRegistry` share an identical attributed-type loop;
`BlockMigrationModSystem.Discover` walks every assembly with its own type-load handling and sort. Add
`ReflectionScan.GetCandidateTypes(IEnumerable<Assembly>)` (sorted by assembly then type name) and
`ReflectionScan.ForEachAttributed<TAttr, TInstance>(api, modId, asm, register)`; the migration
discovery and the two registries call them. `ExDefinitions.Discover` stays apart (its provider
interface has a static abstract member and cannot be a type argument).
- [ ] Implement behind `tests/Registries/ReflectionScanTests.cs`, `RegistrationKeyTests.cs`,
  `tests/Migrations/*`; the migration ordering guarantee survives.

### Task H5: one sub-command shape (M)

`ConfigSubCommand`, `RecipesSubCommand`, `MeasureSubCommand` are the same list, show and set over an
`ExKeyedRegistry`. Add `Registries/Commands/RegistrySubCommand<T>` taking the name, the registry, a
describe function and a set function; the three derive from it; a modder gets `/exmod <thing>` for a
registry by deriving once.
- [ ] Implement; `LangCoverage` and `LangCallSites` guards catch a missed key.

### Task H6: three small cores (S each)

- `ExOrientation.SegmentedCode` (split, index, rejoin a code on `-`), used by
  `MultiblockLayoutBuilder.FindOrientationSegments` and `MultiblockFacings.Rotate`.
- `ExMeshCache.GetOrCreateRef` and the one bypass converted (`mods/iiex/.../BlockMoltenBarrel.cs`,
  a hand-rolled `ObjectCache` entry outside the cache's prefix).
- `Helpers/ExHighlightSlots.Reserve(string key)` replacing the two magic highlight slot ids in
  `NetworkHighlightModSystem` and `BlockEntityMultiblockStructure`.
- [ ] Implement; gate.

Corrections to scheduled tasks from the same audit: Task E1's loader base parses each file once
(today every `Load(api)` parses twice to count entries) and threads the source per parsed item
(today `SourceOf` misattributes every later error once one file failed); the testing plan's Task T1
gains `MachineRig.RunWhile(Action beforeEachStep, float seconds, float stepSeconds = 1f)` (thirteen
hand-written "hold a source and step" loops) and an `EnginePlant` base in iiex's fixtures for the
three `*Plant` fixtures that repeat its eight steps.


---

## Stage C - builders and the JSON megablock

### Task C1: `ExItemDef` parity and enum overloads

**Files:**
- Modify: `mods/exlib/src/Definitions/ExItemDef.cs`, `ExBlockDef.cs`.
- Test: `mods/exlib/tests/Definitions/ExItemDefTests.cs`, `ExBlockDefTests.cs`.

**Interfaces:**
- Produces on `ExItemDef`, each mirroring the `ExBlockDef` method of the same name and emitting the
  same JSON shape: `Behavior(string)`, `Behavior(string, object)`, `Behavior<T>()`,
  `Handbook(...)` (same overloads as the block builder), `SkipVariants(params string[])`,
  `AttributeByType(string key, string wildcard, object value)`,
  `TextureByType(string wildcard, string key, string path)`, and the positional transform
  overloads the block builder has (`GuiTransform`, `FpHandTransform`, `TpHandTransform`,
  `GroundTransform` taking translation, rotation, origin, scale floats).
- Produces on both builders: `RenderPass(EnumChunkRenderPass)`, `FaceCullMode(EnumFaceCullMode)`,
  `DrawType(EnumDrawType)` writing the same strings the string overloads write.

- [ ] **Step 1: tests** per new method: the emitted JSON path and value (`json["behaviors"][0]["name"]`, `json["renderpass"] == "OpaqueNoCull"` etc), and a parity test that every public method name on `ExBlockDef` that applies to items exists on `ExItemDef` (explicit allow-list of block-only names: `CollisionBox`, `SelectionBox`, `Lighting`, `Sounds`, `FillerOffsets`, `Drops`, `Material`, `Replaceable`, `Fertility`, `Resistance`, `Climbable`, `RainPermeable`, `SideOpaque`, `SideSolid`, `SideAo`, `EmitSideAo`, `FaceCullMode`, `LiquidLevel`, `RandomDrawOffset`, `RandomizeAxes`, `RandomizeRotations`, `VertexFlags`, `Frostable`, `HeldTpUseAnimation` and whatever else the implementer finds is block-only).
- [ ] **Step 2-4:** implement, gate, goldens unchanged.

### Task C2: the JSON megablock

**Files:**
- Modify: `mods/exlib/src/Blocks/Structures/BlockFilledMegastructure.cs` (add
  `[BlockRegister("ExFilledMegastructure", PrefixModId = false)]`), create
  `mods/exlib/src/Blocks/Structures/BlockEntityMultiblock.cs`
  (`[BlockEntityRegister("ExMultiblock", PrefixModId = false)]`, concrete subclass of
  `BlockEntityMultiblockStructure`).
- Test: `mods/exlib/tests/Structures/JsonMultiblockTests.cs`.
- Wiki: `mods/exlib/wiki/Multiblock-Structures.md` new section "From JSON only", replacing the
  attribute-generator paragraph.

**Interfaces:**
- `BlockEntityMultiblock` implements the three abstract members from block attributes:
  `UpdateStructureRotation` reads the block's `side` or `rot` variant through `ExOrientation` (the
  same rule `BlockBehaviorExOrientable` applies); `GetIncompleteMessage`/`GetCompleteMessage` return
  `Lang.Get("<domain>:multiblock-<blockpath>-incomplete", missingCount)` and
  `...-complete`, falling back to the exlib keys `exlib:multiblock-incomplete` / `exlib:multiblock-complete`
  (added to `en.json` and locales) when the domain has none.
- A JSON blocktype with `"class": "ExFilledMegastructure"`, `"entityClass": "ExMultiblock"`,
  `"behaviors": [{"name": "MultiblockStructure", ...}]` and `"attributes": {"fillerOffsets": ...,
  "layout": ...}` gets fillers, completion monitoring and the messages with no C#.

- [ ] **Step 1: test** with `TestWorld`: configure a bare `BlockFilledMegastructure` with the attributes of a 2x1x1 layout built from `ExBlockDef` (to reuse the emitter), place it, tick, assert `StructureComplete` after the second cell is placed and the lang key requested for the incomplete message.
- [ ] **Step 2-4:** implement, gate. `ReleasedCodes`-style guard: two new registered class strings, listed in CHANGELOG.

- [ ] **Stage C gate:** `bash scripts/exmod.sh test all`, `format -Check`.

---

## Stage D - the adoption layer

### Task D1: `ExMods`

**Files:**
- Create: `mods/exlib/src/Registries/ExMods.cs`, `mods/exlib/src/Registries/ExModsModSystem.cs`
- Test: `mods/exlib/tests/Registries/ExModsTests.cs`
- Wiki: `mods/exlib/wiki/Registries.md` new section "Other mods".

**Interfaces:**
```csharp
namespace ExpandedLib.Registries;
public static class ExMods {
  /// True when the mod id is loaded and enabled. Never throws; false for null or blank.
  public static bool IsLoaded(ICoreAPI api, string modId);
  /// The loaded mod's version, or null when absent.
  public static string? Version(ICoreAPI api, string modId);
  /// True when the loaded version satisfies the floor, false when the mod is absent or older.
  public static bool AtLeast(ICoreAPI api, string modId, string minimumVersion);
  /// Runs the action now when the mod is loaded, otherwise never. Returns whether it ran.
  public static bool WhenLoaded(ICoreAPI api, string modId, Action action);
  /// The world-config flag exlib sets for every loaded mod: "exlib:mod:<modid>" = true, usable in a
  /// JSON patch "condition": { "when": "exlib:mod:toolsmith", "isValue": "true" }.
  public static string FlagKey(string modId);
}
```
`ExModsModSystem` (`ExecuteOrder` 0.0, `StartPre`) sets `api.World.Config.SetBool(FlagKey(id), true)`
on the server for every enabled mod, before the patch loader's 0.05. The implementer verifies with
the vendored `.compat/Vintagestory/vsessentialsmod` patch loader that `World.Config` is readable at
that point on both sides; if the client receives world config later, the flags are set in `Start`
too, and the test asserts both.

- [ ] **Step 1: tests** with a substituted `IModLoader` (`IsModEnabled`, `GetMod`) and a `World.Config` bag: `IsLoaded`, `Version`, `AtLeast` (semver compare including `-rc.1`), `WhenLoaded`, `FlagKey` format, flags set for each mod.
- [ ] **Step 2-4:** implement, gate, wiki.

### Task D2: `ExHarmony`

**Files:**
- Create: `mods/exlib/src/Registries/ExHarmony.cs`
- Modify: `mods/exlib/src/ExpandedLibModSystem.cs`, `mods/iiex/src/IronIndustryExpandedModSystem.cs`,
  `mods/siex/src/SteelIndustryExpandedModSystem.cs` (replace the copied bootstrap).
- Test: `mods/exlib/tests/Registries/ExHarmonyTests.cs`
- Wiki: `Registries.md` section "Harmony".

**Interfaces:**
```csharp
namespace ExpandedLib.Registries;
public static class ExHarmony {
  /// Patches every [HarmonyPatch] class in the assembly under the mod id, once per process however
  /// many times it is called; returns the instance for Unpatch.
  public static Harmony PatchOnce(Mod mod, Assembly assembly);
  /// Applies only the classes carrying [HarmonyPatchCategory(category)] when the named mod is
  /// loaded; returns whether it patched.
  public static bool PatchCategoryWhenLoaded(ICoreAPI api, Harmony harmony, Assembly assembly,
    string category, string requiredModId);
  /// Unpatches everything registered under the mod id; safe to call twice.
  public static void UnpatchAll(Mod mod);
}
```

- [ ] **Step 1: tests**: a test patch class in the test assembly under a unique category; `PatchOnce` twice yields one patch (`Harmony.GetPatchInfo`); `PatchCategoryWhenLoaded` false when the mod is absent, true and applied when present; `UnpatchAll` removes.
- [ ] **Step 2-4:** implement, replace the three bootstraps, gate.

### Task D3: config to the client

**Files:**
- Create: `mods/exlib/src/Registries/Config/ExConfigSyncModSystem.cs`,
  `mods/exlib/src/Registries/Config/ConfigSyncPacket.cs`
- Modify: `mods/exlib/src/Registries/Config/IExConfigAccess.cs` (add
  `string ExportJson(); void ImportJson(string json);`), `ExConfigRegister.cs` (implement; after
  `ImportJson` the generated accessor reads the new values because it reads through `Config`),
  `mods/exlib/src/Commands/ConfigSubCommand.cs` (after a successful `Set`, rebroadcast),
  `mods/exlib/generators/ExConfigGenerator.cs` only if the accessor caches values (the implementer
  checks; if it copies into static fields, add a `Refresh()` the sync calls).
- Test: `mods/exlib/tests/Config/ConfigSyncTests.cs`
- Wiki: `Config-System.md` section "What the client sees".

**Behaviour:** channel `exlib.config`; on `PlayerJoin` the server sends one packet per registered
section `{ModId, FileName, Json}`; the client `ImportJson`s into the matching register and logs one
Notification per section; a `/exmod config set` broadcasts the changed section to all players.
Single player: the client import overwrites the same values it loaded. The client never writes the
imported values to its own file.

- [ ] **Step 1: tests**: `ExportJson` round-trips through `ImportJson` on a fresh register; import of an unknown section is ignored with a Warning; a range-violating imported value is clamped the same way `Load` clamps.
- [ ] **Step 2-4:** implement, gate.

### Task D4: checks in the game

**Files:**
- Create: `mods/exlib/src/Checks/ICheckSource.cs`, `AssetCheckSource.cs`, `ExlibChecks.cs`,
  `CheckResult.cs`, and one file per moved check: `DefinitionCatalogueCheck.cs`,
  `MultiblockCodesCheck.cs`, `RecipeCodesCheck.cs`, `LangCoverageCheck.cs`,
  `NetworkNodeContractCheck.cs`, `PinnedNetworkNodesCheck.cs`, `CodePrefixCollisionCheck.cs`;
  `mods/exlib/src/Commands/VerifySubCommand.cs`.
- Modify: the seven validators in `mods/exlib/testing/` become thin wrappers over the `Checks`
  classes with a `RepoCheckSource(root, domain)` (created in `mods/exlib/testing/RepoCheckSource.cs`);
  `mods/exlib/src/ExpandedLibModSystem.cs` (`AssetsFinalize` runs `ExlibChecks.All` unless
  `ExlibConfig.RunChecksOnLoad` is false); `ExlibConfig.cs` (the flag, default true).
- Test: `mods/exlib/tests/Checks/ExlibChecksTests.cs`; the existing validator tests keep passing.
- Wiki: new `Checks.md`; `Commands.md` gets `/exmod verify`.

**Interfaces:**
```csharp
namespace ExpandedLib.Checks;
public interface ICheckSource {
  IEnumerable<string> Domains { get; }
  IEnumerable<AssetLocation> BlockCodes { get; }     // every registered block code
  IEnumerable<AssetLocation> ItemCodes { get; }
  IEnumerable<(AssetLocation File, JObject Json)> Recipes(string domain);
  IEnumerable<(string Locale, JObject Json)> Lang(string domain);
  IEnumerable<ExBlockDef> BlockDefinitions(string domain);
}
public sealed record CheckResult(string Check, string Domain, IReadOnlyList<string> Errors);
public static class ExlibChecks {
  public static IReadOnlyList<CheckResult> All(ICheckSource source);
  public static IReadOnlyList<CheckResult> All(ICoreAPI api);   // AssetCheckSource
  public static void Log(ILogger logger, IReadOnlyList<CheckResult> results);
}
```
`AssetCheckSource` reads codes from `api.World.Blocks/Items` and files from `api.Assets`.
`/exmod verify [domain]` runs `All` and prints the counts plus the first ten errors.

- [ ] **Step 1: tests**: an `ICheckSource` stub with one dangling recipe code, one missing lang key, one multiblock cell naming an unknown block; `All` returns exactly those three errors; `Log` writes one line per check.
- [ ] **Step 2-4:** move, wrap, implement the command, gate (`bash scripts/exmod.sh test latest` must show the validator tests unchanged in count).

### Task D5: `ExBlockState` in the machine bases

**Files:**
- Modify: `mods/exlib/src/Blocks/Machines/BlockEntityProductionMachine.cs`,
  `mods/exlib/src/Blocks/Structures/BlockEntityMultiblockStructure.cs` (and therefore
  `BlockEntityMultiblockMachine`), `mods/exlib/src/Blocks/Networks/BlockEntityNetworkNode.cs`,
  `mods/exlib/src/Blocks/Machines/BlockEntityMachineStation.cs`: each gains the `State` property and
  the four override pairs exactly as `ExBlockEntity` has them, with a `protected virtual void DeclareState(ExBlockState state) {}` default so existing subclasses compile unchanged.
- Modify: `mods/exlib/src/Blocks/ExBlockState.cs`: add
  `ExBlockState Tree(string key, Action<ITreeAttribute> write, Action<ITreeAttribute, IWorldAccessor> read)`
  for nested trees (the `MoltenCharge` case) and `ExBlockState Pos(string key, ...)` already exists.
- Test: `mods/exlib/tests/Machines/ProductionMachineStateTests.cs`, `mods/exlib/tests/Blocks/ExBlockStateTests.cs` (the `Tree` primitive).

- [ ] **Step 1: tests**: a `TestProductionMachine` declaring one float through `State` round-trips through `ToTreeAttributes`/`FromTreeAttributes`; a subclass that declares nothing still round-trips its base fields; `Tree` writes and reads a nested tree.
- [ ] **Step 2-4:** implement, gate. The conversion of the family's 46 block entities is the convenience plan's Task V1.

- [ ] **Stage D gate:** `bash scripts/exmod.sh test all`, `format -Check`.

---

## Stage E - structural

### Task E1: one loader shape and the naming law

**Files:**
- Create: `mods/exlib/src/Registries/ContributedCatalogueLoader.cs`
- Modify: `ProcessRouteLoader.cs`, `ProcessJobLoader.cs`, `BayOccupancyLoader.cs` (derive; keep
  their public `Load(ICoreAPI)` names), `ProcessRouteRegistry.cs`, `ProcessJobRegistry.cs`,
  `BayOccupancyRegistry.cs`, `MaterialRoleRegistry.cs`, `MetalRegistry.cs`, `ExLiquids.cs`
  (all keyed through `ExKeyedRegistry<T>`; verbs: `Register` declares from code, `Contribute`
  merges a parsed set, `Load` reads assets into the registry, `Clear` empties);
  `docs/design/conventions.md` (the naming law, five lines).
- Test: existing loader tests; `mods/exlib/tests/Invariants/CatalogueNamingTests.cs` (reflection:
  every type named `*Registry` in exlib exposes `Clear`, `Contributors`, and no member named
  `Add*` or `Load*`).

- [ ] **Step 1: the guard test**, failing.
- [ ] **Step 2-4:** refactor behind the existing tests, gate.

### Task E2: the boundary re-check

- [ ] `ExBlockDef.MineTool` writes `mineTool`, a root key the loader does not read (found by the
  known-key warning in the sample's smoke run): find what the family's blocks meant by it (a mining
  tool requirement or a per-tool mining speed - read the callers and vanilla's `BlockType`), emit the
  key the game reads, mark the old method `[Obsolete]` if its name misleads, rebless the affected
  goldens on purpose, and make the sample's smoke run warning-free.
- [ ] A from-scratch build is warning-free: `dotnet build VintageStory.sln --no-incremental` shows
  zero warnings (incremental builds hide about forty nullable and cref warnings, some predating this
  work, some added by it; fix them all so the next stranger's build is silent).
- [ ] After every other task: `bash scripts/exmod.sh test latest -Filter PublicSurface` green, `Supported-API.md` lists every new public type from Stages B to E (`CatalogueLoadReport`, `KnownRootKeys`, `CatalogueContributors`, `ExMods`, `ExHarmony`, `ICheckSource`, `CheckResult`, `ExlibChecks`, `BlockEntityMultiblock`, `CellRole`, `ContributedCatalogueLoader`), CHANGELOG complete.

---

## Stage F - documentation to a visible first result (docs tasks; may run alongside code)

### Task F1: fix what is wrong or stale

**Files:** `mods/exlib/wiki/Multiblock-Structures.md` (the attribute-generator paragraph becomes the
`ExBlockDef.FillerOffsets` code-first route plus the JSON route from Task C2),
`mods/exlib/src/Blocks/Structures/IFillerHost.cs` (its comment), `mods/exlib/docs/moddb.html`
(names and ids `iiex`, `siex`), `mods/exlib/wiki/Getting-Started.md` (dependency version `0.7.3`,
csproj path `..\..\exlib\src\ExpandedLib.csproj`), `Testing-Harness.md` (paths
`..\src\ExpandedLib.csproj`, `..\testing\ExpandedLib.Testing.csproj`), `Home.md` (mod links to
the ModDB pages or removed).

- [ ] Apply; `bash scripts/exmod.sh test latest -Filter WikiParity` green.

### Task F2: `Code-First-Definitions.md` and `Lifecycle.md`

- [ ] `Code-First-Definitions.md`: `IExBlockDefProvider`, `ExBlockDef`, `ExItemDef`, `ExRecipeDef`,
  the layout DSL, `RootKey` and the known-key warning, goldens, the duplicate-location rule and its
  notification; every snippet compiles against the current builders (WikiParity checks class lines).
- [ ] `Lifecycle.md`: one table, rows `StartPre`, `Start`, `AssetsLoaded 0.04 (definitions injected,
  server)`, `0.05 (patches)`, `AssetsFinalize (catalogues cleared, loaded, contributors invoked,
  checks run)`, `StartServerSide`, `StartClientSide`, `PlayerJoin (config sync)`; columns "what exlib
  has done by then" and "what you may call".
- [ ] `_Sidebar.md` and `Home.md` link both.

### Task F3: Getting-Started to a placeable block, and `samples/HelloExpanded`

**Files:** `mods/exlib/wiki/Getting-Started.md` (sections 4 to 6: a definition provider for one
block with a shape from the game's own assets, a lang entry, a creative-tab entry, the first launch,
the log lines to expect from Task B1), `samples/HelloExpanded/HelloExpanded.csproj` (references
exlib like a third party: `HintPath` to the built dll), `samples/HelloExpanded/src/HelloExpandedModSystem.cs`,
`BlockHello.cs` (`[BlockRegister]`, `IExBlockDefProvider`), `BlockEntityHello.cs`
(`BlockEntityProductionMachine` counting ticks into `State`), `HelloConfig.cs` (`[ExConfigRegister]`,
one value), `assets/helloexpanded/lang/en.json`, `modinfo.json`; `VintageStory.sln` (solution folder
`samples`); `.github/workflows/tests.yml` (build the sample).

- [ ] Build clean; the sample boots in the smoke lane (testing plan Task T6) with zero errors.

- [ ] **Stage F gate:** `bash scripts/exmod.sh test latest -Filter "WikiParity|LangParity|HandbookParity"`.

---

## Order and parallelism

A1 -> A2 -> A3 -> A4 (contract; reviewer once). Then B1..B5 in any order, one at a time, with F1 and
F2 alongside. Then G1, G2 (the tree), H1. Then H2, C2 (its JSON layout rides the grid core), C1, H3 with D5, D1..D4,
H4..H6 one at a time, F3 alongside after C2. Then E1 (with the audit corrections), E2. Stage gates run
`test all` (the legacy lanes need `.NET 7 and 8` runtimes, which `scripts/exmod.ps1` provisions into
`.dotnet/`).
