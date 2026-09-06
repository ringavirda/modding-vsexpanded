# Conventions, Units & Shared Rules

The single source of truth for units, global invariants and network semantics. Every mod doc cites this
file rather than restating these. The shared simulation models are not claimed here - each lives with its
owner page under [mechanics/](mechanics/).

---

## Units

| Quantity | Unit | Notes |
|---|---|---|
| Metal mass | **units (u)** | 100 u = 1 vanilla ingot |
| Fluid/gas volume | **litres (L)** *(live)* | A pipe segment holds 30 L *(live)*; a run's capacity = node count × 30 L. Litres everywhere, never m³ |
| Water → steam | **1 : 16** *(live)* | 1 L water boils to 16 L steam (`SteamExpansionFactor`) |
| Mechanical power | **MP** *(live)* | Vanilla MP network; constant-power generator model (see iiex) |
| Steam/water flow | **L/s** | Per-tick flow, EMA-smoothed for the throughput readout *(live)* |
| Temperature | **°C** | One network-wide pipe temperature *(live)*; molten canals are per-cell |
| Pressure | **atm** | 1 atm = ambient. LP steam ≤ ~4–5 atm; HP ~8–12 atm (tunable) |
| Steam-engine efficiency | **0.75** *(live)* | Output pressure = inlet × efficiency |

---

## Global invariants (named rules)

Referenced by name from the mod docs.

- **R1 - Single medium.** A pipe network carries one medium at a time (gas or water) with a
  unified Volume/Temperature/Pressure/MediumType pool *(live)*. Air, steam, exhaust (and the chemistry
  fractions) are gas media; water and the liquid fractions are liquid media.
- **R2 - Declared recovery, nothing hidden.** *(revised 2026-07-27; see § metal recovery below)* Every step
  declares what fraction of its input it returns, and that fraction is a fixed readable number, never a
  random roll. Transport, casting, forming, distillation and chemistry are 1:1. Reduction and refining are
  not: each declares a recovery fraction, and the shortfall must become a by-product the player can see and
  use, never a silent drain. Ore carries its true content rather than one process's share of it, so a better
  process gets more metal from the same rock.
- **R3 - Molten is per-cell.** Molten metal lives in per-cell molten canals (each block owns its
  metal, flows cell to cell) *(live)*. The ladle is the only block that merges canals and mixes
  metals. The ladle half is designed, not live - no `Ladle` type exists in `src/`, so every alloying
  rule downstream of it is currently unreachable. See [ladle](machines/ladle.md), which also records that a
  ladle must pull by code rather than join the graph, and therefore needs no exlib change.
- **R4 - Powered forming only.** Billets and profiled stock cannot be worked on a vanilla anvil - only on
  the [rolling mill](machines/rolling-mill.md) (iiex, MP) or the [steam hammer](machines/steam-hammer.md)
  (iiex, LP steam).
- **R5 - Gate efficiency, not possibility.** The heat-balance and distillation models gate speed and
  efficiency, never hard-block a process: there is always one guaranteed path (high-coke + cold blast
  melts at the iron tier). Every threshold is config-tunable.
- **R6 - Stock carries its mass.** Stock/billet items carry remaining mass in a unit-count stack
  attribute, so any cut or divide step is exact arithmetic.
- **R7 - Nothing is hidden.** *(revised 2026-07-29)* Every machine's state must be legible to a player
  standing in front of it: temperature, pressure, what is in the pipe, how far through a cycle it is.
  Operation is in-world and verb-based - the player works the machine, not a menu. Windows are allowed
  where the interaction genuinely needs one (the design table and boring machine both have full windows,
  and more may follow), and every machine carries status UI. What the rule forbids is state that cannot be
  seen: no hidden timers, no invisible buffers, no "it is doing something, trust it".
- **R8 - Mechanical energy is conserved.** *(Added 2026-07-29; the network it governs has been live for
  months.)* A flywheel is a reservoir, not a battery: it stores `E = ½Iω²` in joules and every joule out
  came from a joule in, less friction. Torque balance is `I·dω/dt = τ_drive − τ_load − τ_friction`. No node
  may mint energy, and a run whose total inertia is zero has no speed rather than infinite speed. Owned by
  [mp-energy](mechanics/mp-energy.md). Two clauses of the original proposal are not implemented -
  governor throttling and over-speed burst (`IsOverSpeed` has zero call sites).
- **R9 - Mass is derived from the shape.** *(Added 2026-07-29.)* 1 voxel³ = 2.5 units. Every mass in the
  suite is the density rule applied to a drawn shape, not a picked number, which is why masses divide exactly
  and why the ladder's crop points land on integers. Derived by measuring vanilla (ingot 42 vx³/100 u, rod
  40/100, plate 81/200 - mean 2.45, within 2.5 %). Owned by [density rule](mechanics/density-rule.md).
  Shipped code does not yet obey it everywhere: the pig (375 u) does; the cast plate, stock forms and mold
  cavities land with the settled mass batch ([economy-landing](items/economy-landing.md)).
- **R10 - A code is its family, then its member.** *(Added 2026-08-03.)* Every block code reads
  `family-member-{variant}…`, and the rendered code is its asset path with `/` for `-`, so a block's
  name, its file and its lang key are one string spelled three ways. Families are singular, a code never
  stutters against its own `type` state, `-` is a segment boundary and never a word break, and `side`
  (vanilla words) means the player fixed the facing while `orientation` (letter tokens) means the network
  decides it. Plus one rule with teeth: a path segment may only become a folder if it is not itself a
  block - `ExRecipeCosts` applies every catalogue entry in sequence, so a parent's wildcard silently
  swallows its children's costs. Owned by [naming](mechanics/naming.md), which carries the verified
  inventory of what still breaks each rule. iiex is fully converted; what remains is the iiex/hpex/smex
  machine folders.

---

## Block-size vocabulary

- **block** - a single 1×1×1 block.
- **megablock** - occupies more than one cell via the **filler-block** mechanic; often a
  RightClickConstructable (RCC).
- **multiblock** - a structure the player builds by hand in a specific shape, guided by an in-world
  **projection**. Blocks and megablocks can be parts of a multiblock.

A block can be both - boilers are RCC megablocks whose construction is also gated by a multiblock
projection.

---

## Networks

Five transport-network families, all on exlib's shared block-network graph. The network logic lives
in exlib; the pipe blocks are per-mod tiers.

Three are registered graph types in code (`pipe`, `molten`, `mpenergy`); vanilla MP and the electrical grid
are not exlib networks - vanilla MP is the engine's own, and elex's grid is deferred.

- **Mechanical energy (`mpenergy`)** *(live)* - the flywheel-buffered energy reservoir the heavy machines draw
  from: joules, not torque-at-a-speed. A vanilla waterwheel or windmill is bridged in at the flywheel's hub
  face, and in iiex the player swaps that producer for a steam engine while the network stays identical.
  Governed by R8. Owned by [mp-energy](mechanics/mp-energy.md); blocks ship in iiex under
  `BlockNetworkEnergy/`.
- **Molten-canal** *(live)* - per-cell metal, flows cell→cell, end caps recomputed on tesselation. The
  ladle is the only merge/mix point (R3). Owned by [molten-network](mechanics/molten-network.md).
- **Pipe (gas or water)** *(live)* - single medium per network (R1). Used for water, steam, compressed
  air, exhaust, coal gas and the chemistry fractions. Three material tiers of pipe block, ascending
  burst pressure: plated (iiex - hammered from iron plates, 2.5 atm), cast (iiex - from cast
  pipe-parts finished on the [boring machine](machines/boring-machine.md), 5 atm), and rolled
  ([hpex](machines/rolled-pipe.md) - 12 atm, curled from skelp on iiex's
  [bending roller](machines/bending-roller.md)). Each tier ships its own model at
  `{domain}:pipes/*`.
  Connectors read the adjacent cell; valves sever/flow; pressure valves overflow.

  Tiers do not all interconnect, and the rule is the joint, not the pressure *(live)*. Plated and cast
  pipe are square in section and bolted through flanges, so a plated run and a cast run join freely and
  the weakest segment caps the whole run's burst pressure. Rolled pipe is octagonal and welded, with no
  flange to bolt to, so an HP run couples only to another HP run: an HP main cannot be fed with cheap
  plated pipe.

  Implemented as a joint family registered per domain (`BlockPipe.RegisterJoint`) and enforced in
  exlib's `BlockNetworkNode.AcceptsNeighbour`, checked at the single point both the graph traversal and
  the open-end scan pass through. Two consequences:
  - A refused joint is not a seal. The unmated face reads as an open end, so the run leaks, which is
    what tells a player the two lines are not plumbed together.
  - Every fitting (valve, outlet, passthrough) is a `BlockPipe` subclass, so the rule blocks a rolled
    run from the cast tier's fittings too. The HP tier needs its own fittings, and until it has them a
    rolled run is segments and machine ports only. Machine ports are not pipes and are unaffected.
- **Mechanical power (MP)** *(live)* - vanilla MP network; drives the mechanical blower (iiex) and
  mechanical pump (iiex) as well as engine sub-machines.
- **Electrical (AC + DC)** - the elex tier (planned).

### The per-mod project skeleton

Every content mod is laid out the same way, so a folder that exists in one mod and not another means "this
mod has no such content", never "this mod files it somewhere else".

**Core folders - always present:**

| Path | Holds |
|---|---|
| `<Name>ModSystem.cs` (root) | the mod entry point |
| `<Mod>Config.cs` [+ `<Mod>RecipeConfig.cs`] (root) | the `ExConfigRegister` types |
| `LegacyUsings.cs` (root) | the multi-version shim usings |
| `BlockMigrations/` | save migrations, one file per subject |
| `BlockStructures/<Machine>/{Blocks,BlockEntities}/` | one folder per machine; a shared base for the family sits at the machine-folder root, next to its block entity base, not inside `Blocks/` |
| `Patches/` | Harmony patches on vanilla |
| `Recipes/<Type>/` | code-first recipe providers (see below) |

**Content-gated folders - present only when the tier has that content:** `BlockNetwork<Kind>/` (only
for a network-owning mod - iiex owns `BlockNetworkPipe` and `BlockNetworkMolten`; iiex ships pipe
fittings under its own `BlockNetworkPipe/` but does not own the graph), `Items/`, `Molds/`,
`Commands/` and `Preferences/` (only with a client sub-feature), `Compat/` and `Helpers/` as needed.

**Naming rules that fall out of this:**

- A single-file top-level folder is a smell - the file belongs beside its subject. `SlagPath/` became
  `BlockStructures/Products/`, `Rendering/` became the machine folder its renderer serves.
- Renderers are co-located with their consumer, never in a per-mod renderer bucket. Where a mod does
  need one (exlib), it is `Renderers/`, never `Rendering/`.
- An abstract base belongs at the machine-folder root, beside its block-entity counterpart -
  `Furnaces/BlockFurnaceCoreBase.cs`, not `Furnaces/Blocks/`. `Blocks/` is for the concrete variants.
- `Compat/` is a legitimate growth bucket even at one file: it is a category, not a stray.

### Where a code-first definition lives

A block/item def lives next to the class it defines (`BlockBurdenmaker.Definitions(...)` sits in
`BlockBurdenmaker.cs`). A def with no class of its own - a family emitted from a table, like
`SlagPathDefinitions` or `ToolMoldDefinitions` - lives in the area folder of its subject
(`BlockStructures/Products/`, `Molds/`), not in a per-mod `Blocks/` bucket.

### Where a recipe provider lives

Recipes have no class of their own to sit next to, so a recipe file is addressed by its type first:

```
src/<Mod>/Recipes/<Type>/<Feature>RecipeDefinitions.cs      namespace <Mod>.Recipes.<Type>
```

The recipe type is the folder, the feature is the file, and the mod name appears in neither - the
namespace already carries it, and identical class names across assemblies are fine. This is the same
shape the shipped assets use (`recipes/grid/blastfurnace.json`), so the two trees read alike:
`Recipes/Grid/FurnaceRecipeDefinitions.cs` emits `grid/blastfurnace` and `grid/cupola`.

Split a provider along its mod's own `BlockStructures/` areas, not by recipe count. Ingredient captures
used by one provider stay private to it; captures two providers must agree on go in
`Recipes/RecipeIngredients.cs`, because a wildcard that drifts between two files is a bug nobody sees (the
shared cobblestone capture is what keeps the hand-patterned and diagram-crafted molten canals yielding the
same `{rock}` variants).

### Where the authoring copies of assets live (`workbench/`)

`workbench/shapes/*.json` are not duplicates of the shipped shapes - they are the same models
in the format the VS Model Creator needs. The editor requires texture paths that are absolute and
domain-less; the game requires domain-prefixed asset paths. The two cannot be one file, so the
editable copy is the hand-edited source and the domain copy is what ships. Do not "de-duplicate" them.
The same holds for `workbench/textures/*.psd`, which export to the shipped PNGs.

**The three kinds of tree under `assets/`**, none of which is a mod domain in the same sense:

| Tree | Ships? | Owner | What it is |
|---|---|---|---|
| `assets/<mod>/` | yes | that mod (`<AssetDomain>` in its csproj) | the mod's own domain — blocktypes, shapes, textures, lang, config |
| `workbench/` | **no** | source-only | authoring copies: VS Model Creator shapes (domain-less absolute texture paths) and `.psd` sources |
| `mods/iiex/assets/game/` | yes, into `game:` | **exactly one mod** | overrides on vanilla's own domain, chiefly lang strings |

`mods/iiex/assets/game/` writes into vanilla's namespace, so two mods shipping the same `game:` key silently fight,
and which wins depends on load order. It therefore has a single owner in this repo rather than being a
per-mod convenience.

### Where handbook prose lives

A handbook page is authored as HTML in `docs/<mod>/handbook/NN-*.html` and ships as one long string
under a lang key in `assets/<mod>/lang/en.json`. The HTML is the source.

The copy across is not manual. `HandbookSync` (in `ExpandedLib.Testing`) joins the two trees on the `NN-`
ordering prefix, not the file name, so slugs are free to differ; `HandbookParityTests` fails on drift,
`EXLIB_WRITE_HANDBOOK=1` imports HTML → lang, and `EXLIB_EXPORT_HANDBOOK=1` writes back the other way when
the good copy turned out to be the shipped one. Titles stay hand-authored (a few words, no HTML source) and
translations are untouched: the sync changes values, never the key set, so the lang-parity guard still holds.

Line breaks in an authoring file are wrapping, not content - VTML collapses whitespace like HTML, and
so does the sync - so re-wrapping a page is a no-op. Attribute quotes are written `\"` because the files
predate the tooling and were meant to be pasted straight into JSON; the sync un-escapes them.

### How exlib is laid out (ruled 2026-09-05; supersedes the `Blocks/`-vs-top-level rule)

A top-level folder under `mods/exlib/src/` is something a modder is doing, and it is one namespace.
Sub-folders organise files; they never add a namespace segment. A consumer needs one `using` per
activity, and a machine mod needs about six in total.

| Folder | Namespace | A modder who is... |
|---|---|---|
| `Registries/` | `ExpandedLib.Registries` | registering blocks, items, behaviours, commands, preferences, recipe profiles; asking about other mods; patching with Harmony |
| `Config/` | `ExpandedLib.Config` | declaring a config class, its ranges, migrations and live editing; syncing it to clients |
| `Definitions/` | `ExpandedLib.Definitions` | writing block, item, recipe and layout definitions in C# |
| `Blocks/` | `ExpandedLib.Blocks` | writing a block entity: declared state, orientation, right-click construction |
| `Migrations/` | `ExpandedLib.Migrations` | renaming or removing codes in old saves, healing lost block entities |
| `Structures/` | `ExpandedLib.Structures` | building a multiblock or megablock |
| `Machines/` | `ExpandedLib.Machines` | building a machine that ticks, with ports, readiness and stations |
| `Networks/` | `ExpandedLib.Networks` | building a connected network: the graph model and the engine-facing nodes together |
| `Catalogues/` | `ExpandedLib.Catalogues` | shipping or extending data catalogues: processes, materials, liquids, storage, their loaders, reports and contributors |
| `Checks/` | `ExpandedLib.Checks` | verifying content in the game or in a test |
| `Helpers/` | `ExpandedLib.Helpers` | everything content-neutral that saves a few lines: orientation, meshes, inventories, units, rendering |
| `Legacy/` | `ExpandedLib.Legacy` | supporting 1.20 and 1.21 from one source tree |
| `industry/<Pack>/` | `ExpandedLib.Industry.<Pack>` | reusing the family's content layer: pipes, molten, mechanical power, metals, heat. Its own project beside `src/`, shipping `exlib.industry.dll` inside the same mod folder |

Rules with teeth: a folder that would hold one file is not a folder (the file goes beside its
subject); a sub-folder appears at four files; a type's folder is decided by the activity that reaches
for it first, not by its base class - `BlockNetworkNode` sits in `Networks/` beside `BlockNetwork`
because a modder building a network wants both. The retired rule split each family across a
model folder and a `Blocks/` shell folder and asked consumers to import both; the split is gone.

`mods/exlib/testing` is one namespace, `ExpandedLib.Testing`, laid out the same way: `World/` (the
fake world and its blocks), `Scenes/` (the layout DSL), `Rigs/` (drivers for machines and
structures), `Doubles/` (stand-ins), `Checks/` (the validators), `Repo/` (this repository's own
history and paths). `mods/exlib/tests` mirrors `mods/exlib/src` folder for folder.

### Catalogue registry verbs (ruled 2026-09-06)

Every catalogue registry (`ProcessRouteRegistry`, `ProcessJobRegistry`, `BayOccupancyRegistry`,
`MaterialRoleRegistry`, `MetalRegistry`, `ExLiquids`) reads the same four verbs the same way:
`Register` declares one entry from code, `Contribute` merges a whole parsed file's worth and reports
its clashes, `Load` (always on the loader, never the registry) reads assets and repopulates the
registry, and `Clear` empties it; `Contributors` is the static hook a `Load` re-runs after its own
read, so a `Register` from `Start` survives the clear that precedes every reload. No catalogue
registry declares a public `Add*` or `Load*` member of its own - `CatalogueNamingTests` guards it.

The medium set is data-driven *(live)*: each medium is a `LiquidDef` in exlib's liquid taxonomy
(code, gas/liquid phase, merge priority, boil/condense points). A mod adds a medium by shipping one
JSON entry; the built-in four (Air / Steam / Exhaust / Water) reproduce the old hardcoded behaviour
exactly. A later mod (Industrial Homestead) can register its distillation fractions as further media
that ride the same pipes, condensers, valves and tanks - the taxonomy is open by design, but
none of those media are this suite's to ship.

---

## Shared simulation model — blast demand comes from the burden

Owned elsewhere, cited here for the map:

- The burden → blast-demand mapping is owned by [heat-balance](mechanics/heat-balance.md)
  (`RequiredBlastPressureFor` / `TuyereDrawFor`).
- Burden composition is owned by [burden](items/burden.md): a burden is ore + flux only, three
  bands on the flux axis, and coke is charged as its own bands.
- Fuel-as-carbon (coke 2, charcoal 1) is owned by [fuels](items/fuels.md).

Pipe burst ratings double as capacity (`burst × pipes × litres-per-pipe`), so the plated tier is both
the low-pressure tier and the small-buffer one: plated 2.5, cast 5, rolled 12.

---

## The furnace axes are a class tree *(decided and built 2026-08-02)*

Of the furnace's three axes (heat source, charge store, product), the charge-store axis is a class
tree and the other two are parameters. The furnace hierarchy carries two abstract classes under the core:

```
BlockEntityFurnaceCore              heat ledger · structure · HUD · draught · damper
├── ShaftFurnace                    burden column · blast · tuyeres · full cold-charge loss
│   └── BlastFurnaceCold · BlastFurnaceHot · Cupola
└── FireboxFurnace                  plain fuel · natural draught · no blast
    └── Puddling · Heating (· Crucible, later)
```

The parameter form works where a default can be true rather than stubbed, which is how the blast and
product axes read. The charge store axis is not like that: it is four parallel knobs
(`ShaftHoldsLayeredCharge`, `ReadChargeMix`, `MinChargeToIgnite`, `AcceptedFamilies`) that must be set
consistently, with nothing enforcing it. Two shipped defects came from that:

* B8's first and third causes - the puddling furnace inherited a 320-unit ignition threshold onto a
  one-cell firebox, and a burden-only charge read that scored a firebox of coke at zero. It could
  never light, at any temperature ([puddling-furnace](machines/puddling-furnace.md#gotchas)).
* The `ShaftHoldsLayeredCharge` opt-in trap - a hearth subclassing a leaf shaft furnace silently
  inherits `true` and starts writing charge columns. Guarded until this landed only by a ~25-line warning
  comment on the core.

A firebox cell holds 16 u, not 128. No hearth layout places a hopper; the only route fuel into a
firebox is the vanilla coal pile, whose `BlockEntityCoalPile.MaxStackSize` is a hard 16 on 1.20, 1.21 and
1.22 alike (read at IL level on all three). So the one-cell puddling firebox holds 16 u and the two-cell
reheat firebox 32, which is why retargeting the threshold at the cupola's 160 left both hearths unable to
light, and why the threshold is derived from the firebox's own cell count
(`FireboxCellCount × FireboxMixPerCell`, default 12/cell) rather than a fixed constant no single value
could satisfy for two fireboxes of different sizes.

The ignition threshold is also the cold-charge-loss denominator (`chargeLoss = BfChargeLossFull ×
clamp(mixCount / MinChargeToIgnite)`), so moving it moves the heat balance with it. Deriving it means a
full firebox pays the full 310 °C penalty on both hearths, where a brim-full reheat firebox previously paid
62 °C of it. The term is a burden-column idea; a per-branch replacement is scheduled with
`NaturalDraughtFor(courses, damper)` and a reverberatory transfer-loss term
([crucible-furnace](machines/crucible-furnace.md)), not here.

What the class tree does not absorb: the product axis. ~237 lines of the shaft furnace are molten pool and
taps, orthogonal to shaftness - the open hearth is a reverberatory furnace that taps molten steel
([open-hearth](machines/open-hearth.md)), i.e. a firebox that pours. Product stays a parameter, which the
core already supports: six molten virtuals with defaults that are true rather than stubbed. A later phase
replaces those 237 lines with the composable `BEBehaviorMoltenCell`, after which any furnace on either
branch can compose a pool.

The crucible furnace is not an example of this. The player lifts the white-hot pot with tongs and pours it
into a mould by hand ([crucible-furnace](machines/crucible-furnace.md)), so the melt lives in the item, not
the block entity: no pool, no taps, no `DrainProducts`. Every firebox machine that exists or is near-term
(puddling, reheat, crucible) is non-pouring.

Cost, as built: nothing in `src/` casts to `BlockEntityBlastFurnace` - not iiex, smex, iiex, hpex or
exlib; every component talks to `BlockEntityFurnaceCore`. No golden moved: goldens record only the concrete
leaf's `entityClass`, which did not change. `BlockEntityBlastFurnace.cs` became `BlockEntityShaftFurnace.cs`;
puddling dropped its always-zero molten pool, so its save tree is now byte-identical to the reheat furnace's
and one `ShaftColumnsTests` assertion moved with it.

The invariants are enforced. All four charge-store answers are declared on a branch class and `sealed`
there - `ShaftHoldsLayeredCharge`, `AcceptedFamilies` and `MinChargeToIgnite` on both branches,
`ReadChargeMix` per branch. A leaf restating any of them is a compile error in every mod, not a test
failure in one assembly. Two reflection guards back that up across the whole loaded assembly closure
(`mods/iiex/tests/Invariants/FurnaceBranchGuards.cs`, invoked from the iiex and smex
suites): the branch owns the flag, and no firebox may ask for more fuel than `cells × MaxStackSize`.

Caution: some design files still cite `BlockEntityBlastFurnace.cs:NNN` - a filename that no longer
exists (the class became `BlockEntityShaftFurnace`, and the shared logic lives on
`BlockEntityFurnaceCore`). Treat any such citation as stale and read the current source.

---

## Shared material model — refractory tier **is** refractory chemistry *(decided 2026-08-02)*

Vanilla ships three refractory brick tiers and presents them as a quality ladder. This suite reads them as a
chemistry axis instead:

| Tier | Vanilla additive | Chemistry | Role |
|---|---|---|---|
| **tier1** | crushed **quartz** + bauxite | SiO₂ — **acid** | siliceous lining; the acid Bessemer |
| **tier2** | + crushed **olivine** | (Mg,Fe)₂SiO₄, magnesia-bearing — **basic** | the basic/Thomas lining; the open hearth |
| **tier3** | + crushed **ilmenite** | FeTiO₃, titania, amphoteric — **neutral** | use-anywhere premium |

The vanilla recipes already carry the reading, so no new recipe is required: quartz is silica and
unambiguously acid; olivine refractory brick is a real product classified basic, used in steel ladles for
that reason; only tier3 is a stretch, and titania-alumina systems are used as neutral linings.

### Where it changes an outcome — and where it deliberately does not

Tier is not a gate everywhere. Chemistry matters only where the lining reacts with the slag. Everywhere
else brick is brick, and any tier anywhere remains the rule.

| Machine | Tier matters? |
|---|---|
| [bessemer](machines/bessemer.md) | **yes** — the whole point |
| [open-hearth](machines/open-hearth.md) | yes — it is basic practice by design |
| the [ladle](machines/ladle.md) | **yes — pinned tier2**, and it is the one entry here that is a *ruling* rather than a consequence (see below) |
| blast furnace · cupola · coke oven · crucible furnace · reheat · puddling | **no** — the brick is an enclosure, not a reagent |
| the [crucible pot](machines/crucible-furnace.md) | **no** — but **not fireclay either**: a pot is a vessel, not a lining, and fired clay caps at 1200 °C, so it is a *refractory pot item* |

The ladle is an exception and must not be "fixed" back to any-tier *(ruled 2026-08-06)*. Its lining reacts
with nothing - it merges metal canals and gates alloys, while slag has its own taps - so by the rule above
it should be any tier. It is pinned to tier2 (basic) because a steel ladle is basic-lined. The pin buys
authenticity, not a mechanic.

Fire clay is not an option anywhere metal is held above 1200 °C, a trap two vessels have fallen into.
Fired clay's ceiling is vanilla's own `maxHeatableTemp: 1200` plus this mod's at
`IiexConfig.cs:65`; pig iron is 1482 °C and steel is higher. It is why the crucible pot had to become a new
refractory item, and why the ladle's original `+ fire clay (the refractory lining)` costing was a lining
that melts. What a vessel is lined with is decided by heat first, chemistry second, and cost last.

The payoff is Gilchrist-Thomas (1878): an acid lining cannot remove phosphorus, a basic one can. Today
[bessemer](machines/bessemer.md):59-60 fixes the converter as acid by fiat and records the basic/Thomas
process as "deliberately absent", while [open-hearth](machines/open-hearth.md):443 fixes that machine as
basic the same way, with different slag rates already falling out (10 % basic vs 6 % acid, `:295`). With
tier-as-chemistry those become consequences of what the player lined the vessel with, and the Thomas route
opens: line it basic, charge phosphoric ore, get good steel plus basic slag (historically sold as fertiliser).

The implementation cost is close to nil. The Bessemer's RCC already has a lining stage -
`Root/InputLining`, stage 7, currently 12 fire clay ([bessemer](machines/bessemer.md):218). Changing that
input to a tiered refractory brick turns an existing construction step into the acid/basic choice. No new
stage, no new mechanic, no new material.

**Open:** whether more historically-shaped brick recipes are worth adding later (ganister, dolomite, tarred
magnesite). Not needed for the reading above - recorded only so the idea is not lost.

---

## Shared simulation model — dynamic heat balance

The per-tick heat balance - no hardcoded max temperature, contributors always legible per R7, gating
efficiency per R5 - is owned by [heat-balance](mechanics/heat-balance.md) *(ceded 2026-08-07)*.

## Shared simulation model — metal recovery & material loss

*Designed 2026-07-27; the anchor and the furnace mass balance are live, the recovery ladder and roasting
are designed.*

Vanilla's 5 units per nugget is not the ore's iron content, it is a bloomery's share of it. A real bloomery
threw 40–60 % of the iron into its slag, so the rock holds roughly twice what vanilla hands the player, and a
blast furnace recovering more of it is not inflation.

### The anchor and the ladder

The anchor's derivation and the ore-to-metal recovery ladder are owned by
[metal recovery](mechanics/metal-recovery.md); the fractions are stated once there and cited everywhere
else. End-to-end after a realistic refining loss the chain still lands ~1.65× the bloomery through
puddling at ~90 %, or the same through the Bessemer at 90 % - as steel, which the bloomery cannot make
at all.

### Two rules that keep losses honest

1. A loss must go somewhere. Never a silent drain. Iron lost in puddling becomes tap cinder; iron
   lost under the rolls becomes mill scale; the blast furnace's becomes slag. All three are
   `fettlestock`, so a shop's losses feed the next heat's fettling - a loop, not a leak. *(The fettle
   recipe is already built this way: three parts, each any of crushed ore, tap cinder or mill scale. A new
   works pays in ore; a running one feeds itself.)*
2. A loss must move with something the player controls. Temperature, coke ratio, fettle quality,
   whether the ore was roasted. A loss that varies is a mechanic; a flat percentage cannot be optimised
   against.

### Which tier moves which number — recovery is spent **once**

Recovery is a bounded resource and therefore a bad axis to stack upgrades on: it cannot exceed 100 % of
what the ore holds. Spend it once - on the blast furnace's gap over the bloomery - and make every tier after
that compete on axes with no ceiling (fuel, throughput) or no scale at all (quality, scrap tolerance).

| Tier | Primary axis | Secondary | Deliberately **not** |
|---|---|---|---|
| **Roasting** | fuel ↓ — the *charge* carries no water/CO₂ in | small recovery ↑ | throughput |
| **Cold-blast furnace** | **recovery ↑↑** vs the bloomery | throughput | fuel |
| **Hot blast** | **fuel ↓↓** — a low-coke burden clears its melt line | throughput ↑ | recovery |
| **Bessemer** | **speed ↑↑** — minutes, not hours | cheap structural steel | quality, scrap |
| **Open hearth** | **quality (low-N) + bulk scrap** | modest recovery ↑ vs Bessemer | speed |

Grounding:

- Hot blast (Neilson, 1828) cut fuel by roughly two thirds; it did not make more iron. smex expresses
  that without a hidden multiplier: a low-coke burden becomes viable, which is the fuel saving, stated as a
  change in what may be charged. Its throughput gain is emergent too, via the temp-margin term in the heat
  balance. Do not add a recovery bonus on top.
- Open hearth wins on things the Bessemer structurally cannot do: it takes hours, so the heat can be
  sampled and corrected; it heats from an external flame instead of the charge's own carbon, so it swallows
  bulk scrap where the autothermal Bessemer chokes at ~15–20 %; and no air is blown through the metal,
  which is the low-N identity smex already builds its material tiers on. Its modest recovery edge
  over the Bessemer is earned - no blow means no spitting and less fume - so ~94 % against the
  Bessemer's 90 % is defensible. That is the only later tier that should touch recovery, and only barely.

> **Roasting and hot blast both reduce fuel - keep them distinguishable.** Roasting acts on the charge
> before it enters; hot blast acts on the furnace (`T_in`). Different terms of the same equation, so they
> stack without either becoming redundant, and hot blast is much the larger of the two.

### Roasting

Roasting adds no iron - it drives off water (limonite ≈ 14 % of its mass), CO₂ from carbonates, and
sulphur, and leaves the ore porous so reduction gas can get in. Its payoff is less coke and a faster
melt (historically 10–20 % fuel), plus a modest recovery bump because more reducible ore leaves less
unreduced FeO in the slag. It plugs into machinery that already exists - the heat balance keys off coke
ratio.

Decisions taken: roasted ore is its own item (roasting precedes mixing, so it is a pipeline stage, not
a burden attribute); it also smelts in a vanilla bloomery at the normal bloomery rate, via the same
`combustibleProps`/`smeltedStack` trick smex already applies to `crushed-iron` - otherwise roasting strands
a player who then wants to bloom. The burdenmaker accepts both raw and roasted; making roasted mandatory
would gate iron behind cast iron (which the heating furnace needs), a deadlock.

> **The one number that decides whether roasting is real:** the coke spent roasting must be clearly less
> than the coke it saves downstream. Otherwise nobody roasts and it is a decorative furnace.

### The accounting that carries it

The blast furnace has a mass balance. Yield is priced per unit of ore content (`BfIronPerOreUnit`),
charge mass is tracked through the melt, and product mass comes out the taps, which is the accounting
recovery fractions require and what makes "slag comes out of the charge" possible. The guard-rail
invariant is enforced: the iiex chain must never yield less iron per ore than a vanilla bloomery
(`OreRecoveryGuardRailTests`).

> **All figures above are placeholders.** Throughput and recovery get balanced once the furnaces are
> actually running - what is written down now is the model, not the numbers.

## Shared simulation model — distillation & phase change (the general still)

> **Scope.** The model below is shared and stays here - the boiler runs it today. The fractionating
> still and every chemical product built on it are deferred to Industrial Homestead
> ([overview.md](overview.md) § Scope) and are not to be designed or built in this suite.

The boiler's water→steam conversion *(live)* is the simplest case of one mechanism the whole liquid
line reuses: heat a liquid, boil off its fractions in ascending boiling-point order, condense each
back to a liquid at a cooler stage. Steam is the one fraction water throws off; a fractionating still
does the same to a mixture. It runs entirely on the live medium taxonomy (a `LiquidDef` has a boiling
point; its vapour is the gas phase; it condenses back per `condensesTo`/`condenseBelow`).

```
while  T_column ≥ boil(fraction):  fraction boils → rises as vapour → condenses at its stage → tapped
residue (never boils in range)  :  stays in the pot → the bottom product (pitch / petroleum coke)
```

Mass-conserving and band-gated (R2, R5): distillation is a set of recipes keyed to temperature
bands (`source + band → fraction (+ remaining source)`), not per-litre composition vectors. The still
is its own multiblock, sibling to the boiler and ladle - built to a height that sets how many
fractions it can split (short pot still = one/two cuts; tall column = the full light→middle→heavy→
residue set). All its I/O reuses live blocks (pipe charge-in, coke firebox or steam jacket for heat,
one vapour take-off per stage condensing at a condenser block, residue tap at the bottom). One still
runs any distillation recipe by its charge. *(Deferred - see the scope note above.)*
