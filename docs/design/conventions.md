# Conventions, Units & Shared Rules

The single source of truth for units, global invariants, network semantics and the shared simulation
models. Every mod doc cites this file rather than restating these.

---

## Units

| Quantity | Unit | Notes |
|---|---|---|
| Metal mass | **units (u)** | 100 u = 1 vanilla ingot |
| Fluid/gas volume | **litres (L)** *(live)* | A pipe segment holds **30 L** *(live)*; a run's capacity = node count × 30 L. Litres everywhere, never m³ |
| Water → steam | **1 : 16** *(live)* | 1 L water boils to 16 L steam (`SteamExpansionFactor`) |
| Mechanical power | **MP** *(live)* | Vanilla MP network; constant-power generator model (see [lpex](lpex.md)) |
| Steam/water flow | **L/s** | Per-tick flow, EMA-smoothed for the throughput readout *(live)* |
| Temperature | **°C** | One network-wide pipe temperature *(live)*; molten canals are per-cell |
| Pressure | **atm** | 1 atm = ambient. LP steam ≤ ~4–5 atm; HP ~8–12 atm (tunable) |
| Steam-engine efficiency | **0.75** *(live)* | Output pressure = inlet × efficiency |

---

## Global invariants (named rules)

Referenced by name from the mod docs.

- **R1 — Single medium.** A pipe network carries **one medium at a time** (gas *or* water) with a
  unified Volume/Temperature/Pressure/MediumType pool *(live)*. Air, steam, exhaust (and the chemistry
  fractions) are gas media; water and the liquid fractions are liquid media.
- **R2 — Mass-conserving, no hidden yield loss.** Every smelt/convert/refine/distil step preserves
  input mass (1 u in → 1 u out of the new material). Slag, smoke and fume are **cosmetic only**.
  Process *tiers* differ in **throughput and fuel cost, never material yield** — a run's output is
  always predictable.
- **R3 — Molten is per-cell.** Molten metal lives in **per-cell molten canals** (each block owns its
  metal, flows cell to cell) *(live)*. The **ladle** is the only block that merges canals and mixes
  metals.
- **R4 — Steam-only forming.** Billets and profiled stock **cannot** be worked on a vanilla anvil —
  only on the rolling mill / steam hammer (see [smex](smex.md)).
- **R5 — Gate efficiency, not possibility.** The heat-balance and distillation models gate **speed and
  efficiency**, never hard-block a process: there is always one guaranteed path (high-coke + cold blast
  melts at the iron tier). Every threshold is config-tunable.
- **R6 — Stock carries its mass.** Stock/billet items carry remaining mass in a **unit-count stack
  attribute**, so any cut or divide step is exact arithmetic.
- **R7 — No GUI.** All interaction is in-world and verb-based; all state is readable from block-info.

---

## Block-size vocabulary

- **block** — a single 1×1×1 block.
- **megablock** — occupies more than one cell via the **filler-block** mechanic; often a
  RightClickConstructable (RCC).
- **multiblock** — a structure the player **builds by hand in a specific shape**, guided by an in-world
  **projection**. Blocks *and* megablocks can be parts of a multiblock.

A block can be **both** — e.g. boilers are RCC **megablocks** whose construction is also gated by a
**multiblock** projection.

---

## Networks

Four transport-network families, all on exlib's shared block-network graph. The network **logic** lives
in exlib; the pipe **blocks** are per-mod tiers.

- **Molten-canal** *(live)* — per-cell metal, flows cell→cell, end caps recomputed on tesselation. The
  ladle is the only merge/mix point. Owned by [iwex](iwex.md).
- **Pipe (gas *or* water)** *(live)* — single medium per network (R1). Used for water, steam, compressed
  air, exhaust, coal gas and the chemistry fractions. **Three material tiers** of pipe block, ascending
  burst pressure: **bolted** (iwex — hand-riveted from iron plates, 2.5 atm), **cast** (lpex — from cast
  pipe-parts finished on the boring machine, 5 atm), and **rolled** (hpex — Hadfield steel rolled on
  smex's rolling mill, 12 atm). Each tier ships its **own model** at `{domain}:pipes/*`.
  Connectors read the adjacent cell; valves sever/flow; pressure valves overflow.

  **Tiers do not all interconnect, and the rule is the joint, not the pressure** *(live)*. Bolted and
  cast pipe are square in section and bolted through flanges, so a bolted run and a cast run join
  freely — the weakest segment caps the whole run's burst pressure, which is the intended trade. Rolled
  pipe is **octagonal and welded**: there is no flange on it to bolt anything to, so an HP run couples
  only to another HP run. A player cannot save money by feeding an HP main with cheap bolted pipe.

  Implemented as a **joint family** registered per domain (`BlockPipe.RegisterJoint`) and enforced in
  exlib's `BlockNetworkNode.AcceptsNeighbour`, checked at the single point both the graph traversal and
  the open-end scan pass through. Two consequences worth knowing:
  - A refused joint is **not a seal**. The unmated face reads as an open end, so the run leaks — which
    is what tells a player the two lines are not actually plumbed together, rather than a run that
    silently holds and never flows.
  - Every fitting (valve, outlet, passthrough) is a `BlockPipe` subclass, so the rule blocks a rolled
    run from the cast tier's fittings too. That is deliberate: **the HP tier needs its own fittings**,
    and until it has them a rolled run is segments and machine ports only. Machine ports are not pipes
    and are unaffected — a tier is not expected to bring its own boiler.
- **Mechanical power (MP)** *(live)* — vanilla MP network; drives the mechanical blower (iwex) and
  mechanical pump (lpex) as well as engine sub-machines.
- **Electrical (AC + DC)** — the [elex](elex.md) tier (planned).

### The per-mod project skeleton

Every content mod is laid out the same way. The point is that a reader who knows one mod knows them
all, so a folder that exists in one and not another should mean "this mod has no such content", never
"this mod files it somewhere else".

**Core folders — always present:**

| Path | Holds |
|---|---|
| `<Name>ModSystem.cs` (root) | the mod entry point |
| `<Mod>Config.cs` [+ `<Mod>RecipeConfig.cs`] (root) | the `ExConfigRegister` types |
| `LegacyUsings.cs` (root) | the multi-version shim usings |
| `BlockMigrations/` | save migrations, one file per subject |
| `BlockStructures/<Machine>/{Blocks,BlockEntities}/` | one folder per machine; a shared base for the family sits at the machine-folder root, next to its block entity base, not inside `Blocks/` |
| `Patches/` | Harmony patches on vanilla |
| `Recipes/<Type>/` | code-first recipe providers (see below) |

**Content-gated folders — present only when the tier has that content:** `BlockNetwork<Kind>/` (only
for a network-*owning* mod — iwex owns `BlockNetworkPipe` and `BlockNetworkMolten`; lpex ships pipe
*fittings* under its own `BlockNetworkPipe/` but does not own the graph), `Items/`, `Molds/`,
`Commands/` and `Preferences/` (only with a client sub-feature), `Compat/` and `Helpers/` as needed.

**Naming rules that fall out of this:**

- A single-file top-level folder is a smell — the file belongs beside its subject. `SlagPath/` became
  `BlockStructures/Products/`, `Rendering/` became the machine folder its renderer serves.
- **Renderers are co-located with their consumer**, never in a per-mod renderer bucket. Where a mod does
  need one (exlib), it is `Renderers/`, never `Rendering/`.
- An abstract base belongs at the **machine-folder root**, beside its block-entity counterpart —
  `Furnaces/BlockFurnaceCoreBase.cs`, not `Furnaces/Blocks/`. `Blocks/` is for the concrete variants.
- `Compat/` is a legitimate growth bucket even at one file: it is a category, not a stray.

### Where a code-first definition lives

A block/item def lives **next to the class it defines** (`BlockOreMixer.Definitions(...)` sits in
`BlockOreMixer.cs`). A def with no class of its own — a family emitted from a table, like
`SlagPathDefinitions` or `ToolMoldDefinitions` — lives in the **area folder of its subject**
(`BlockStructures/Products/`, `Molds/`), *not* in a per-mod `Blocks/` bucket. Homing by subject beats
homing by kind here: a def is meaningless away from the machinery that reads it, and a `Blocks/` bucket
would split every mega-block's def from its block.

### Where a recipe provider lives

Recipes have no class of their own to sit next to, so they get their own rule — and it is the mirror
image of the one above, because a recipe *file* is addressed by its type first:

```
src/<Mod>/Recipes/<Type>/<Feature>RecipeDefinitions.cs      namespace <Mod>.Recipes.<Type>
```

**The recipe type is the folder, the feature is the file, and the mod name appears in neither** — the
namespace already carries it, and identical class names across assemblies are fine. This is the same
shape the shipped assets use (`recipes/grid/blastfurnace.json`), so the two trees read alike:
`Recipes/Grid/FurnaceRecipeDefinitions.cs` emits `grid/blastfurnace` and `grid/cupola`.

Split a provider along its mod's own `BlockStructures/` areas, not by recipe count — the reader looking
for the cowper's recipe looks for the cowper. Ingredient captures used by **one** provider stay private
to it; captures two providers must agree on go in `Recipes/RecipeIngredients.cs`, because a wildcard that
drifts between two files is a bug nobody sees (the shared cobblestone capture is what keeps the
hand-patterned and diagram-crafted molten canals yielding the same `{rock}` variants).

### Where the authoring copies of assets live (`assets/editable/`)

`assets/editable/shapes/*.json` are **not duplicates of the shipped shapes** — they are the same models
in the format the VS Model Creator needs. The editor requires texture paths that are **absolute and
domain-less**; the game requires domain-prefixed asset paths. The two cannot be one file, so the
editable copy is the hand-edited source and the domain copy is what ships. Do not "de-duplicate" them.
The same holds for `assets/editable/textures/*.psd`, which export to the shipped PNGs.

**The three kinds of tree under `assets/`**, none of which is a mod domain in the same sense:

| Tree | Ships? | Owner | What it is |
|---|---|---|---|
| `assets/<mod>/` | yes | that mod (`<AssetDomain>` in its csproj) | the mod's own domain — blocktypes, shapes, textures, lang, config |
| `assets/editable/` | **no** | source-only | authoring copies: VS Model Creator shapes (domain-less absolute texture paths) and `.psd` sources |
| `assets/game/` | yes, into `game:` | **exactly one mod** | overrides on vanilla's own domain, chiefly lang strings |

`assets/game/` is the one to be careful with: it writes into vanilla's namespace, so **two mods shipping
the same `game:` key silently fight**, and which wins depends on load order. It therefore has a single
owner in this repo rather than being a per-mod convenience.

### Where handbook prose lives

A handbook page is authored as HTML in `docs/<mod>/handbook/NN-*.html` and ships as one long string
under a lang key in `assets/<mod>/lang/en.json`. **The HTML is the source**; editing prose inside a JSON
string literal is miserable, which is the whole reason the split exists.

The copy across is not manual any more — it drifted a full rewrite behind when it was. `HandbookSync`
(in `ExpandedLib.Testing`) joins the two trees on the **`NN-` ordering prefix**, not the file name, so
slugs are free to differ; `HandbookParityTests` fails on drift, `EXLIB_WRITE_HANDBOOK=1` imports
HTML → lang, and `EXLIB_EXPORT_HANDBOOK=1` writes back the other way when the good copy turned out to be
the shipped one. Titles stay hand-authored (a few words, no HTML source) and translations are untouched:
the sync changes values, never the key set, so the lang-parity guard still holds.

Line breaks in an authoring file are **wrapping, not content** — VTML collapses whitespace like HTML, and
so does the sync — so re-wrapping a page is a no-op. Attribute quotes are written `\"` because the files
predate the tooling and were meant to be pasted straight into JSON; the sync un-escapes them.

### Where the network code lives (the `Blocks/`-vs-top-level rule)

exlib splits the network family across two folders, and the split is deliberate:

| Folder | Namespace | Holds |
|---|---|---|
| `src/ExpandedLib/Networks/` | `ExpandedLib.Networks` | the **graph model** — `BlockNetwork` and its subclasses, `PipeNetworkState`, and the contracts a participant implements (`INetworkNode`, `INetworkConnector`, `IPipeNode`, `IPipeVentStrategy`, `IMoltenCell`, `IBurstablePipe`, `IChimneyVentable`). No `Block`/`BlockEntity` in sight. |
| `src/ExpandedLib/Blocks/Networks/` | `ExpandedLib.Blocks.Networks` | the **engine-facing shell** — `BlockNetworkNode`, `BlockEntityNetworkNode`, and the two `ModSystem`s that own the graph and its highlight. |

**The rule, generalized:** a top-level folder under `src/ExpandedLib/` is a *simulation domain* — a model
you could reason about with no world loaded (`Networks`, `Metals`, `Fluids`, `Materials`, `Process`).
`Blocks/` is where that model meets Vintage Story: types that derive `Block`, `BlockEntity`,
`BlockBehavior` or `ModSystem`. The graph used to sit under `Blocks/Networks/`, one level *deeper* than
the `Fluids/` it consumes, which read as an accident rather than a decision.

Consumers usually want both namespaces and import both; that is expected, not a smell. Deliberately
**not** done: a `Simulation/` super-folder over the domain folders (YAGNI - there is nothing to
disambiguate them from).

**The medium set is data-driven** *(live)*: each medium is a `LiquidDef` in exlib's liquid taxonomy
(code, gas/liquid phase, merge priority, boil/condense points). A mod adds a medium by shipping one
JSON entry; the built-in four (Air / Steam / Exhaust / Water) reproduce the old hardcoded behaviour
exactly. The chemistry add-on registers its distillation fractions (coal tar, benzene, kerosene, crude
oil, the acids, …) as further media that ride the very same pipes, condensers, valves and tanks.

---

## Shared simulation model — blast demand comes from the burden

Neither the pressure a furnace needs nor the air it draws is a property of the furnace. Both come out of
one number — the burden's **coke fraction** — and both directions are physically grounded:

- **Air**: air is the oxidant for coke. A coke-rich burden burns more fuel per ton of iron and needs
  proportionally more air to do it. (Neilson's hot blast cut coke per ton by roughly two-thirds — and
  with it, blast volume per ton.)
- **Pressure**: coke is the **permeable skeleton** of the charge column, the coarse non-fusing component
  that holds gas channels open through the stack. A coke-lean burden packs denser, so the pressure drop
  across it is higher and the blast must be driven harder to get through.

```
required atm  = BfBlastPressureAtReference + (BfReferenceFuelFrac − fuelFrac) × BfBlastPressureCokeSensitivity
air  L/s/tuyere = TuyereIntakeVolume × clamp(fuelFrac / BfReferenceFuelFrac)
```

At the shipped defaults:

| Burden | Needs | Air/tuyere | Blowable by |
|---|---|---|---|
| rich (30 % coke) | 1.25 atm | 21 L/s | bellows |
| standard (20 %) | 2.0 atm | 14 L/s | bellows |
| lean (10 % coke) | 2.75 atm | 7 L/s | **steam only** |

**This is the tier gate, and it is a consequence rather than a rule.** A mechanically blown iron furnace
can always be brute-forced with a coke-rich charge — cheap pressure, expensive fuel. The coke-lean charge
that actually saves fuel demands 2.75 atm, which is above both the twin-tub blower's 2.2 atm ceiling
*and* bolted pipe's 2.5 atm burst rating, so it is gated twice over: you need a steam blower **and** cast
pipe to carry it. Nothing in the code branches on which furnace it is; the hot blast furnace is simply
the one worth building once you can run lean.

Note that hot air does **not** need more pressure *because it is hot* — hot blast is thermal
recuperation, nothing pneumatic. The causation runs through the burden it enables. Keep the fiction
pointed at the charge, not the air temperature.

**Pipe burst ratings double as capacity** (`burst × pipes × litres-per-pipe`), so the bolted tier is both
the low-pressure tier and the small-buffer one: bolted 2.5, cast 5, rolled 12.

---

## Shared simulation model — dynamic heat balance

Furnaces have **no hardcoded max temperature**. Each runs a per-tick heat balance:

```
T_process = T_in − T_loss     — melts/refines only while  T_process ≥ T_threshold(material)
```

- **T_in** (heat source): coke combustion (coke ratio × air flow) + a blast-preheat buff
  (cowper/regenerator); OR electrical power (arc); OR autothermal oxidation (converter — the pig's own
  C/Si burned by the blow); OR fuel flame + regenerator (open hearth, reverberatory).
- **T_loss** (heat sinks): cold-charge mass (scrap, ore, wet feed) + radiation/ambient (worse in
  winter).

Block-info always shows current T, the threshold and the contributors, so a stall reads
"1410 °C, needs 1538 °C — add coke or hot blast", never a silent failure. Per R5 the model gates
efficiency, not possibility, and every threshold is tunable. Consequences (all emergent, not
hardcoded): cold-blast vs hot-blast falls out of whether cowpers are charged; melt rate scales with the
temp margin; the converter's scrap cap emerges from bath freezing past ~15–20 %; an underfed
boiler → weak blower → cold furnace is one loop.

## Shared simulation model — distillation & phase change (the general still)

The boiler's water→steam conversion *(live)* is the simplest case of one mechanism the whole liquid
line reuses: **heat a liquid, boil off its fractions in ascending boiling-point order, condense each
back to a liquid at a cooler stage.** Steam is the one fraction water throws off; a fractionating still
does the same to a *mixture*. It runs entirely on the live medium taxonomy (a `LiquidDef` has a boiling
point; its vapour is the gas phase; it condenses back per `condensesTo`/`condenseBelow`).

```
while  T_column ≥ boil(fraction):  fraction boils → rises as vapour → condenses at its stage → tapped
residue (never boils in range)  :  stays in the pot → the bottom product (pitch / petroleum coke)
```

Mass-conserving and band-gated (R2, R5): distillation is a set of **recipes keyed to temperature
bands** (`source + band → fraction (+ remaining source)`), not per-litre composition vectors. The still
is its **own multiblock**, sibling to the boiler and ladle — built to a height that sets how many
fractions it can split (short pot still = one/two cuts; tall column = the full light→middle→heavy→
residue set). All its I/O reuses live blocks (pipe charge-in, coke firebox or steam jacket for heat,
one vapour take-off per stage condensing at a condenser block, residue tap at the bottom). One still
runs any distillation recipe by its charge. Spec in [lpex chemistry add-on](lpex.md).
