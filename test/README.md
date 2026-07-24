# Test suites

Five xUnit projects, one per mod, plus the shared harness. The **project** a test lives in is decided
by the dependency chain; the **folder** it lives in is decided by the table below. Both rules are
mechanical - if you have to think about it, the answer is in one of the two tables here.

## Projects

| Project | Covers | References |
|---|---|---|
| `ExpandedLib.Testing` | *(not a test project)* the headless harness itself - `TestWorld`, `Scene`, `SceneDiagram`, `TestBlocks`, content-free doubles | exlib only |
| `ExpandedLib.Tests` | exlib framework | harness |
| `IronworkingExpanded.Tests` | iwex | harness |
| `LowPressureExpanded.Tests` | lpex | + iwex tests |
| `SteelmakingExpanded.Tests` | smex | + lpex tests |
| `HighPressureExpanded.Tests` | hpex | + lpex tests, + smex tests *(test-only)* |

**Homing rule:** a test file lives in the project of the **top mod whose _types_ it touches** - not
the mod its folder is named after, and not the mod in its `namespace` line (all test files use one
flat `<Mod>.Tests` namespace regardless of folder, so folders are free to move). Block **code
strings** lie too: headless blocks are hand-configured, so a `"smex:…"` literal in a test proves
nothing about ownership.

The test-project reference chain deliberately mirrors the mod chain. Content-specific fixtures live
with their content (`PipeTestWorld` in iwex because pipes are iwex; the boiler/engine plants in lpex);
only **content-free** doubles may go in `ExpandedLib.Testing`, which ships as a standalone dev bundle
and must stay mod-agnostic.

`HighPressureExpanded.Tests` → `SteelmakingExpanded.Tests` is the one edge with no mod counterpart:
the Cornish engine drives smex's air blower, so those scenarios need both assemblies, and hpex-on-top
is the only legal direction (smex must never see hpex).

## Folders

Every project uses the same top-level buckets, omitting the ones it has no content for:

| Folder | Holds |
|---|---|
| `Fixtures/` | support types with **no `[Fact]`** - see the suffix vocabulary below |
| `Definitions/` | code-first `ExBlockDef`/`ExItemDef`/`ExRecipeDef` providers, golden tests, metal/catalogue registration, shipped-JSON guards |
| `Blocks/<Family>/` | block-entity behaviour, one subfolder **named after the `src/` area** it covers (`Blocks/Boiler/` ↔ `src/…/BlockStructures/Boiler/`) |
| `Networks/` | the network model itself - graph walks, pools, flow, connectors |
| `Items/` | item behaviour |
| `Materials/` | material roles, metal parity, burden/composition classifiers |
| `Migrations/` | save migrations |
| `Invariants/` | registry-wide invariants that span families |
| `Scenarios/` | multi-block, world-built, end-to-end runs |
| `Helpers/` | pure helper/utility tests |
| `goldens/<domain>/` | golden files (rebless with `EXLIB_WRITE_GOLDENS=1`, never hand-edit) |

`ExpandedLib.Tests` additionally keeps framework-area folders that mirror `src/ExpandedLib/`
(`Config/`, `Registries/`, `Metals/`, `Fluids/`, `Generators/`, `Localization/`, `Machines/`,
`Structures/`, `Recipes/`, `Harness/`) - it has no machine families to put under `Blocks/`.

### Fixture suffixes

| Suffix | Means | Example |
|---|---|---|
| `*Scenes` | a **file** grouping the fixtures for one area; may hold several types | `SteamSupplyScenes.cs` |
| `*Rig` | a driver for **one machine** - builds it, exposes fast-forwards and readbacks | `BlastFurnaceRig` |
| `*Plant` | a machine **plus its supply chain** (boiler + main + engine), driven as a unit | `MPGeneratorPlant` |
| `*Fakes` | stand-ins with no behaviour of their own | `BoilerFakes` |

`EngineFixture` and `BoilerFixture` predate this vocabulary and are `*Rig`s in all but name; they are
left alone deliberately - renaming them collides with the existing `BoilerRig` and buys nothing.

A `*Rig` need not drive a *block*: `FurnaceLayoutRig` (iwex) drives the **assertion** shared by every
furnace in the line - offsets against the shipped layout, at north and at all four orientations. It
lives in iwex because the cells it checks are iwex types, and smex reaches it through the reference
chain. That is the homing rule doing its job: the shared oracle sinks to the lowest mod that owns the
types, and each suite above keeps only the facts that need one of *its* types (smex's furnace tests are
now "the hot furnace vents", "its cells rotate", and "both furnaces agree").

Multiblock machines belong in `Blocks/<Family>/` or `Scenarios/` like anything else, but they must be
stood up with `StructureRig` rather than a forced `StructureComplete` - see
[docs/wiki/Testing-Harness.md](../docs/wiki/Testing-Harness.md#standing-up-a-mega-block-with-structurerig).
**Every machine in the repo now does.** The only two `SetProperty(be, "StructureComplete", …)` sites
left are deliberate: `MultiblockProjectionTests` and `FurnaceHudDistributionTests` gate on the flag
itself rather than on a live recount, which is the thing under test.

Two things a fixture must get right, both of which used to be invisible:

- **The anchor has to wear the code its own layout asks for at the origin cell.** A hand-configured
  `smex:cowperstove-north` is not `smex:cowperstove-intake*`; the structure is then permanently one
  cell short and the machine never commissions. Four machines shipped fixtures with the wrong anchor
  code, and nine service blocks across them had codes no block has carried in months.
- **A scene must not build inside a machine.** Once the footprint is real, a pond intake or a cap block
  dropped on a layout cell breaks the structure on the next monitor tick. `StructureRig.MissingReport`
  names the offending cell.

**Pipe tier is a real choice in a fixture, not decoration.** `PipeTestWorld.MakePipe(material:)` selects
a tier - `"iron"` bolted (iwex), `"steel"` cast (lpex), `"hadfield"` rolled (hpex) - and the tier sets
the burst ceiling. LP steam at 3-5 atm does not belong on bolted pipe; charging it there bursts the run,
which is exactly the gate the tier ladder exists to enforce. The iwex rating is read from live config;
the other two are constants in the fixture (iwex cannot reference lpex or hpex) guarded by
`PipeBurstParityTests` in each of those suites - a stale copy silently retunes every burst test in three
suites and they all still pass.

Two distinctions that decide most borderline cases:

- **`Blocks/<Family>/` vs `Networks/`** - a block entity that *uses* a network is still a block test
  (`Blocks/SmokeStack/`); `Networks/` is for the network model itself (`GasPoolTests`, `MoltenFlowTests`).
- **`Blocks/<Family>/` vs `Scenarios/`** - one machine driven directly is a block test; several blocks
  placed in a world where the behaviour *emerges* from adjacency and ticking is a scenario.

Root files: `ModuleInit.cs` (module initializer - assembly resolver + registry seeding) and
`LegacyUsings.cs` where a project needs it.

## Running

```
scripts/run-tests.ps1              # all five suites, current game version
scripts/run-tests.ps1 -Legacy      # also 1.21 (net8.0) and 1.20 (net7.0)
```

Two traps worth knowing, both of which fail *quietly*:

- **A suite reporting 0 tests is a failure, not a pass.** vstest probes a test assembly's direct
  references on disk *before* the harness's `VsAssemblyResolver` runs, and **skips** the assembly
  ("could not find dependent assembly 'VintagestoryAPI'") instead of erroring. Fix is
  `<Private>true</Private>` on that project's `VintagestoryAPI` reference.
- **Process-global statics race** across test classes, because xUnit parallelises them. Serialize
  every class touching one with a `[CollectionDefinition(…, DisableParallelization = true)]` - see
  `SteelmakingExpanded.Tests/Blocks/Molds/MoldGatingCollection.cs` and
  `IronworkingExpanded.Tests/Fixtures/FurnaceConfigCollection.cs`. **Joining is what serializes**: a
  collection only orders the classes that opt in, so the *readers* of a mutated static must join it too,
  not just the writer. A writer alone looks fine and races anyway.

Two source-tree guards live in `ExpandedLib.Tests` rather than per-mod, because they discover every
domain from the source tree and so cover a new mod automatically: `LangParityTests` (locale key sets +
format placeholders) and `HandbookParityTests` (shipped handbook text vs its `docs/<mod>/handbook/*.html`
source - `EXLIB_WRITE_HANDBOOK=1` to import, `EXLIB_EXPORT_HANDBOOK=1` to write back the other way).

See [docs/wiki/Testing-Harness.md](../docs/wiki/Testing-Harness.md) for the harness API.
