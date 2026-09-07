# Changelog - Expanded Library (`exlib`)

All notable changes to this mod are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/), and the project follows
[Semantic Versioning](https://semver.org/). For changes before this file existed,
see the git history.

## [Unreleased]

### Added

- **Modules**, exlib's extension mechanism: `[assembly: ExModule("<id>")]` marks an assembly as a
  module, driven through the lifecycle of the mod named in `Host` (default `exlib`) instead of
  carrying a `ModSystem` of its own, ordered against the rest of its host's modules by `Requires`.
  `ExModules.For(api, host)` discovers and orders a host's modules, keeping only the ones whose
  shipping mod (`Mod`) is enabled; `ExModuleHost` owns one driver instance's entry-point instances
  and runs them, and their assembly's registries, through the same phases and order of operations
  as `ExModSystem` runs for a main assembly. `PatchHarmony` opts a module into Harmony patching
  under its own `<host>.<id>` id. `ExModules.IsLoaded(api, id)` and the `exlib:module:<id>`
  world-config flag let another mod or a JSON patch condition gate on a module the way
  `ExMods`/`exlib:mod:<id>` already do for a mod. `IExDefinitionContributor` runs at
  `AssetsLoaded` 0.04, right before injection, regardless of host order, so a module (or a main
  assembly) can register code-first definitions built from assets that are only readable once
  every mod's `Start` has run - Industry's metal-family emission moved here from its own
  `AssetsLoaded`. `ExpandedLib.Industry` is the first module, shipped inside the exlib mod folder
  (`exlib.industry.dll`); `samples/HelloModule` proves the third-party form, a module shipped as
  its own mod depending on exlib. See the wiki's [Modules](wiki/Modules.md) page.
- `Catalogues/AssetCatalogueLoader.cs` is public. It reads every domain's
  `config/<yours>/*.json` under a path prefix into a typed object and reports what failed to
  parse - the primitive `ContributedCatalogueLoader<TSet, TRegistry>` is built on. Both of those
  were already supported API while the loader under them was not, so a mod could derive the
  contributor base but not read a catalogue of its own.
- `ExBlockNames.AddVariantQualifier(variantGroup, langPrefix)`: the block-name decorator handles
  `material`, `rock` and `brick` itself, and any other variant group is now registered by the mod
  that owns it rather than hardcoded. `ExBlockNames` moved from the family layer to
  `ExpandedLib.Helpers` with it - nothing about it was family-specific except the one clause.
- `Registries/ExModSystem.cs`: an abstract `ModSystem` base that runs `ExConfig.LoadAll`,
  `EntityRegistry.RegisterAll`, `CommandRegistry.RegisterAll` and `PreferenceRegistry.RegisterAll`
  in the right phase and order for you (preferences before commands on the client), with an
  overridable, empty-by-default hook after each; `PatchHarmony` folds `ExHarmony.PatchOnce`/
  `UnpatchAll` in too. `Config/ExConfig.cs`'s `LoadAll(api, assembly)` finds every generated config
  accessor in an assembly by the new `Config/ExConfigAccessorAttribute.cs`, which
  `ExConfigGenerator` now stamps on every accessor it emits. `samples/HelloExpanded`'s mod system
  is now an empty class deriving `ExModSystem`.
- `TestWorld.LoadAssets(modPath, gamePath?)`: drives the game's own `AssetManager` and
  `ModRegistryObjectTypeLoader` against a mod's real assets and compiled classes, registering the
  resulting `Block`/`Item` instances (real, variant-resolved) into the `TestWorld`. Covers JSON and
  code-first mods alike, base `game` domain assets excepted (see
  `docs/internal/research/2026-09-06-asset-loading-spike.md`); one test on the HelloExpanded sample
  in `mods/exlib/tests/Harness/AssetLoadingTests.cs`, wiki section "Real assets" in
  `mods/exlib/wiki/Testing-Harness.md`.
- `exlib-verify` (`infra/tools/ExlibVerify`, packed as the `ExpandedLib.Verify` .NET tool): checks
  a JSON-only mod folder or zip without the game running - every asset under `assets/` parses,
  every `patches/*.json` op applies against its real target (run through the game's own
  `Tavis.JsonPatch` engine), every `config/handbook/*.json` lang key resolves, every recipe
  ingredient/output code resolves against the mod, the game, and any `--mods`. Never a false
  error: a `dependsOn`/`condition` this run cannot evaluate, a `loadFromProperties` variant group
  it cannot expand, or a code in an unloaded domain is reported informationally instead. See
  `mods/exlib/wiki/Checks.md`, "Without the game: exlib-verify".
- `Blocks/ExBlockEntityBehavior.cs` and `Blocks/ExBlockEntityContainer.cs`: the `ExBlockEntity`
  `[Persist]`/`Persisted` convenience for a `BlockEntityBehavior` and a `BlockEntityContainer`
  respectively, for a block entity whose base slot is already spent. `ExpandedLib.Testing.TreeKeys`
  gains `AssertDeclaresBaseKeys`, a guard for a subclass `DeclareState` override that skips its own
  `base.DeclareState(state)` call - a golden alone does not catch it, since `PersistScan`'s
  `[Persist]` scan always contributes its keys regardless of what the override chain does.
- `dotnet new exlib-tests` (`templates/exlib-tests/`, identity `ExpandedLib.Templates.Tests`): a
  headless test project scaffold - one `TestWorld` block test, one code-first-definition golden
  fact, one shipped-JSON-parses fact, all passing vacuously until the mod they're generated
  alongside has content for them to check.
- `.github/workflows/release.yml`: on a `v*` tag, runs the test gate, the Cake `Package` and
  `PackageTesting` tasks, and `dotnet pack` for `ExpandedLib` and `ExpandedLib.Testing`, then
  uploads the mod zips, the dev bundle and both nupkgs as GitHub release assets. `templates/ci/tests.yml`
  joins the existing `templates/ci/smoke.yml` for a third-party repository.
- `ExpandedLib.csproj` and `ExpandedLib.Testing.csproj` are packable: `PackageId`, `Version` read
  from `modinfo.json`, `Authors`, `Description`, `RepositoryUrl`, `PackageReadmeFile`,
  `PackageLicenseExpression`. Nothing is pushed to NuGet.org by this repo yet - see `release.yml`'s
  commented push step.
- `LICENSE`: the repository is MIT licensed. `PackageLicenseExpression` is set to MIT on
  `ExpandedLib`, `ExpandedLib.Testing` and `ExpandedLib.Verify`, and every packaged mod zip and
  the developer bundle now carry a copy as `LICENSE.txt`.
- The testing docs are rewritten as a whole for the current shape (four test projects, the
  template, the release workflow): `docs/internal/testing.md`, the wiki's `Testing-Harness.md` and
  `Testing-API-Reference.md`, and a new source-tree guard, `HarnessSurfaceTests`, that fails when a
  public harness type or a `Testing-API-Reference.md` table entry drifts from the other.
- `exlib.testing`-adjacent internal test seams (not part of the public API - iiex's and siex's own
  suites are the only callers): `DriveProductionTick`/`DriveIdleTick` on
  `BlockEntityProductionMachine`/`BEBehaviorProductionMachine`, `DriveMonitorTick`/
  `ApplyStructureRotation` on `BlockEntityMultiblockStructure`, and `SetNetworkTypeForTest`/
  `ApplyOrientationForTest` on `BlockNetworkNode`, replacing a `ReflectionHelpers` call at each of the
  family's own call sites with a compile-checked one.
- `samples/HelloExpanded`: a buildable, bootable, tested third-party mod using the convenience
  layer end to end (a code-first block, `[Persist]` state, `ExInteraction`/`ExInfo`, a config value,
  a command, two headless tests). The wiki's [Getting Started](wiki/Getting-Started.md) walk is
  rewritten from section 4 onward to read alongside it, copying every snippet from the sample so it
  compiles.
- `exlib.testing` gains `Rigs.MachineRig` (drive one machine to a condition: `RunUntil`, `RunLive`,
  `RunWhile`), `Rigs.RegistryLawScanner` (a law that must hold across every concrete subclass of a
  base type, in the whole loaded assembly closure), `Rigs.ResourceInvariant<TState>` (a randomised-
  operation invariant, reporting the failing sequence and seed), and `Rigs.StaticStateCollection`
  (`EveryCollectionNameHasADefinition` catches a bare xUnit `[Collection("...")]` name with no
  `[CollectionDefinition(...)]`, which otherwise silently stops serializing anything). See
  [Testing Harness](wiki/Testing-Harness.md).
- `exlib.testing` gains six supported doubles wired straight into `TestWorld`, so a first inventory,
  config or logging test needs no NSubstitute knowledge: `Doubles.TestPlayer` (a real hotbar slot
  behind a substituted `IPlayer`/`IServerPlayer`), `Doubles.TestInventory` (a real multi-slot
  `InventoryGeneric`), `Doubles.TestModLoader` (`Api.ModLoader`, with `IsModEnabled` and the
  `IsModLoaded`/`HasMod`/`HasModId` aliases some mods probe by reflection), `Doubles.WorldConfigBag`
  (`World.Config`'s real tree), `Doubles.ModConfigFiles` (real files under a temp directory backing
  `Api.LoadModConfig`/`StoreModConfig`), `Doubles.RecordingLogger` (`Api.Logger`/`World.Logger`,
  queryable as `Entries`/`Errors`/`Warnings` instead of NSubstitute's `Received()`). `TestWorld` gains
  `Player()`, `Log`, `Config`, `Mods`, `ConfigFiles` and is now `IDisposable`. See
  [Testing Harness](wiki/Testing-Harness.md) "Doubles".
- `exlib.testing` gains `Rigs.HarmonyFixture` (applies a mod's Harmony patches once and reverts them
  on dispose, built on `ExHarmony` so the fixture and the library agree on idempotence) and
  `Doubles.TestChannels` (a client/server channel pair that round-trips a packet through the real
  `SerializerUtil` and delivers it synchronously to the other side's handler; `TestWorld.Channels`
  memoises one per name and is what `Api.Network.RegisterChannel`/`ClientApi.Network.RegisterChannel`
  now hand out, so a `ModSystem` that registers a channel needs no test-only wiring). `TestWorld`
  gains `ClientApi` for exercising `StartClientSide`. See [Testing Harness](wiki/Testing-Harness.md)
  "Testing Harmony patches" and "Testing packets".
- `RepoPaths.Register(domain, modFolder)`: a domain whose zip ships from a different mod's folder
  can now be declared instead of hand-edited into the harness; an unregistered domain falls back to
  `mods/<domain>` rather than throwing. `Repo.ReleasedHistory`: the released-code/version/debt
  registry each mod's own test `ModuleInit` now feeds, so the harness itself carries no mod's
  shipping history; `ReleasedCodes`/`ReleasedVersions`/`ReleasedCodeDebt` are unchanged forwarders
  onto it. See [Testing Harness](wiki/Testing-Harness.md) "Repo paths and released-code history".
- `exmod codes <mod>`: regenerates `{Mod}Blocks.g.cs` through the standalone
  `infra/tools/BlockCodeEmitter` console tool - builds the mod, writes the table, rebuilds.
- `exmod smoke [-Version <x.y>] [-Mods <dir>[,<dir>...]] [-Timeout 180] [-KeepData]`: the smoke lane
  - boots the real dedicated server against every built mod (or the given ones), runs
  `/exmod verify`, and fails on a boot timeout, an `[Error]`/`[Fatal]` log line or a non-clean verify
  summary, printing the offending lines. Runs on an unusual fixed port (42499) so it never disturbs a
  game already running on the same machine. CI runs it in its own job after the test job; see
  `templates/ci/smoke.yml` for a mod outside this repo. `exmod provision game -Kind server` on Linux
  and macOS now redirects to `.game/<slug>-server` instead of overwriting a client install left over
  at `.game/<slug>` for another platform, and `-Dest <absolute path>` is used as given instead of
  being joined onto the repo root. See [Testing Harness](wiki/Testing-Harness.md) "The smoke lane".
- `exlib.testing`'s soft untested-public-surface report (`PublicSurfaceTests`) fell from 64 to 42:
  behaviour tests for the command registry and its sub-commands, preferences, recipe/config profiles,
  the right-click-construction wiring, block/item code migrations and removals, the legacy shims, and
  the handbook unit-conversion patch. See [Testing Harness](wiki/Testing-Harness.md) "Examples".

- ⛔ **Breaking (`exlib.testing`, the dev-only harness bundle): `BlockCodeEmitter` no longer writes
  from inside a test run.** `EXLIB_WRITE_BLOCKCODES=1 dotnet test <project>` is gone; regenerate a
  mod's `{Mod}Blocks.g.cs` with `exmod codes <mod>` instead. `CheckOrWrite` only compares now.

- `ExpandedLib.Testing.TreeKeys`: golden-checks a block entity's `ToTreeAttributes` key set, the
  save-format proof behind converting a hand-written pair to `[Persist]`/`Persisted` (see
  [Block Entities](wiki/Block-Entities.md) § Converting a hand-written pair).
- `ExpandedLib.Checks`: the seven content guards that used to run only in the xUnit harness
  (dangling recipe/multiblock codes, missing lang coverage, pinned network nodes, a network node or
  membership missing part of its contract, a base-code prefix collision), now runnable against the
  live game through `ExlibChecks.All(ICoreAPI)`/`AssetCheckSource`, or against a custom
  `ICheckSource`. `ExpandedLibModSystem.AssetsFinalize` runs them after the catalogues load and logs
  the results (`ExlibConfig.RunChecksOnLoad`, default on); `/exmod verify [mod]` runs them on
  demand. See [Checks](wiki/Checks.md).
- `IExConfigAccess` gains `ExportJson`/`ImportJson`, and `ExConfigSyncModSystem` carries every
  `Manageable` config's live values from the host to each joining client and after a live
  `/exmod config set`, so a client's display, handbook and predictions agree with the server's
  tunables instead of its own local file. An import never writes to the client's config file. See
  [Config-System](wiki/Config-System.md) "What the client sees".
- `Registries.ExMods`: `IsLoaded`, `Version`, `AtLeast` (game-standard semver compare) and
  `WhenLoaded` for reacting to another mod being installed, plus `FlagKey(modId)` - the
  `"exlib:mod:<modid>"` world-config flag `ExModsModSystem` sets for every enabled mod, so a JSON
  patch's `condition` can gate on another mod with no C# code. `Registries.ExHarmony`:
  `PatchOnce(mod, assembly)` applies an assembly's uncategorised `[HarmonyPatch]` classes once per
  process, `PatchCategoryWhenLoaded` applies a `[HarmonyPatchCategory]` group only when a required
  mod is loaded, and `UnpatchAll(mod)` tears down both. Replaces the copied Harmony bootstrap in
  exlib, iiex and siex. See [Registries](wiki/Registries.md) "Other mods" and "Harmony".
- `BlockEntityProductionMachine`, `BlockEntityMultiblockStructure`, `BlockEntityNetworkNode` and
  `BlockEntityMachineStation` each gain the `Persisted`/`DeclareState` pair `ExBlockEntity` already
  had, layered on top of whatever keys they already write by hand - a machine, multiblock, network
  node or station now declares extra saved fields the same way. `ExBlockState` gains `Tree(key,
  write, read)` for a value that manages its own multi-attribute or nested-tree serialization (a
  `MoltenCharge`, for instance) against the same tree every other field writes into.
- `[Persist]` (`ExpandedLib.Blocks.PersistAttribute`): mark a field or auto-property of a block
  entity and it is saved with no `DeclareState` entry at all. Supports `bool`, `int`, `long`,
  `float`, `double`, `string`, an enum (stored as its underlying `int`), `BlockPos`, `ItemStack`,
  and `IPersistable` - a value type that writes itself into a nested tree. `Legacy` names an older
  key, read only when the new one is absent and never written back. See
  [Block-Entities](wiki/Block-Entities.md).
- `ExRightClickConstructable` publishes `IProductionReadiness`: `IsReadyToProduce` is `IsComplete`
  and `StopsProductionWhenNotReady` is `true`, so a machine that carries the behaviour and hosts a
  production tick waits for construction with no gate to write by hand. `GatesProduction`
  (JSON `gatesProduction`, default `true`) opts a machine that must keep ticking while unfinished
  out of the gate; the definition builder's `Construction(...)` DSL gains a matching
  `GatesProduction(bool)` step. See [Construction](wiki/Construction.md) "Construction gates
  production".

- Every catalogue loader's `Load(ICoreAPI)` (metals, liquids, material roles, process routes,
  process jobs, bay occupancy) now returns a `Catalogues.CatalogueLoadReport`: files read,
  entries accepted, and the errors, each naming its asset. `AssetsFinalize` logs one summary
  line per catalogue plus one `Error` line per failure, instead of the bare per-error loop it
  had before. See [Extending-Processes](wiki/Extending-Processes.md) "What the log tells you".
- Every catalogue registry exposes a static `Contributors` (`Catalogues.CatalogueContributors`):
  register a code contribution once from `Start` and it is re-applied after every JSON read, so
  it survives the clear that precedes each `AssetsFinalize`. `MaterialRoleRegistry.RegisterContributor`
  /`ClearContributors` now forward to it. See [Extending-Processes](wiki/Extending-Processes.md)
  "From C#, and surviving the next load".
- `Definitions.KnownRootKeys`: the top-level keys the game's object loader reads for a block or
  item type, taken by reflection from the loader's own types. `ExDefinitionModSystem` now logs a
  Warning for every registered def's root key that is not one of them, naming the def and the key,
  instead of writing a mistyped key into the JSON with nothing to say it is never read.
- `MultiblockLayoutBuilder.Core(char)` marks the anchor glyph; `Build()` then throws naming the
  layout, the declared `Origin` and where the anchor actually landed when `Origin` is not its
  negation, instead of building a structure silently offset from the block the player placed.
  Optional - a layout that never calls `Core` is unchecked, as before.
- `ExDefinitions` gains a static `Logger`; re-registering an asset location from a different
  assembly than last time now logs a Notification naming the location and both assemblies, instead
  of replacing silently.
- `StructureFillers.CanPlace`/`PlaceFillers`/`RemoveFillers` log an Error, once per process, naming
  `FillerCode` when it fails to resolve to a registered block, instead of quietly refusing to place
  or remove anything.
- `EntityRegistry`'s cross-mod `Class<T>()`/`Behavior<T>()` domain fallback (an assembly with no
  `[assembly: ExDomain]` that was never registered) now logs a Warning naming the assembly, instead
  of silently resolving into the caller's own domain.
- `Structures.CellGrid`/`GridPlane`/`GridOptions` and `Structures.SymbolLegend<T>`: the grid-and-legend
  core the multiblock, filler and scene-diagram ASCII DSLs now all draw over. `MultiblockLayoutBuilder`
  gains `Slice(int x, string grid)` and `Face(int z, string grid)`, matching the elevation grids the
  filler layout already had; a layout may mix `Layer`/`Slice`/`Face` freely. See
  [Multiblock-Structures](wiki/Multiblock-Structures.md) "One grid, three uses".
- `exlib.testing` gains `Checks.LayoutTable.From(ExBlockDef)`/`.Rotated(def, angle)`, reading a
  code-first multiblock layout's emitted table back into a per-cell block code, and `Scenes.SceneGrid`,
  the grid-core derivation `SceneDiagram` now forwards to.
- A JSON-only megablock, no C# required: `BlockFilledMegastructure` is now registered as
  `ExFilledMegastructure` (previously an abstract base only), and `Structures.BlockEntityMultiblock`
  (registered `ExMultiblock`) is a concrete `BlockEntityMultiblockStructure` reading orientation from
  the block's own `side`/`orientation` variant and its incomplete/complete messages from
  `<domain>:multiblock-<blockpath>-incomplete`/`-complete`, falling back to the new
  `exlib:multiblock-incomplete`/`-complete` lang keys. `Structures.JsonMultiblockLayout` resolves a
  block's `attributes.multiblockLayout` ASCII grid - the JSON twin of `MultiblockLayoutBuilder` - into
  the same `multiblockStructure` and, absent an explicit `fillerOffsets`, the same derived footprint a
  code-first definition would emit. See [Multiblock-Structures](wiki/Multiblock-Structures.md)
  "From JSON only".
- `ExItemDef` gains every `ExBlockDef` method whose JSON key also exists on an itemtype:
  `Behavior`/`Behavior(name, props)`/`Behavior<T>()`, `Handbook`/`HandbookExclude`, `SkipVariants`,
  `ShapeByType`, `TextureByType`, `AttributeByType`, `RootKeyByType`/`RawByType`, the codeless
  `VariantGroupFromProperties(path)` overload, and positional `GuiTransform`/`FpHandTransform`/
  `TpHandTransform`/`GroundTransform` overloads taking translation, rotation, origin and scale
  directly. `ExBlockDef.RenderPass`/`DrawType` also take `EnumChunkRenderPass`/`EnumDrawType`
  directly, and `FaceCullMode` takes `EnumFaceCullMode` - all three write the same string the
  existing string overload does. See [Code-First-Definitions](wiki/Code-First-Definitions.md).
- `Registries.ReflectionScan` gains `GetCandidateTypes(IEnumerable<Assembly>)` (a deterministic,
  cross-assembly candidate scan) and `ForEachAttributed<TAttr, TInstance>` (the shared
  find-attribute/activate/register loop); `CommandRegistry`, `PreferenceRegistry` and
  `BlockMigrationModSystem`'s discovery now route through them instead of each keeping its own copy.
- `Helpers.ExOrientation.SegmentedCode`: splits a block code's path on `-` so a caller can index,
  rewrite and rejoin one dash-segment at a time. `MultiblockLayoutBuilder`'s orientation-segment scan
  and `MultiblockFacings`' rotation now share it.
- `Helpers.ExMeshCache` gains `GetOrCreateRef`/`DisposeGroup` for a cache of uploaded GPU
  `MultiTextureMeshRef`s grouped for disposal together, and `Helpers.ExHighlightSlots.Reserve(key)`
  hands out a stable, distinct `world.HighlightBlocks` slot id per key instead of a hand-picked
  literal.
- `Registries.RegistrySubCommand<T>`: derive once for a `/exmod <name>` sub-command over a keyed
  registry (list every code, show one, hand the rest to your own `Set`). `ConfigSubCommand` and
  `RecipesSubCommand` now derive from it instead of each keeping its own list/show/set command
  loop. See [Commands](wiki/Commands.md) "Your own /exmod sub-command for a registry".
- `Catalogues.ContributedCatalogueLoader<TSet, TRegistry>`: derive once for a hand-parsed JSON
  catalogue with C# contributors (one asset path, an unknown-key audit, a merge that reports its
  clashes, contributors re-run after every reload). `ProcessRouteLoader`, `ProcessJobLoader` and
  `BayOccupancyLoader` are now this base with their own schema; each file is parsed exactly once
  (the report used to parse twice, once to count and once to merge) and a clash is always named
  after the file it actually came from, never the family/machine/store name a mismatched count used
  to fall back to once any earlier file in the batch failed to parse. See
  [Extending-Processes](wiki/Extending-Processes.md) "Adding a catalogue of your own".
- `docs/design/conventions.md` "Catalogue registry verbs": the naming law every catalogue registry
  follows (`Register`/`Contribute`/`Load`/`Clear`/`Contributors`), guarded by
  `CatalogueNamingTests`.
- `ExBlockAccess`: `BlockEntity<T>`/`TryGetBlockEntity<T>` for the `GetBlockEntity(pos) is X be`
  null-guard every machine wrote by hand, plus `Neighbour<T>`/`Neighbours<T>` for the one-step and
  around-a-position walks. See [Helpers-and-Renderers](wiki/Helpers-and-Renderers.md) "Finding
  block entities".
- `ExSide`: `IsServer`/`IsClient` over `ICoreAPI` and `IWorldAccessor`, for the
  `Api.Side == EnumAppSide.X` check every machine wrote by hand. See
  [Helpers-and-Renderers](wiki/Helpers-and-Renderers.md) "Which side".
- `ExInteraction.Of`/`Interaction`: reads what a click carried - held stack, tool, sneak, clicked
  face, side - without deciding which side acts on it. See
  [Helpers-and-Renderers](wiki/Helpers-and-Renderers.md) "Reading a click".
- `ExInfo`: `Lang`/`LangIf`/`Measure` extension methods on `StringBuilder` for a `GetBlockInfo`
  body's `Lang.Get` lines, including one that folds a value through `ExMeasure` for both display
  systems. See [Helpers-and-Renderers](wiki/Helpers-and-Renderers.md) "Block info lines".

### Changed

- The `ExpandedLib` package now carries the config and lang source generators (packed as analyzers
  under `analyzers/dotnet/cs/`) and the MSBuild plumbing behind `GamePath` resolution, provisioning,
  asset globs and version stamping (packed under `build/`), so a consumer references one package and
  needs no props of its own beyond a `TargetFramework`. Package versions for `ExpandedLib`,
  `ExpandedLib.Industry` and `ExpandedLib.Testing` are central, in `Directory.Packages.props` at the
  repo root, read once from `modinfo.json`.
- The framework no longer names its consumers' test assemblies: `InternalsVisibleTo` on `exlib.dll`
  grants only `ExpandedLib.Tests` and `ExpandedLib.Testing`. The internal seams `IronIndustryExpanded.Tests`
  and `SteelIndustryExpanded.Tests` reached directly now go through harness hooks
  (`MachineTestHooks`, `StructureTestHooks`, `NetworkNodeTestHooks` - see
  [Testing API Reference](wiki/Testing-API-Reference.md)) instead.
- ⛔ **Breaking: `ExpandedLib.Industry` is its own assembly and its own NuGet package.** The
  family's content layer - pipes, molten metal, mechanical power, metals, heat - now builds as
  `exlib.industry.dll` from `mods/exlib/industry/`, and ships beside `exlib.dll` inside the same
  exlib mod folder, so nothing changes for a player: one mod, one modinfo, one download. A mod
  that uses those types adds a reference to `ExpandedLib.Industry` alongside `ExpandedLib`; no
  namespace, type name or registered class key moved, so a save and a shipped blocktype JSON are
  unaffected. The framework assembly no longer references the domain layer in either direction,
  which the compiler now enforces and `IndustryBoundaryTests` proves.
- The metal catalogue and the generated metal item family are loaded by the domain layer's own
  `IndustryModule` rather than by exlib's mod systems calling into it, through the new companion
  assembly mechanism below. Its driver runs at ExecuteOrder 0.03, under `ExDefinitionModSystem`'s
  0.04 so the emitted items are registered before that system injects them, and under
  `ExpandedLibModSystem`'s default 0.1 so metals still load before liquids and material roles.
- `scripts/exmod.ps1` is split from one 900-line file into a dispatcher plus one file per
  lifecycle stage under `scripts/exmod/` (`provision.ps1`, `src.ps1`, `run.ps1`, `dist.ps1`,
  `windows.ps1`); each command registers itself next to its own implementation, so `exmod`
  prints a grouped command list and `exmod help <command>` prints one command in detail.
  `scripts/exmod.sh` is unchanged. See [Contributing](../../CONTRIBUTING.md) "Running things".
- ⛔ **Breaking: construction now gates production by default.** A machine whose blocktype carries
  `ExRightClickConstructable` and hosts a production tick used to tick through its own unfinished
  construction unless it named the gate itself; the behaviour's new `IProductionReadiness` answer
  now waits for it on every such machine. A machine that must keep ticking while unfinished sets
  `gatesProduction: false` in its `entityBehaviors` properties (or `.GatesProduction(false)` in the
  `Construction(...)` builder).
- `FillerLayoutBuilder` no longer refuses a footprint that mixes `Layer`/`Slice`/`Face` grids; the
  grid core tracks every drawn cell regardless of which call drew it, so mixing is simply supported.
- ⛔ **Breaking: `ExBlockDef.Raw`/`RawByType` and `ExItemDef.Raw` are renamed `RootKey`/`RootKeyByType`.**
  The old names are kept as `[Obsolete]` forwarders for one release (see
  [Supported API](wiki/Supported-API.md) "Obsolete members"). `Raw` writes a top-level key the
  object loader reads only when it is a real blocktype/itemtype key; the new name says so.
- ⛔ **Breaking: `ExLiquids.Load(ICoreAPI)` moves to `LiquidCatalogueLoader.Load(ICoreAPI)`.** A
  registry does not read assets under the catalogue naming law; `ExLiquids.Load` is kept as an
  `[Obsolete]` forwarder for one release (see [Supported API](wiki/Supported-API.md) "Obsolete
  members"). `ProcessRouteRegistry` now stores its families in an `ExKeyedRegistry<ProcessRoute>`
  instead of a hand-rolled dictionary, with no visible change; `ProcessJobRegistry`,
  `BayOccupancyRegistry` and `MaterialRoleRegistry` keep their own dictionaries because each key
  holds a list of entries, which `ExKeyedRegistry<T>`'s one-value-per-key shape does not fit.

- ⛔ **Breaking: an unrecognised JSON key is now a load error, not a silently dropped field.**
  Every catalogue asset (`config/metals/*.json`, `config/liquids.json`,
  `config/materialroles.json`, `config/processroutes/*.json`, `config/processjobs/*.json`,
  `config/bayoccupancy/*.json`) is bound strictly: a misspelt or retired key fails that one
  file, named with the file and the key, rather than loading with the field quietly ignored.
  Every warning and error also now names the asset it came from.
- ⛔ **Breaking: the family half of exlib moved to `ExpandedLib.Industry.*`.** Pipe, molten and
  mechanical-power networks, the metal, heat and molten-material catalogues, and their shared
  helpers now live under `ExpandedLib.Industry.Pipes`, `.Molten`, `.MechanicalPower`, `.Metals`,
  `.Heat`, `.Helpers` and `.Materials`. Type names and registered codes are unchanged - only the
  namespace moved - so a `using` fix is the whole cost of the update. `ExpandedLib.Industry.*` is
  public and reusable but carries no stability promise: it is the family's content layer, not
  the framework contract.
- `CellRole` is now a string-keyed `readonly record struct` (`CellRole.Of(key)`) instead of an
  exlib-declared enum; a machine's roles are declared by the mod that owns it (see iiex's
  `FurnaceCellRoles`). exlib itself declares no roles.
- ⛔ **Breaking: `mods/exlib/src` is reorganised so one top-level folder is one namespace** (see
  [Supported-API](wiki/Supported-API.md), [conventions](../../docs/design/conventions.md) "How
  exlib is laid out"). Type names, members and registered codes are unchanged - only folders,
  namespaces and `using`s moved:
  `ExpandedLib.Registries.{Entities,Commands,Preferences,Recipes}` and `ExpandedLib.{Commands,Preferences}`
  -> `ExpandedLib.Registries`; `ExpandedLib.Registries.Config` -> `ExpandedLib.Config`;
  `ExpandedLib.{Processes,Materials,Fluids,Storage}` and the catalogue loader/report classes that
  stayed in `ExpandedLib.Registries` -> `ExpandedLib.Catalogues`;
  `ExpandedLib.Blocks.{Behaviors,Construction}` -> `ExpandedLib.Blocks`;
  `ExpandedLib.Blocks.{Migrations,Healing}` -> `ExpandedLib.Migrations`;
  `ExpandedLib.Blocks.Structures` -> `ExpandedLib.Structures`;
  `ExpandedLib.Blocks.Machines` -> `ExpandedLib.Machines`;
  `ExpandedLib.Blocks.Networks` -> `ExpandedLib.Networks`; `ExpandedLib.Renderers` and
  `ExpandedLib.Patches` -> `ExpandedLib.Helpers`.
- ⛔ **Breaking (`exlib.testing`, the dev-only harness bundle): `ExpandedLib.Testing.Doubles` is
  folded into `ExpandedLib.Testing`.** The test doubles (`StubNetwork`, `TestNetworkBlock`,
  `CapturingNode`, `SeverableNode` and the rest) move from `Doubles/` namespace into the harness's
  single namespace; the folder stays, only the `using` goes. `using ExpandedLib.Testing;` is the
  whole cost of the update.
- `MetalRegistry.DefaultRecoveryFallback` replaces the removed `ExlibConfig.MetalRecoveryFallback`:
  the drop-item fallback for an unresolved molten solid drop is content knowledge, not a
  framework default, so a dependent mod now owns its own fallback (iiex's `IiexConfig`).
- The published API boundary is enforced: every public type of `exlib.dll` outside
  `ExpandedLib.Industry` is now either listed on the new [Supported
  API](wiki/Supported-API.md) page or marked `[EditorBrowsable(EditorBrowsableState.Never)]`
  (public because the engine instantiates it by reflection, not because a mod should call it).
  A guard test keeps the two in step. Going forward, a public member on the supported surface is
  removed only after one full release spent marked `[Obsolete]` naming its replacement.
- `AssetCatalogueLoader`, `ExConfigFiles`, `ExDefinitionOrigin`, `ExJson`, `ExPreferencesConfig`,
  `ExSyntheticAsset`, `FillerSlab` and `NetworkHighlightRequest` are `internal` on the framework
  surface this page covers.
- A tuned `MetalRecoveryFallback` under the `exlib` section of `ex_values.json` is not carried into
  the new `iiex` key - the field moved mods, not just section, so it must be set again there.
- `ExpandedLibModSystem`, `ExDefinitionModSystem`, `ChunkColumnSweeperModSystem`,
  `NetworkHighlightModSystem`, `ExmodCommand`, `ConfigSubCommand`, `HealSubCommand`,
  `MeasureSubCommand`, `NetworkSubCommand`, `RecipesSubCommand`, `HandbookUnitPatch`,
  `ExlibConfig`, `MeasurePreference` and the generated `ExlibBlocks` / `Structurefiller` types
  are marked `[EditorBrowsable(EditorBrowsableState.Never)]`: public because the engine has to
  see them, not part of the supported surface.
- New wiki pages: [Supported API](wiki/Supported-API.md) (the published boundary),
  [Code-First Definitions](wiki/Code-First-Definitions.md) and [Lifecycle](wiki/Lifecycle.md).

### Fixed

- `exlib-verify` reported a patch aimed at a code-first definition as a missing target. exlib
  injects one synthetic blocktype/item/recipe asset per definition before the patch loader runs,
  so those files exist in a running game and never on disk - siex's iiex refractory-pipe compat
  patch alone produced 27 errors for patches that apply correctly. A target missing from a domain
  whose mod ships an assembly is now an informational finding naming why it cannot be checked
  headlessly; a target missing from a JSON-only domain is still an error.
- ⛔ **Breaking: `AssetCheckSource.Domains` now covers only exlib and the mods that depend on it.**
  It used to answer every loaded mod, vanilla's own `game`/`survival`/`creative` included, whose own
  incomplete non-English locales alone produced on the order of 164,000 findings on a full-tree
  smoke run. `/exmod verify <domain>` still checks any domain named explicitly, in or out of scope.
- ⛔ **Breaking: `LangCoverageCheck` now guards the `en` locale only.** An unresolved `en` key is
  what renders raw on screen; a gap in some other shipped locale falls back to English instead, and
  tracking parity across a mod's other locales moved to `ExpandedLib.Testing.LangCoverage` and each
  mod's own `LangParityTests` (a repository-time concern the running game never needs to fail over).
- `MultiblockCodesCheck` matches a layout's wanted code against the registry segment by `-`
  rather than only trimming a trailing `*`, so a wildcard in the middle of a reference
  (`furnace-blastcore-*-n`, any tier) or more than one in one reference
  (`furnace-firebox-*-*`) resolves against the concrete variant it means instead of reporting a
  false dangling code.
- `ExBlockDef.MineTool`/`ExItemDef.MineTool` are now a no-op, `[Obsolete]`: `mineTool` was never a
  key the loader reads, and the mining-tool preference the callers wanted is already carried by
  `Material` (the per-material mining-speed table) and `MiningTier`; the three iiex call sites and
  the sample drop the dead call. `HandbookExclude` now sets `attributes.handbook.exclude`, matching
  the handbook system's own read, instead of a top-level `handbook` key nothing reads. Two dead
  `RootKey("temperatureDamage", ...)` calls on the wrought-ball and stock items are removed for the
  same reason - no vanilla key by that name exists. Found by the sample's smoke run and fixed as
  Task E2's boundary re-check.
- `KnownRootKeys` walked only public instance members, missing several loader fields the vanilla
  types declare `private` with a `[JsonProperty]` (`CollisionBox`/`SelectionBox`, the deprecated
  `heldTpIdleAnimation`); it now walks every type in the hierarchy with `DeclaredOnly` to see them
  too. The known-key audit also stopped flagging any `*ByType` root key as unknown - vanilla's own
  loader (`RegistryObjectType.solveByType`) resolves that suffix generically for any field name
  before binding, so the un-suffixed name need not itself be a member KnownRootKeys can see. Between
  the two, the smoke lane's `root key '...' is not a ... key the game reads` warnings drop from
  several hundred to zero.

## [0.7.3] - 2026-08-13

Six public subsystems landed between 0.7.0 and 0.7.2 without a changelog entry; they are
recorded here together with 0.7.3's own packaging work.

### Added

- **Code-first definitions** (`ExBlockDef` / `ExItemDef` / `ExRecipeDef`). Blocks, items
  and recipes are authored in C# and injected as synthetic assets at `ExecuteOrder 0.04` -
  above the base index, below the JSON patch loader (0.05) and the object loader (0.2) - so
  vanilla variant expansion, the atlas, block-id assignment and other mods' JSON patches all
  still apply. Fluent builders with derived codes, an ASCII multiblock layout DSL validated
  at load, and type-safe class binding.
- **Process extension contract** (`StageLadderRegistry`, `ProcessJobRegistry`, `SpecSchema`,
  `ProcessExtensions`, `ItemDie`). Merged catalogues read from `config/stageladders/*.json`
  and `config/processjobs/*.json`, load-order-independent, with a versioned spec format:
  an absent `schema` reads as 1, an older one falls back, and a **newer one is refused** with
  an error naming both versions.
- **Metal, material-role, liquid and heat catalogues** (`MetalRegistry`, `MaterialRoleRegistry`,
  `ExLiquids`, `HeatBalance`), each backed by a JSON catalogue a dependent mod contributes to.
- **XML documentation now ships** (`exlib.xml`, beside the dll in every zip), so a consumer
  gets IntelliSense over the public surface instead of bare signatures.
- **Source generators are distributed** in the `exlib-testing` bundle under `analyzers/`.
  The config-accessor recipe in the wiki previously could not compile outside this repository.
- **`[assembly: ExDomain]`** declares the domain an assembly's registered classes are keyed
  under, so `Class<T>()` / `Behavior<T>()` resolve correctly across assemblies.
- **`[ExDefDomain]`** lets one assembly emit definitions into more than one domain.
- **`BlockPipe.Tier`** - a pipe's family, read from its `tier` variant, with
  `PlatedTier` / `CastTier` / `RolledTier` naming the three this project ships. A pipe that
  declares no tier takes the default rating, throughput and joint, which is what every
  fitting does.

### Changed

- ⛔ **Breaking: a pipe's tier is a variant, not its domain.** `RegisterBurst`,
  `RegisterThroughput` and `RegisterJoint` are keyed on the tier name rather than the mod id,
  and `BlockPipe.Segments` takes the tier as a second argument
  (`Segments(domain, tier)`; pass `null` for an untiered family). A mod may now ship several
  tiers under one domain, which one domain per tier made impossible. Callers registering with
  `Mod.Info.ModID` must pass their tier name instead - the registration is otherwise silently
  unused and every segment falls back to the defaults.
- ⛔ **Breaking: `BlockPipePassthrough.Passthroughs` takes the tier too**
  (`Passthroughs(domain, tier)`, `null` for an untiered family). Two tiers shipping a passthrough
  apiece carried one code between them, so under a single domain the later registration replaced
  the earlier with no error; the tier is what keeps both. The sheet texture is now selected by tier
  rather than by domain.
- **Assembly identity is real.** Every release previously shipped `AssemblyVersion` and
  `FileVersion` `1.0.0.0`; both are now read from the mod's own `modinfo.json`.
  `AssemblyVersion` stays `major.minor.0.0` so a patch does not break a dependent's binding.
- **Type-safe class binding works across assemblies.** `EntityRegistry.KeyFor` resolves the
  domain from the *type's* assembly rather than the caller's. Naming a class from a dependency
  previously produced a key nobody had registered - which compiles, fails at world load, and on
  the block half is not logged.

### Fixed

- **An unregistered network type no longer takes a world down.** `AddNode` runs inside chunk
  load; a mistyped `networkType` threw out of it. It now logs an error naming the block, the
  position, the requested type and the registered types, and adds no node.
- **The test harness runs outside this repository.** Its path helpers probed upward for a file
  literally named `VintageStory.sln` and threw everywhere else, which disabled the goldens, the
  block-code table and the handbook sync for any outside consumer. Any `.sln`/`.slnx`/`.git`
  now marks a root, overridable with `EXLIB_REPO_ROOT`.
- Nineteen XML doc references that pointed at nothing, surfaced by enabling the doc file.

## [0.7.0] - 2026-06-21

### Added

- **Orphaned block-entity healer.** A server-side system that recreates a block
  entity when a block is left in the world without one - e.g. a block entity
  discarded on chunk load (a load exception) or lost to a server desync, which
  otherwise leaves an inert, often unbreakable block. It runs automatically as
  chunks load (and once over already-loaded chunks at startup), scoped to block
  entities registered through the mod's attribute system so vanilla/other-mod
  entities are never touched.
- **`/exmod heal` command.** Sweeps the loaded chunks and recreates orphaned block
  entities on demand, for an operator who does not want to wait for the automatic
  on-load pass. Server-side, gated behind the `/exmod` root's `controlserver`.
- **Config framework.** A generic, versioned per-mod config store with
  source-generated value accessors, version-reset migrations, and legacy file-name
  renaming. Values can be marked manageable and edited live via
  `/exmod config <mod> [value] [new]` - applied immediately, no world reload.
- **Min/max range gates** on config values: out-of-range edits are rejected with a
  clear message.
- **Recipe-cost profiles.** A per-mod catalogue framework that rebalances grid and
  right-click-construction ingredient quantities, switchable with
  `/exmod recipes <mod> <level>`.
- **Content-gating helper** (`ExContentGate`) for hiding a block/item from creative
  and the handbook and removing its recipes - the framework behind smex's mold
  toggle.
- **Command framework.** Attribute-driven `[CommandRegister]` / `[SubCommandRegister]`
  registration under a shared `/exmod` (server) and `.exmod` (client) root, so
  dependent mods hang their own sub-commands off one root.
- **Production-machine base** (`BlockEntityProductionMachine`) and machine-port
  helpers, shared by engines, furnaces, converters and sub-machines.
- **Legacy support framework.** Shims and polyfills that let the family build and run
  against Vintage Story 1.21 and 1.20 alongside 1.22.
- **Russian and Ukrainian** translations.

### Changed

- **Internal reorganization** into `Blocks/{Networks,Structures,Machines,Migrations,Construction,Healing}`,
  `Registries/{Entities,Commands,Config,Preferences,Recipes}`, `Helpers`,
  `Renderers` and `Legacy`.
- **Registration attributes split.** The single `[EntityRegister]` became
  kind-specific `[BlockRegister]`, `[ItemRegister]`, `[BlockEntityRegister]`,
  `[BlockBehaviorRegister]`, `[BlockEntityBehaviorRegister]` and
  `[CollectibleBehaviorRegister]`, each validating that the class derives from the
  expected base type.
- **Right-click-construction salvage:** the ratio of materials dropped when a
  partially-built or finished structure is broken is now configurable.
- **Multiblock structures read live config changes** without a world reload.

### Fixed

- Right-click-constructable blocks ignored their last construction stage when
  computing dropped materials.
- Non-pipe network blocks could incorrectly burst.
- Block display-name ordering and assorted localization issues.
- `/exmod config` value display formatting.

## [0.6.0] - 2026-06-16

### Added

- **Command framework.** A shared `/exmod` (server) and `.exmod` (client) command
  root, with a server-side version and privilege handling, so dependent mods hang
  their sub-commands off one root.
- **Measurement helpers** (metric/imperial) and a **per-player preference registry**,
  with a handbook patch that converts displayed measurements to the player's units.
- **Network-highlight** subcommand and a **base surface renderer**.
- **Source generators** that bake block/item JSON attributes into generated class
  members.
- **Config migrations** from older versions.

### Changed

- The **structure filler** mirrors the principal block's block-info.

## [0.5.1] - 2026-06-14

### Changed

- The **migration system** now also covers items held in inventories, not just placed
  blocks.

### Fixed

- **Right-click-constructable** wildcard handling and the names shown for missing
  materials.

## [0.5.0] - 2026-06-13

The first standalone release of the shared library, extracted from Steelmaking
Expanded (internal `0.1.0` groundwork promoted to `0.5.0`).

### Added

- **Block-network framework** (nodes, connectors, graph) backing gas pipes and molten
  canals.
- **Multiblock structure framework** with right-click construction.
- **Attribute-driven registration** for blocks, items and behaviors.
- **World-migration system** for updating old blocks.
- Shared **particle, sound and orientation** catalogues and helpers.
