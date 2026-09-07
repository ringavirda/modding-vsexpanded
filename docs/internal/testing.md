# Test suites

Four xUnit test projects - one per mod, plus `HelloExpanded.Tests` for the sample third-party mod -
plus the shared harness. The **project** a test lives in is decided by the dependency chain; the
**folder** it lives in is decided by the table below. Both rules are mechanical - if you have to
think about it, the answer is in one of the two tables here.

A mod outside this repo scaffolds its own equivalent of `HelloExpanded.Tests` with `dotnet new
exlib-tests` (`templates/exlib-tests/`, identity `ExpandedLib.Templates.Tests`) - see
[Testing Harness](../../mods/exlib/wiki/Testing-Harness.md) "Ten minutes to a green test".

## Projects

| Project | Covers | References |
|---|---|---|
| `ExpandedLib.Testing` | *(not a test project)* the headless harness itself, one namespace laid out by activity: `World/`, `Scenes/`, `Rigs/`, `Doubles/`, `Checks/`, `Repo/` | exlib only |
| `ExpandedLib.Tests` | exlib: both the framework (`exlib.dll`) and the family's domain layer beside it (`exlib.industry.dll`) | harness + industry |
| `IronIndustryExpanded.Tests` | iiex | harness |
| `SteelIndustryExpanded.Tests` | siex | + iiex tests |
| `HelloExpanded.Tests` | the sample third-party mod, `samples/HelloExpanded` | harness only - deliberately outside the `exlib -> iiex -> siex` chain, the shape a stranger's project takes |

**Homing rule:** a test file lives in the project of the **top mod whose _types_ it touches** - not
the mod its folder is named after, and not the mod in its `namespace` line (all test files use one
flat `<Mod>.Tests` namespace regardless of folder, so folders are free to move). Block **code
strings** lie too: headless blocks are hand-configured, so a `"siex:..."` literal in a test proves
nothing about ownership.

The test-project reference chain deliberately mirrors the mod chain. Content-specific fixtures live
with their content (`PipeTestWorld` in iiex because pipes are iiex; the boiler/engine plants in iiex);
only **content-free** doubles may go in `ExpandedLib.Testing`, which ships as a standalone dev bundle
and must stay mod-agnostic. `ReleasedCodes.cs` is the one sanctioned exception to "content-free doubles
only": harness-owned release history - the shipped ppex/smex catalogues that migration coverage replays.

## Folders

Every project uses the same top-level buckets, omitting the ones it has no content for:

| Folder | Holds |
|---|---|
| `Fixtures/` | support types with **no `[Fact]`** - see the suffix vocabulary below |
| `Definitions/` | code-first `ExBlockDef`/`ExItemDef`/`ExRecipeDef` providers, golden tests, metal/catalogue registration, shipped-JSON guards |
| `Blocks/<Family>/` | block-entity behaviour, one subfolder **named after the mod's `src/` area** it covers (`Blocks/Boiler/` <-> `mods/iiex/src/BlockStructures/Boiler/`); network-block areas conventionally split finer than `src/` (iiex `Pipe/`, `Valves/`, `Condenser/`, `FluidIntake/` all cover `src` `BlockNetworkPipe`; iiex `Energy/` and `Molten/` cover `BlockNetworkEnergy`/`BlockNetworkMolten`; iiex `Blocks/Pipe/` covers the exlib pipe bases exercised at the iiex tier) |
| `Networks/` | the network model itself - graph walks, pools, flow, connectors |
| `Items/` | item behaviour |
| `Materials/` | material roles, metal parity, burden/composition classifiers |
| `Migrations/` | save migrations |
| `Invariants/` | registry-wide invariants that span families |
| `Scenarios/` | multi-block, world-built, end-to-end runs |
| `Helpers/` | pure helper/utility tests |
| `goldens/<domain>/` | golden files (rebless with `EXLIB_WRITE_GOLDENS=1`, never hand-edit) |

`ExpandedLib.Tests` additionally keeps framework-area folders that mirror `mods/exlib/src/` folder
for folder (`Config/`, `Registries/` with `Recipes/` merged in, `Catalogues/` with `Processes/`,
`Materials/`, `Fluids/`, `Storage/` subfolders, `Blocks/`, `Migrations/`, `Structures/`, `Machines/`,
`Networks/`, `Definitions/`, `Helpers/` with a `Measure/` subfolder, plus `Metals/` for the
`Industry.Metals` content it exercises, `Generators/`, `Localization/`, `Harness/`) - it has no
machine families to put under `Blocks/`.

`Localization/` is not exlib-only: every mod's suite (and the sample's, and the template's) keeps a
thin `Invariants/ShippedAssetJsonTests.cs`/`LoopingAnimationTests.cs` and a thin
`Localization/LangKeyResolutionTests.cs`/`LangParityTests.cs` calling the harness's four repo-wide
guards - `ShippedJson`, `LoopingAnimations`, `LangKeys`, `LangParity` - over its own tree, the shape
that used to live only in `ExpandedLib.Tests` scanning everyone's. `RepoPaths` and `RepoManifest`
resolve which tree belongs to which mod (and which mod ships an overlay domain, like iiex's `game`
lang overlay) from `exmod.json` at the repo root, falling back to the `mods/<id>` convention when
that file is absent; see [Testing Harness](../../mods/exlib/wiki/Testing-Harness.md) "Repo-wide
guards, per mod".

### Fixture suffixes

| Suffix | Means | Example |
|---|---|---|
| `*Scenes` | a **file** grouping the fixtures for one area; may hold several types | `SteamSupplyScenes.cs` |
| `*Rig` | a driver for **one machine** - builds it, exposes fast-forwards and readbacks | `BlastFurnaceRig` |
| `*Plant` | a machine **plus its supply chain** (boiler + main + engine), driven as a unit | `MPGeneratorPlant` |
| `*Fakes` | stand-ins with no behaviour of their own | `BoilerFakes` |

`EngineFixture` and `BoilerFixture` predate this vocabulary and are `*Rig`s in all but name; they are
left alone deliberately - renaming them collides with the existing `BoilerRig` and buys nothing.

A fixture that needs a player stands one up with `world.Player()` (`ExpandedLib.Testing.TestPlayer`)
rather than hand-rolling `Substitute.For<IPlayer>()` - it already carries a real hotbar slot, the
entity and inventory manager wired, and works the same across lanes. `ExOrientableRig` (exlib) is the
converted example; a fixture that still substitutes `IPlayer` by hand predates the double.

A `*Rig` need not drive a *block*: `FurnaceLayoutRig` (iiex) drives the **assertion** shared by every
furnace in the line - offsets against the shipped layout, at north and at all four orientations. It
lives in iiex because the cells it checks are iiex types, and siex reaches it through the reference
chain. That is the homing rule doing its job: the shared oracle sinks to the lowest mod that owns the
types, and each suite above keeps only the facts that need one of *its* types (siex's furnace tests are
now "the hot furnace vents", "its cells rotate", and "both furnaces agree").

Multiblock machines belong in `Blocks/<Family>/` or `Scenarios/` like anything else, but they must be
stood up with `StructureRig` rather than a forced `StructureComplete` - see
[mods/exlib/wiki/Testing-Harness.md](../../mods/exlib/wiki/Testing-Harness.md#standing-up-a-mega-block-with-structurerig).
**Every machine in the repo now does.** The only two `SetProperty(be, "StructureComplete", ...)` sites
left are deliberate: `MultiblockProjectionTests` and `FurnaceHudDistributionTests` gate on the flag
itself rather than on a live recount, which is the thing under test.

Two things a fixture must get right, both of which used to be invisible:

- **The anchor has to wear the code its own layout asks for at the origin cell.** A hand-configured
  `siex:cowperstove-north` is not `siex:cowperstove-intake*`; the structure is then permanently one
  cell short and the machine never commissions. Four machines shipped fixtures with the wrong anchor
  code, and nine service blocks across them had codes no block has carried in months.
- **A scene must not build inside a machine.** Once the footprint is real, a pond intake or a cap block
  dropped on a layout cell breaks the structure on the next monitor tick. `StructureRig.MissingReport`
  names the offending cell.

**Pipe tier is a real choice in a fixture, not decoration.** `PipeTestWorld.MakePipe(material:)` selects
a tier - `"iron"` bolted (iiex), `"steel"` cast (iiex), `"hadfield"` rolled (siex) - and the tier sets
the burst ceiling. LP steam at 3-5 atm does not belong on bolted pipe; charging it there bursts the run,
which is exactly the gate the tier ladder exists to enforce. The plated-tier rating is read from live
config; the other two are constants in the fixture (iiex cannot reference siex) guarded by
`PipeBurstParityTests` in each of those suites - a stale copy silently retunes every burst test in two
suites and they all still pass.

Two distinctions that decide most borderline cases:

- **`Blocks/<Family>/` vs `Networks/`** - a block entity that *uses* a network is still a block test
  (`Blocks/SmokeStack/`); `Networks/` is for the network model itself (`GasPoolTests`, `MoltenFlowTests`).
- **`Blocks/<Family>/` vs `Scenarios/`** - one machine driven directly is a block test; several blocks
  placed in a world where the behaviour *emerges* from adjacency and ticking is a scenario.

Root files: `ModuleInit.cs` (module initializer - assembly resolver + registry seeding) and
`LegacyUsings.cs` where a project needs it.

## Internal access

Each mod grants `InternalsVisibleTo` to its own test project (`mods/<mod>/src/InternalsVisibleTo.cs`), so
new tests should prefer internal accessors over string-keyed reflection - a rename breaks an accessor
at compile time but breaks a reflection string only at run time. The first choice is a named test seam
on the exlib base itself: `BlockEntityProductionMachine`/`BEBehaviorProductionMachine.DriveProductionTick`
and `DriveIdleTick` run a tick exactly as the registered listener would (the first honours the readiness
gate, the second bypasses it); `BlockEntityMultiblockStructure.DriveMonitorTick` and
`ApplyStructureRotation` do the same for the completion monitor and the rotation recompute;
`BlockNetworkNode.SetNetworkTypeForTest` and `ApplyOrientationForTest` write `Type`/`Orientation`
directly where `OnLoaded` is skipped headlessly. `ReflectionHelpers` remains for whatever has no seam
yet. Derived state stays read-only either way: a property like the furnace's `State` is computed from
the machine's inputs, and tests must set up those inputs, never get a setter added for their
convenience.

## Running

**MSBuild worker nodes.** Node reuse is off for every command-line build in this repo
(`Directory.Build.rsp` at the root carries `-nodeReuse:false`, and `scripts/exmod.ps1` sets
`MSBUILDDISABLENODEREUSE=1`). The workers that pile up on a busy day come from somewhere else: the
VS Code C# Dev Kit runs a design-time build on every file change and spawns `MSBuild.dll /nodemode:1
/nodeReuse:true` workers that never exit (74 of them held 11 GB on 2026-09-05; `ps -o ppid` names
the Dev Kit's CPS host as the parent). Those read no response file, so the variable has to be in the
VS Code server's environment: `~/.vscode-server/server-env-setup` exports it and takes effect when
the server restarts. If `ps -eo etimes,args | grep nodemode` shows workers older than a build, `kill`
them; a running build spawns fresh ones.


```
scripts/exmod.ps1 test latest      # all suites, current game version
scripts/exmod.ps1 test all         # also 1.21 (net8.0) and 1.20 (net7.0)
scripts/exmod.ps1 codes <mod>      # regenerate {Mod}Blocks.g.cs from its definitions (exlib|iiex|siex)
scripts/exmod.ps1 smoke            # boot the real dedicated server with every built mod, then stop it
```

`codes` builds the mod, runs the standalone `infra/tools/BlockCodeEmitter` console tool to write
`mods/<mod>/src/Generated/<Mod>Blocks.g.cs`, and rebuilds so a table change that no longer compiles
is caught here. The mod's own `*BlocksCodeTests` fixture only compares against that file now - there
is no env-var switch to write it from inside a test run.

`smoke` provisions a dedicated-server install if none is found (`scripts/exmod.ps1 provision game`),
assembles a scratch mods folder (`mods/*/src/bin/Debug/Mods/mod` by default, keyed by each mod's own
`modid`; `-Mods <dir>[,<dir>...]` to smoke-test something else instead), boots
`VintagestoryServer.dll` against it on an unusual fixed port (42499, so a game the owner is playing on
this machine is never disturbed), waits for it to report ready, runs `/exmod verify` and `/stop` on
its stdin, and fails (non-zero exit, printing the offending lines) on a timeout, an `[Error]`/`[Fatal]`
log line, or a non-clean verify summary. See [Testing Harness](../../mods/exlib/wiki/Testing-Harness.md)
"The smoke lane" for what it checks and the CI template.

`exlib-verify` (`infra/tools/ExlibVerify`, `dotnet run --project infra/tools/ExlibVerify --
<modpath> [--game <install>]`) is the JSON-only path into the same idea: no build, no xUnit, no
running game, just a mod folder or zip checked for parse errors, patches that don't apply, and
dangling recipe/handbook codes. See [Checks](../../mods/exlib/wiki/Checks.md), "Without the game:
exlib-verify", for the full error/informational split.

Two traps worth knowing, both of which fail *quietly*:

- **A suite reporting 0 tests is a failure, not a pass.** vstest probes a test assembly's direct
  references on disk *before* the harness's `VsAssemblyResolver` runs, and **skips** the assembly
  ("could not find dependent assembly 'VintagestoryAPI'") instead of erroring. Fix is
  `<Private>true</Private>` on that project's `VintagestoryAPI` reference.
- **Process-global statics race** across test classes, because xUnit parallelises them. Serialize
  every class touching one with a `[CollectionDefinition(..., DisableParallelization = true)]` - see
  `ExpandedLib.Tests/Helpers/ExMeasureCollection.cs` and
  `IronIndustryExpanded.Tests/Fixtures/FurnaceConfigCollection.cs`. **Joining is what serializes**: a
  collection only orders the classes that opt in, so the *readers* of a mutated static must join it too,
  not just the writer. A writer alone looks fine and races anyway. A bare `[Collection("Name")]` with
  no matching definition still "runs" - xUnit synthesises one per unique name - which is exactly how a
  typo or a copy-pasted literal silently stops serializing anything; `StaticStateCollection
  .EveryCollectionNameHasADefinition` (`ExpandedLib.Testing`) catches it, and each suite calls it from
  one `CollectionGuardTests` fact.

`HandbookParityTests` lives in `ExpandedLib.Tests` rather than per-mod, because it discovers every
domain from the source tree and so covers a new mod automatically: shipped handbook text vs its
`docs/<mod>/handbook/*.html` source - `EXLIB_WRITE_HANDBOOK=1` to import, `EXLIB_EXPORT_HANDBOOK=1`
to write back the other way.

## Packaging and release

`ExpandedLib` and `ExpandedLib.Testing` are `dotnet pack`-able NuGet packages (`PackageId`,
`Version` read from `modinfo.json`, `Authors`, `Description`, `RepositoryUrl`, `PackageReadmeFile`,
`PackageLicenseExpression` MIT, matching the repository's `LICENSE`). `.github/workflows/release.yml`
builds both on a `v*` tag, alongside the Cake `Package`/`PackageTesting` mod zips and dev bundle, and
attaches everything to the GitHub release; nothing is pushed to NuGet.org (the step is present and
commented). `templates/ci/tests.yml` and `templates/ci/smoke.yml` are the copy-into-your-own-repo
CI templates for a mod outside this one.

See [mods/exlib/wiki/Testing-Harness.md](../../mods/exlib/wiki/Testing-Harness.md) for the harness API.
