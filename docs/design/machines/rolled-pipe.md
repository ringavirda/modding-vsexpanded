# Rolled Pipe (hpex tier)
**Status** live (four blocktypes ship) · blocked - zero recipes (B5)   **Mod** hpex

**Owns**
- The rolled tier's identity: which blocktypes hpex contributes to the shared `pipe` network, where its
  rating, throughput and joint family are registered, what the segments are made of (shapes, textures,
  stacks), and that the model is octagonal.
- B5 in full: four live blocktypes, thirty variants, four shapes, three registrations, no way to obtain a
  segment outside creative, and no cost-catalogue key to price one against.
- That nothing in hpex consumes rolled pipe, including hpex's own two machines.
- The 12 atm rating's two side-effects: the run buffer it implies, and its equality with the Lancashire
  boiler's choke.
- `HpMachineDomainMigration` and its remap table.
- The hpex-side test coverage of the tier, including the one test whose assertion cannot detect the
  property it names.

**Does not own** - cited only:
- The graph substrate, the one-medium pool, capacity, pressure formulas, leaks, vents, tick order, burst
  mechanics, the burst-by-tier and throughput-by-tier tables, the joint-family rule and every exlib constant
  behind them - [pipe network](../mechanics/pipe-network.md). This page states hpex's row of those tables and
  the consequences specific to hpex blocks.
- Every fitting (valve, pressure valve, outlet, passthrough, passthrough-bend), and B6 in full - why the
  "mandatory" pressure valve cannot be installed on an HP line, in two independent ways, with the numbers on
  both sides - [cast pipes & fittings](cast-pipes.md). hpex ships no fittings, so it has no rows there.
- The cast tier's own uncraftability, its dead cost keys and its cast → bore → assemble route -
  [cast pipes](cast-pipes.md).
- The plated tier, the tuyere and the twin-tub blower - [pipe network](../mechanics/pipe-network.md),
  [twin-tub blower](twin-tub-blower.md).
- The skelp → segment → bell-weld chain this tier is made by, the machine that would do it, and every mass
  in it - [bending roller](bending-roller.md), [wide hall](wide-hall.md).
- The Lancashire boiler and the Cornish engine a rolled run connects -
  [Lancashire boiler](boiler-lancashire.md), [Cornish engine](engine-cornish.md).
- Code-first defs, the recipe-cost catalogue, `/exmod recipes` - [recipes & config](../mechanics/recipes-config.md).

---

## Role

Top rung of a three-rung pipe ladder: iiex plated at 2.5 atm, iiex cast at 5.0, hpex rolled at 12 - the only tier
that can carry a Lancashire boiler's steam to a Cornish engine without sitting on its own burst clock.

The tier is a variant axis, and the high-order one: a rolled pipe is `siex:pipe-rolled-straight-ns`, and the
`tier` variant resolves the burst rating, the throughput and the joint family (`BlockPipe.cs:252-255`, `:291-294`,
`:321-324`). One material per tier, one model per tier - that part is unchanged; what moved is where the tier is
written down.

Until 2026-08-14 this section read *"the tier is the mod, not a variant axis"*, and the domain was the key.
**M4 supersedes it** ([STATE.md](../../internal/plans/STATE.md)): the merge puts iiex and iiex in one domain, so
tier could no longer be `Code.Domain` without collapsing two tiers into one - which M3 forbids. The reasoning the
old ruling rested on survives intact on the new axis. `BlockPipe.PlatedTier` / `.CastTier` / `.RolledTier`
(`BlockPipe.cs:31`, `:34`, `:37`) are the three names.

⛔ **`tier` is declared first, before `type`** (`BlockPipe.cs:103`). The game's selector matcher backtracks, so a
leading `*` absorbs the new segment and the ~30 `ShapeByType("*-straight-ns", …)` selectors keep matching;
declared last it would move every code out from under them and the blocks would load with no shape and no error.
`EmittedBlocktypeShapeTests` catches that, and it catches it even after the goldens are re-blessed.

The joint separates this tier from the other two. Plated and cast are square in section and bolted through flanges,
so they interconnect and a player upgrades a line segment by segment. Rolled is octagonal and welded, with no
flange to bolt to, so a rolled run is an island: it joins rolled pipe and machine ports and nothing else in the
game ([pipe network](../mechanics/pipe-network.md) § 5), tested from both directions. The HP tier therefore needs
its own fittings before it is usable, and it has neither fittings nor a recipe.

---

## Structure

Nothing here is a multiblock. Every block on this page is a single cell and an instance of the shared `BlockPipe` /
`BlockEntityPipe`, which live in exlib (`src/ExpandedLib/Blocks/Networks/`); hpex ships no pipe C# class.

```csharp
public class RolledPipeDefinitions : IExBlockDefProvider
{
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    BlockPipe.Segments(domain, BlockPipe.RolledTier);
}
```
`RolledPipeDefinitions.cs:15-18`. A stand-alone provider (not a partial of a block class) because the class it
binds to lives in exlib: the injected blocktypes name `exlib.BlockPipe` / `exlib.BlockEntityPipe` as their class
keys (`BlockPipe.cs:78-79`). Discovered when the hpex assembly is scanned by `EntityRegistry.RegisterAll`.

| blocktype | variants | max stack | creative selector | file:line |
|---|---|---|---|---|
| `siex:pipe-rolled-straight-*` | 3 - `ns` `we` `ud` | 16 | `*-straight-ns` | `BlockPipe.cs:118-128` |
| `siex:pipe-rolled-bend-*` | 12 - `nw` `se` `en` `ws` `un` `us` `uw` `ue` `dn` `ds` `dw` `de` | 8 | `*-bend-nw` | `:130-165` |
| `siex:pipe-rolled-tjunction-*` | 12 - `uns` `uwe` `dns` `dwe` `nes` `esw` `swn` `wne` `dnu` `deu` `dsu` `dwu` | 8 | `*-tjunction-uns` | `:167-202` |
| `siex:pipe-rolled-xjunction-*` | 3 - `nswe` `nsud` `weud` | 8 | `*-xjunction-nswe` | `:204-216` |

30 block variants in total. All four share `Common` (`BlockPipe.cs:68-104`): metal material and sounds, the
`Lockable` behaviour, `RenderPass("OpaqueNoCull")`, `FaceCullMode("NeverCull")`, `LightAbsorption(0)`,
`SideSolid(false)`, `SideOpaque(false)`, a creative entry in both `general` and the `hpex` tab, the `tier`
variant group, and a `handbook.groupBy` naming all four of *this tier's* codes - grouping is per tier, so the
rolled straight pipe never shares a handbook entry with the plated or cast one. Collision/selection is the shared 5⁄16 → 11⁄16 core, one box per open
axis.

Connector geometry is the default, `Orientation.Contains(face.Code[0])` (`BlockNetworkNode.cs`); hpex overrides
nothing (the single-face outlet override is iiex's, [cast pipes](cast-pipes.md)).

### The three registrations

All happen once in `ModSystem.Start`, keyed by tier:

```csharp
BlockPipe.RegisterBurst(BlockPipe.RolledTier, () => SiexValues.RolledPipeBurstPressure);    // :39
BlockPipe.RegisterThroughput(BlockPipe.RolledTier, () => SiexValues.RolledPipeThroughput);  // :43
BlockPipe.RegisterJoint(BlockPipe.RolledTier, BlockPipe.WeldedJoint);                       // :49
```
`SteelIndustryExpandedModSystem.cs:39-49`. The burst and throughput getters are `Func<float>`s read live, so `/exmod
config hpex` retunes them without reconstructing networks. hpex registers no network type; the `pipe` network is
exlib's framework and the HP blocks ride it.

The key is the tier name, not `Mod.Info.ModID`, so the registration survives the merge that moves these blocks into
`siex` unchanged - and a mod carrying two tiers registers twice.

Dropping one fails differently from dropping another. `DefaultBurstPressure` is 5 (`BlockPipe.cs:241`), so a lost
`RegisterBurst` visibly halves the tier, unlike cast, whose default is numerically identical and whose loss would
be invisible ([cast pipes](cast-pipes.md) Gotcha 2). The joint default is `FlangedJoint` (`BlockPipe.cs:321-324`),
so a lost `RegisterJoint` silently bolts rolled pipe onto plated pipe. `RolledJointTests` covers the joint case and
not the burst one.

★ A block that names **no** tier takes all three defaults - which is what every fitting does, and what a consumer
shipping a single pipe family gets from `BlockPipe.Segments(domain, tier: null)`.

---

## Assets

| asset | path | state |
|---|---|---|
| Editable straight / bend / T / X | `assets/editable/shapes/pipe-block-rolled-{straight,bend,tjunction,xjunction}.json` | present. All four declare `cast-iron1` as an absolute local path into `assets/editable/textures/`, an editable-only artefact |
| Runtime straight | `assets/siex/shapes/pipes/straight.json` | `Cube2` (barrel, with 45°-rotated `Cube30`–`Cube33` chamfers) + `Cube6` and `Cube14` (the two end rings) |
| Runtime bend | `assets/siex/shapes/pipes/bend.json` | five top-level cubes, same chamfer construction |
| Runtime T / X | `assets/siex/shapes/pipes/tjunction.json`, `xjunction.json` | present |
| Textures (all four) | `cast-iron1` → `iiex:block/metal/castiron`, `steel` → `game:block/metal/plate/steel` | |
| Animations | - | none, on any of the four |
| Lang | `assets/siex/lang/en.json` | `block-pipe-{straight,bend,tjunction,xjunction}*` + `blockdesc-pipe-*` = "High-pressure rolled steel piping. The strongest of the three pipe tiers." |
| Handbook | - | the tier appears in no handbook page. The defs declare a `groupBy` (`BlockPipe.cs:69-74`) but `assets/siex/config/handbook/05-highpressure.json` is the only page hpex ships and it is about the boiler and the engine |

The octagon is real geometry: `straight.json`'s barrel is a 4 × 1 core with four chamfer children rotated ±45° in Z
(`Cube30`/`Cube31` under `Cube3`, `Cube32`/`Cube33` under `Cube4`), and the two end rings at z 0–1 and z 15–16 are
the weld collars (`BlockPipe.cs:248-250`).

Caution: the top steel tier's body texture key is `cast-iron1`, pointing at iiex's cast-iron texture, on all four
rolled shapes as on the cast ones ([cast pipes](cast-pipes.md) § Assets); the `steel` key covers only part of the
mesh. There is no blanket texture override on the shared surface - the three tiers' shapes disagree on key names,
so an override would repaint some segments and miss others (`BlockPipe.cs:76-79`).

---

## Construction

### B5 — there is none

Nothing anywhere outputs `siex:pipe-rolled-straight-*` or any of its three siblings. `src/SteelIndustryExpanded/Recipes/`
contains one file, `Grid/MachineRecipeDefinitions.cs`, emitting two recipes: the Lancashire boiler frame and the
Cornish engine frame. The golden `goldens/siex/recipes/grid/machines.json` is the mod's complete recipe output -
three entries (the boiler, and the engine twice, once per gear code).

iiex at least catalogues four `pipe-*-grid` cost keys against its missing recipes (`IiexRecipeConfig.cs:76-79`,
[cast pipes](cast-pipes.md) § Dead recipe-cost keys). `SiexRecipeConfig.Defaults()` has four entries and none of
them is a pipe (`SiexRecipeConfig.cs:46-56`):

| key | type | match |
|---|---|---|
| `boilerlancashire-rcc` | rcc | `siex:boilerlancashire-*` |
| `enginecornish-rcc` | rcc | `siex:enginecornish-*` |
| `boilerlancashire-grid` | grid | `siex:boilerlancashire-*` |
| `enginecornish-grid` | grid | `siex:enginecornish-*` |

So `/exmod recipes hpex cheap` cannot make rolled pipe cheaper: there is nothing to scale. The catalogue comment
says it is "kept in sync by hand"; the pipe tier is not in it.

### The intended route, and what is missing

The design route is `castbloom → skelp (rolling mill) → conical rolls (bending roller) → pipe segment → bell-weld →
rolled pipe` ([bending roller](bending-roller.md) § the one-machine-four-tools catalogue).

| piece | state | owner |
|---|---|---|
| `skelp` item (8 × 1 × 10, 200 u) | does not exist | [bending roller](bending-roller.md), [wide hall](wide-hall.md) |
| bending roller block | does not exist - no source file, no shape, no lang key | [bending roller](bending-roller.md) |
| the bell-weld step | undecided - stage, grid recipe, or free with the roll? | [bending roller](bending-roller.md) § Open 6 |
| a rolled pipe-part item | does not exist; `assets/editable/shapes/item-cylinder-pipesegment.json` is the only artefact | [cast pipes](cast-pipes.md) |

The settled design places the bending roller in iiex, not smex ([bending roller](bending-roller.md)), so the
machine that makes the high-pressure tier lives one mod below it and B5 cannot be closed inside hpex.

A grid recipe would close B5 immediately and is the smaller of the two options; STATE.md makes B5 release-critical
under D8 ("a player can walk exlib → iiex → iiex → smex → hpex without leaving the spine").

---

## Operation

A rolled segment behaves as any other pipe node - one pool, one medium, uniform temperature, leaks, vents, bursts
and tick order are all [pipe network](../mechanics/pipe-network.md)'s. Specific to this tier: what it couples to,
how much it holds, and how fast it passes.

### What a rolled run connects to

```csharp
public override bool AcceptsNeighbour(Block neighbour) =>
    neighbour is not BlockPipe other || other.JointFamily == JointFamily;
```
`BlockPipe.cs:284-285`.

| neighbour | couples? | why |
|---|---|---|
| `siex:pipe-*` | yes | welded ↔ welded |
| `iiex:pipe-*`, `iiex:pipe-*` | no | welded ↔ flanged |
| iiex valve / pressure valve / outlet / passthrough / passthrough-bend | no | every fitting is a `BlockPipe` subclass, so it inherits its domain's joint |
| iiex tuyere, twin-tub blower | no | same - both are `BlockPipe` subclasses in the flanged family |
| Lancashire steam port, Cornish inlet/outlet, condenser, fluid intake, converter/cowper intake, air blower, smokestack | yes | machine ports are `INetworkConnector`s, not `BlockPipe`s, so the joint test does not apply |

A rolled run is therefore segments plus machine ports and nothing else: no valve to shut it, no outlet to chimney
it, no passthrough to take it through a wall, no pressure valve to gate it. The source calls this outcome correct
and deliberate (`BlockPipe.cs:273-282`); the design says the HP tier needs its own fittings ([pipe
network](../mechanics/pipe-network.md) § Open).

Two live consequences: B6 ([cast pipes](cast-pipes.md) § B6 - the "mandatory" pressure valve is iiex-domain and its
gate ceiling is 5.0, below the Cornish engine's normal engage pressure) and B18 ([pipe
network](../mechanics/pipe-network.md) / [cast pipes](cast-pipes.md) Gotcha 3 - a refused joint does not leak,
because `ClassifyOpenings` counts an open face only when the neighbour block is `air`, `PipeNetwork.cs:652`).
Together they mean a player butts a rolled segment against a cast one, sees no leak, no warning and no particle,
and has two runs that do not talk.

### What a rolled run holds and passes

The rating doubles as the buffer size - a run holds `burst × nodes × LitresPerPipe` (`PipeNetwork.cs`) - and the
tier also caps the run's flow rate ([pipe network](../mechanics/pipe-network.md) owns the throughput model; plain
segments limit, fittings and the tuyere are exempt, `BlockPipe.cs:239-242`):

| tier | burst | litres per node at the ceiling | relative buffer | throughput |
|---|---|---|---|---|
| plated | 2.5 | 75 L | 1× | 50 L/s |
| cast | 5.0 | 150 L | 2× | 120 L/s |
| rolled | 12 | 360 L | 4.8× | 250 L/s |

Those three numbers, plus the joint, are the tier's whole mechanical difference. There is no length advantage and
no pressure drop.

### 12 == 12

`RolledPipeBurstPressure` (`SiexConfig.cs:115`) and `LancashireBoilerMaxOutputPressure` (`SiexConfig.cs:60`) are
the same number, so a rolled main charged by a Lancashire at full choke sits exactly at its own burst threshold;
`TickOverpressureAndBurst` compares with a `0.001` epsilon, so a main pinned at 12.000 is inside the 30-second
grace window, not outside it.

Production is safe by a hair: `TryProduceGas` clamps to `min(maxOutputPressure, MinBurstPressure)` and the boiler
pushes with `maxOutputPressure: InternalPressure`. But the top pipe tier has no headroom above the top boiler: any
relief path that lets the boiler push past its own ceiling, or any upward retune of
`LancashireBoilerMaxOutputPressure` without matching the pipe, destroys one segment every 30 s. Cast pipe and the
Cornish boiler carry the same coincidence at 5.0 ([cast pipes](cast-pipes.md) § Numbers).

### Nothing in hpex consumes rolled pipe

| hpex consumer | what it actually asks for | file:line |
|---|---|---|
| Cornish engine grid recipe | `iiex:pipe-plated-straight-*` ×2 | `MachineRecipeDefinitions.cs:58` |
| Lancashire boiler grid recipe | no pipe at all | `:29-37` |
| Lancashire boiler RCC stages | steel plate, nails, rod, fire brick | `BlockBoilerLancashire.cs:142-163` |
| Lancashire boiler required structure | `iiex:pipe-passthrough-fire-*`, `iiex:pipe-passthroughbend-fire-u*`, `iiex:pipe-outlet-fire-u` (cast) | `:89-95` |
| Cornish engine RCC stages | iron-or-steel plate, rod, nails, fire brick | `BlockEngineCornish.cs:60-93` |

The recipe source records why the engine still takes a plated segment: tier-gating the HP builds waits on those
segments getting craft recipes of their own, and on the hadfield material gate
(`MachineRecipeDefinitions.cs:52-59`). Both are open.

---

## Numbers

### hpex config — `SiexConfig.cs`, `ModConfig/ex_values.json`, section `hpex`

| key | value | file:line | what it does |
|---|---|---|---|
| `RolledPipeBurstPressure` | 12 atm | `SiexConfig.cs:115` | the rolled tier's plain-segment rating; also the run's buffer multiplier, and the ceiling any future hpex pressure valve would inherit |
| `RolledPipeThroughput` | 250 L/s | `SiexConfig.cs:121` | litres per second a rolled segment passes; the smallest across a run caps the run |

That is the whole of hpex's pipe config. Everything else a rolled run uses - `LitresPerPipe`, `GasLeakRate`,
`LiquidLeakRate`, `EvaporationLitresPerDay`, `PipeOverpressureSeconds` - is exlib's, and `ChimneyGasDrawRate` is
iiex's; all are tabulated by [pipe network](../mechanics/pipe-network.md) § Numbers.

Caution: `SiexConfig.cs`'s doc comment calls this "a rolled (hpex) Hadfield-steel pipe segment" and
`SteelIndustryExpandedModSystem.cs` calls the tier "rolled (Hadfield steel)", the lang string calls it "rolled
steel" (`assets/siex/lang/en.json`), and the test fixture maps the material name `"hadfield"` onto the hpex domain
(`PipeTestWorld.cs`). No hadfield material exists in code ([Cornish engine](engine-cornish.md) Gotcha 6); the name
is a design-doc term that appears in comments and a test fixture.

### Hard-coded — not config

| value | file:line | note |
|---|---|---|
| `WeldedJoint = "welded"` | `BlockPipe.cs:258` | the family string hpex registers |
| `FlangedJoint = "flanged"` | `BlockPipe.cs:255` | the default every unregistered domain falls back to |
| `DefaultBurstPressure = 5f` | `BlockPipe.cs:182` | fallback for an unregistered domain - not numerically equal to rolled, so a dropped `RegisterBurst` would be visible here |
| `DefaultThroughput = 120f` | `BlockPipe.cs:215` | fallback throughput for an unregistered domain |
| `CanBurst => GetType() == typeof(BlockPipe)` | `BlockPipe.cs:203` | only plain segments burst or cap a run; hpex ships nothing else, so every hpex pipe block is burstable - the one tier with no exempt fittings |
| collision/selection `0.3125 → 0.6875` core | `BlockPipe.cs:96-97`, `:122-125`, `:151-154`, `:167-170` | identical to the other two tiers |
| max stacks `16 / 8 / 8 / 8` | `BlockPipe.cs:90`, `:104`, `:132`, `:161` | |

### The tier ladder — one row owned here

| tier | domain | burst | throughput | joint | keys | owner of the row |
|---|---|---|---|---|---|---|
| plated | iiex | 2.5 | 50 | flanged | `PlatedPipe*` | [pipe network](../mechanics/pipe-network.md) |
| cast | iiex | 5.0 | 120 | flanged | `CastPipe*` | [cast pipes](cast-pipes.md) |
| rolled | hpex | 12 | 250 | welded | `RolledPipe*` | this page (`SiexConfig.cs:115`, `:121`) |

### Where 12 sits against the machines it exists for

| quantity | value | owner |
|---|---|---|
| Cornish engine engage, low / normal / high | 5 / 6 / 7 atm | [Cornish engine](engine-cornish.md) |
| Cornish engine break | 8 atm | [Cornish engine](engine-cornish.md) |
| Lancashire hand-prime ceiling | 9.6 atm | [Lancashire boiler](boiler-lancashire.md) |
| Lancashire choke | 12 atm | [Lancashire boiler](boiler-lancashire.md) |
| rolled pipe burst | 12 atm | this page |
| iiex pressure-valve gate ceiling | 5.0 atm | [cast pipes](cast-pipes.md) § B6 |

```
2.5      5.0        6   7  |  8      9.6      12
plated   cast     Cornish  | break  prime   rolled == Lancashire choke
burst    burst    engage   |        ceiling
         == valve ceiling
```

Rolled pipe is the only tier above the Cornish engine's normal engage pressure, which makes B5 release-critical
rather than cosmetic ([Cornish engine](engine-cornish.md) § Which pipe can supply it).

---

## Drops

Plain block drops throughout. None of the four defs sets `NoDrops()`; `BlockPipe` overrides no `GetDrops`.

| block | drops | note |
|---|---|---|
| `siex:pipe-rolled-straight-*` | itself | stack 16 |
| `siex:pipe-rolled-bend-*` / `rolled-tjunction-*` / `rolled-xjunction-*` | itself | stack 8 |
| a burst segment | its items, plus a steam puff and a pop; the cell is set to air and the node removed, fracturing the run | [pipe network](../mechanics/pipe-network.md) § 5 |

Salvage is 1:1 and lossless: none of these is a right-click construction, so `RccBrokenDropsRatio`
(`SiexConfig.cs`) does not apply to them even though it is registered for the hpex domain.

---

## Code

| piece | file:line |
|---|---|
| `RolledPipeDefinitions : IExBlockDefProvider` | `BlockNetworkPipe/RolledPipeDefinitions.cs:15-18` |
| burst + throughput + joint registration | `SteelIndustryExpandedModSystem.cs:42-46` |
| `SiexConfig.RolledPipeBurstPressure` / `.RolledPipeThroughput` | `SiexConfig.cs:115`, `:121` |
| `BlockPipe` (segments factory, burst/throughput/joint registries) | `src/ExpandedLib/Blocks/Networks/BlockPipe.cs:23`, `:49`, `:175-244`, `:246-287` |
| `BlockEntityPipe` | `src/ExpandedLib/Blocks/Networks/BlockEntityPipe.cs` |
| `HpMachineDomainMigration : IBlockCodeMigration` | `BlockMigrations/HpMachineDomainMigration.cs:34`, `GetRemaps` `:46-59` |
| the migrator that applies it | `ExpandedLib/Blocks/Migrations/BlockMigrationModSystem.cs` |
| goldens | `test/SteelIndustryExpanded.Tests/goldens/siex/blocktypes/pipes/{straight,bend,tjunction,xjunction}.json` |

### `HpMachineDomainMigration`

The Lancashire and the Cornish shipped as `iiex:` blocks, and before the `ppex → iiex` rename as `ppex:` ones, so a
placed machine in an older save carries a code that no longer resolves. The migration names its two block bases
literally (`ExtractedBases = ["boilerlancashire", "enginecornish"]`, `HpMachineDomainMigration.cs:41`) and emits
both historical domains for each:

```
(iiex:<path>  →  siex:<path>)
(ppex:<path>  →  siex:<path>)
```
`:46-59`, via `CodeRelocation.Remap` with `legacySideWords` (both machines carried word-spelled sides in older
saves). Both legacy domains are emitted because iiex's rename migration only covers blocks that are still iiex, so
it never produces an `iiex:boilerlancashire-*` hop for a chain to follow; emitting `ppex:` directly is the only
path for a pre-rename world.

Do not widen this migration to enumerate the hpex domain: `hpex:pipe-*` uses the very paths iiex uses for its live
cast pipes, so a domain-wide enumeration would claim `iiex:pipe-*` as a legacy source and rewrite every placed cast
pipe into a rolled one. `ReleasedCodeCoverageTests` fails if any migration declares a live code as a legacy source.

### Tests — `test/SteelIndustryExpanded.Tests/`

| file | pins |
|---|---|
| `Networks/RolledJointTests.cs` | five coupling cases (iiex↔iiex, iiex↔iiex, iiex↔iiex both ways, hpex↔hpex) · four refusal cases (hpex↔iiex and hpex↔iiex, both orderings - the symmetry an `AcceptsNeighbour` implementation is required to have) · the three declared joint families · a machine port is not a pipe and is unaffected |
| `Fixtures/PipeBurstParityTests.cs` | `PipeTestWorld.RolledTierBurst` (12) equals `SiexValues.RolledPipeBurstPressure` - the shared iiex fixture cannot reference hpex, so a retune would otherwise leave every HP test running against a stale ceiling and still passing |
| `Definitions/SiexDefinitionGoldenTests.cs` | the four pipe defs reproduce their goldens; the golden set exactly covers the defs; every shape reference resolves to a shipped file |

`RolledJointTests` is hosted here because it is the only suite that can see all three tiers.

Caution: `The_refused_joint_reads_as_an_open_end_not_a_seal` (`:92-108`) cannot detect what it claims. It butts one
hpex cell against one iiex cell along +Z, produces gas, ticks, and asserts `net.State!.OpeningsCount > 0`. Both
pipes are `ns`, so the network at `(0,0,0)` also has a north face against unplaced world, which `TestWorld` answers
with `game:air`. `ClassifyOpenings` only tallies a face when `neighbour.FirstCodePart() == "air"`
(`PipeNetwork.cs:652`), so the count that makes the assertion pass comes from the free end, not from the refused
joint, and it would pass identically if the joint were accepted (two nodes, two air ends). B18 is precisely the bug
it was written to catch. A real assertion needs both ends sealed with `IiexScenes.Cap`, at which point it fails.

---

## Gotchas

1. **B5: the rolled tier is creative-only.** Four blocktypes, thirty variants, four runtime shapes, four
   editable shapes, a rating, a throughput, a joint, a lang block, a creative tab and a migration - and no
   recipe, and not even a cost-catalogue key to hang one on. See [Construction](#construction).

2. **A rolled run has no fittings at all** - no valve, no pressure valve, no outlet, no passthrough, no
   tuyere, no blower. Everything on that list is a `BlockPipe` subclass in the flanged family
   (`BlockPipe.cs:273-285`). That produces B6 ([cast pipes](cast-pipes.md) § B6) and makes the HP main
   unshuttable, unventable and ungateable. The fix is hpex-side and is two registrations plus four defs: an
   hpex-domain valve/pressure-valve pair resolves both halves of B6 at once, because both the joint family
   and the gate ceiling are read from the block's own `Code.Domain`.

3. **A refused joint does not leak - B18.** `ClassifyOpenings` counts an open face only when the neighbour is
   air (`PipeNetwork.cs:652`), so a welded segment butted against a cast one produces no leak, no warning and
   no particle. Owned by [pipe network](../mechanics/pipe-network.md) § 5 /
   [cast pipes](cast-pipes.md) Gotcha 3; recorded here because it is what makes B3-through-B6 invisible on an
   HP line, and because the one test that would have caught it does not (see
   [Tests](#tests--testhighpressureexpandedtests)).

4. **Every hpex pipe block is burstable.** `CanBurst => GetType() == typeof(BlockPipe)`
   (`BlockPipe.cs:203`) exempts every subclass and hpex ships no subclasses, so there is no non-bursting hpex
   block to break up a run. `MinBurstPressure` on a pure rolled run is always 12, never `float.MaxValue`.

5. **"Hadfield" is a documentation word with no code behind it.** Comments in `SiexConfig.cs` and
   `SteelIndustryExpandedModSystem.cs` and one test fixture (`PipeTestWorld.cs`) use it; the shipped lang
   string says "rolled steel"; nothing in `src/` defines it. See [Cornish engine](engine-cornish.md)
   Gotcha 6 and `../materials.md`.

6. **`overview.md` files rolled pipe under smex** - its build order says "Hadfield steel + rolled pipe
   (smex) → HP boilers/engines (hpex)" (`overview.md:108`). The blocks are hpex-domain
   (`RolledPipeDefinitions.cs`), and the machine that would make them is iiex's bending roller
   ([bending roller](bending-roller.md)). The mod table also still marks hpex "planned"
   (`overview.md:71`); it ships.

7. **The rolled tier appears in no handbook page.** The defs declare a `groupBy` across all four codes
   (`BlockPipe.cs:69-74`), and hpex ships exactly one handbook page, about the boiler and the engine. No
   shipped handbook text anywhere mentions 12 atm or the welded joint.

8. **A dropped `RegisterJoint` would fail silently; a dropped `RegisterBurst` would not.** Joint default
   `FlangedJoint`, burst default 5 (`BlockPipe.cs:182`, `:268-271`). The joint is the one that matters, the
   one whose loss is invisible, and the only registration `RolledJointTests` covers.

9. **The tier is the domain, so a rolled segment carries no material variant.** There is no `-iron` /
   `-steel` axis; `PipeMigration` (iiex) lands legacy `ppex:pipe-*-steel` codes on iiex plated, not on any
   higher tier ([cast pipes](cast-pipes.md) § Migrations), so no upgrading world ends up holding a rolled
   segment either.

---

## Open

- B-class blocker: write a recipe. A grid recipe against a new `pipe-*-grid` cost key closes B5 today; the
  designed route (skelp → conical rolls → bell-weld) needs the bending roller, the skelp item and a decision
  on the weld step, none of which exist ([bending roller](bending-roller.md) § Open).
- hpex needs its own fittings: a welded valve + pressure valve (rating 12) closes B6 in both halves with no
  core change, and an outlet + passthrough makes an HP main routable through a wall and cappable with a
  chimney.
- B18 should be fixed in the core, and `RolledJointTests` made able to see it - cap both ends of the two-cell
  rig and the current assertion becomes meaningful.
- Decide what "hadfield" is (Gotcha 5): a real material with items and a gate, or a word to delete from the
  comments, the fixture and `../materials.md`.
- 12 == 12 leaves the top tier no headroom over the top boiler (see [Operation](#operation)). Either raise
  the pipe or lower the choke; one retune in the wrong direction turns a working HP main into a segment
  shredder.
- The tier has no handbook presence. It needs a paragraph stating 12 atm, 250 L/s, welded-joins-only-itself,
  and that there are no fittings yet.
