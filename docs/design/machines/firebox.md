# Firebox

**Status** built 2026-08-03 - block, block entity, composable pool behaviour, runtime shape, and both
branch cutovers. Still open: the recipe (quantities) and the boilers' internal fireboxes.
**Mod** iwex (`IronworkingExpanded`), shared by every fuel-bed machine in the suite

**Owns** - the facts this page is canonical for:

* the block that replaces free-placed `game:coalpile` in a firebox, and why that replacement is the same
  move as `iwex:chargepile` in a shaft;
* the shared pool rule - which fireboxes merge, and what "fill one, fill all" costs;
* the fuel list and why lignite is excluded;
* the layer model and the per-fuel texture swap;
* what its arrival breaks upstream - the ignition threshold, the `Firebox` role's glyph, and the second
  cutover.

**Does not own** - cited only: the Shaft/Firebox class split ([conventions](../conventions.md)); the cell-role
mechanism and the layout DSL ([multiblock](../mechanics/multiblock.md)); the heat model and the reverberatory
transfer loss ([heat balance](../mechanics/heat-balance.md), [crucible-furnace](crucible-furnace.md)); the
charge column, which is the shaft equivalent and deliberately a different model
([layered-charge](../layered-charge.md)).

**Depends on** [puddling-furnace](puddling-furnace.md) · [reheat-furnace](reheat-furnace.md) ·
[crucible-furnace](crucible-furnace.md) · [boiler-cornish](boiler-cornish.md) ·
[boiler-lancashire](boiler-lancashire.md) · [cowper](cowper.md) · [layouts.md](../../internal/workbench/layouts.md) § 1

---

## Why

`iwex:furnace-firebox` is the firebox's answer to `iwex:chargepile`: a real block, holding real state,
replacing a vanilla pile. It answers four problems of the free-placed `game:coalpile` under legend
`@(air|coalpile)`:

* `@(air|coalpile)` meant an empty firebox satisfied the structure - a furnace completed with no fuel
  cell built at all, because air was an accepted occupant;
* the pile was vanilla's, dragging in `BlockEntityCoalPile`'s machinery and a Harmony side-table;
* a 16-unit-per-cell ceiling nobody chose - `BlockEntityCoalPile.MaxStackSize`, which made the ignition
  threshold unreachable on both hearths (B8's first cause);
* no fuel identity in the world: a firebox of charcoal and one of coke looked and behaved alike.

With both piles landed, nothing in either branch touches a vanilla coal pile, and the Harmony side-table
(`Patches/CoalPileBlastmixPatches.cs`) is deleted.

---

## The block

**Shape** `assets/editable/shapes/furnace-block-firebox.json`. Two zero-size top-level groups, which is what
`SelectiveElements` wants:

| Group | Contents |
|---|---|
| `Base` | `Masonry` - a 2-thick refractory rim at y 0–2 - plus `Firebar1/2/3`, cast-iron bars at y 1–3 running the full Z |
| `Coke` | `CokeL1`…`CokeL6`, each a full 16×16 slab 2/16 tall, stacked y 2→14 |

The firebars are part of the block, which is why `G` (`game:refractorybrickgrating`) leaves the puddling
and reheat layouts - the grate stopped being a separate cell. The grating stays available as a legend
option; it may be wanted again.

Bars are drawn from `assets/editable/shapes/molten-sandcellfilling-castrods.json` - the normal rod shape in
cast iron, and therefore a sand-cast part.

That makes the firebox a tech-tree edge, not a loop. The machines that have a firebox - puddling, reheat -
are not the machines that make cast iron, so the dependency runs one way: blast furnace or cupola → cast
iron → sand-cast rods → firebox → reverberatory furnaces. A player must have a working iron furnace before
they can build a puddling hearth, which is the historical order.

**Orientation:** `HorizontalOrientable`, visual only - it rotates which way the bars run and nothing else.
Declare that in the block's doc-comment, or someone will later assume the facing is load-bearing and
orientation-check it in a layout.

**Refractory tier: any.** The shape currently hardcodes `refractory/tier3/front1`; it must use `{tier}`
like every other furnace core (`BlockBlastFurnaceCoreCold.cs:39`), so the block wears what it was built from.
A firebox is a fuel bed, not a metallurgical shell - there is no heat argument for pinning tier 3, and
pinning it would put a firebox out of reach of the early-tier player who needs the puddling hearth first. In
layout terms the cell takes `ExCodes.Refractory` (any tier), not `ExCodes.RefractoryTier(3)`.

---

## Fuel

| Accepted | Why |
|---|---|
| coke | the metallurgical default |
| bituminous ("black coal") | historically the reverberatory fuel - burning raw coal without contaminating the iron is the entire reason the reverberatory furnace exists |
| anthracite | the best natural coal for metalwork; already read by name in [cowper](cowper.md) (`BlockEntityCowperStove.cs:121-130`) |
| charcoal | the pre-coke fuel, and the iwex-tier fallback |
| lignite | low-rank, high-moisture, high-ash - it will not carry a metallurgical heat |

One fuel per pool. The layer texture depicts what the player charged, so a pool holds a single fuel type
and refuses a mismatched deposit - the same rule the tall hopper's tank already enforces
(`iwex-hoppertall-wronggrade`).

Every texture already exists and vanilla names them all in `coalpile.json`: `block/coal/coke`,
`block/coal/bituminous`, `block/coal/anthracite`, `block/coal/charcoal`, `block/coal/lignite`. The block
declares four and swaps on fuel type - the same prune-and-retexture path
[charge-pile](../layered-charge.md) uses.

---

## The pool

> Fireboxes that belong to one furnace share one pool. Filling it costs per cell.

Group by `CellRole.Firebox`, not by adjacency. The role exists and the layouts already declare it - the
reheat furnace has two firebox cells, the puddling furnace one. So "fill one, fill all" is simply the
furnace's firebox cells being one pool, for free.

Adjacency alone would be wrong: two furnaces built back to back would merge their fuel. Use adjacency only
as the fallback for a firebox with no owning furnace.

Capacity scales with cell count - more adjacent fireboxes = more coal per layer. A layer of an N-cell pool
costs N × the per-cell layer amount, so a two-cell reheat firebox is twice the fuel of a one-cell puddling
firebox for the same visible fill. The player interacts once; the cost is per cell.

### Settled: 2 coke per layer, per firebox cell

A one-cell firebox (puddling) is 2 units a layer, 12 units full; a two-cell firebox (reheat) is 4 a layer,
24 full. Six layers × 2 × cells.

This makes the ignition threshold derivable. `ChargeCapacityUnits` is
`FireboxCellCount × IwexValues.FireboxMixPerCell`, and `FireboxMixPerCell` is 12 - exactly one cell's full
six layers. So "lit" means the bed is full, on a hearth of any size, and the number falls out of the drawn
shape.

The build guard moved with it: the old assertion measured against `BlockEntityCoalPile.MaxStackSize` (16,
read off vanilla at runtime - a ceiling nobody chose, and B8's first cause); the shipped one is expressed
against the firebox's own layer arithmetic. Keep the guard - it is the assertion whose absence let an
unreachable threshold ship twice.

The bed burns down layer by layer - the six layers in the shape are what let the player read remaining fuel
off the block instead of a tooltip.

### The pool is a behaviour, not a block feature

Boilers host a firebox internally - their own shapes are being redrawn to carry one, rather than a separate
`F` cell in the layout. So the fuel pool must be a composable behaviour that either a standalone
`iwex:furnace-firebox` block or a boiler's own block entity can host. The mod already has the pattern:
`BEBehaviorMoltenCell` is composable and any block can carry it
([molten-network](../mechanics/molten-network.md)).

With the boilers included, every consumer of a coal-pile firebox goes away - so `CollectChargePiles` /
`EnumerateChargePiles` can be deleted outright and `Patches/CoalPileBlastmixPatches.cs` with them.

The cowper stove is settled too, and it does not get a firebox. It is pending a remake that deletes the
burning coal pile outright: a gravitational filter on the exhaust converts it to fuel gas, and that is what
burns inside the stoves. So the last `@(air|coalpile)` holdout is not a migration target; it is being
removed. Nothing in the suite will touch a vanilla coal pile once the boilers take internal fireboxes and
the cowper is remade. See [cowper](cowper.md).

## Interaction

| Verb | Effect |
|---|---|
| **charge** | adds fuel to the pool. One fuel type at a time; a mismatched deposit is refused, as the tall hopper's tank already does |
| **take** | removes fuel back out, exactly as a vanilla coal pile does. A firebox is not a one-way sink |
| **break** | drops the remaining fuel as items |
| **light** | works, even standing alone in a field |

A standalone firebox burns and simply wastes the coal. No gate, no refusal, no special case: the player
lights a fuel bed with nothing above it to heat, and watches the fuel go.

Take-and-drop mirrors the [charge pile](../layered-charge.md)'s settled behaviour, and is simpler here: a
pool holds one fuel, so a break drops one stack rather than one per material.

---

## What it changed

| | |
|---|---|
| The `Firebox` role glyph | `F` (`iwex:furnace-firebox`) replaced `c` (`@(air\|coalpile)`) in the layouts - a required block, so the furnace can no longer complete with no firebox built |
| `BlockEntityFireboxFurnace.ReadChargeMix` | reads the block's pool (`BlockEntityFireboxFurnace.cs:108-119`) - the firebox branch's cutover, mirroring the shaft's |
| `BurnOutCharge` | runs over firebox cells against the pool, not piles |
| The Harmony side-table | `Patches/CoalPileBlastmixPatches.cs` is deleted |
| The boilers | still open - they take the firebox internally (their shapes are being redrawn), so no fuel cell survives in either boiler layout |
| The cowper | still open - loses its coal pile in a remake, not a migration; fuel gas from an exhaust filter replaces it ([cowper](cowper.md)) |

---

## Recipe

Rods + refractory brick + a diagram. The rods may be cast iron or wrought iron - either satisfies the
recipe.

Accepting wrought iron is what makes the block reachable: the crucible furnace hearth uses wrought-iron
firebars anyway, so insisting on cast here would contradict the mod's own metallurgy. It also softens the
tech-tree edge above: a player who has any iron at all can build a hearth, while cast-iron bars remain the
obvious choice once a cupola exists.

This relaxes, but does not remove, the ordering. Refractory brick and a diagram still gate it well past
first smelt, so a firebox is not an early-game block.

---

## What shipped, and where it lives

| Piece | File |
|---|---|
| the pool, composable | `src/IronworkingExpanded/BlockStructures/Furnaces/BEBehaviorFirebox.cs` |
| the block | `.../Furnaces/Blocks/BlockFirebox.cs` - `iwex:furnace-firebox-{tier}-{side}` |
| the block entity (draw + HUD only) | `.../Furnaces/BlockEntities/BlockEntityFirebox.cs` |
| the branch cutover | `.../Furnaces/BlockEntities/BlockEntityFireboxFurnace.cs` - `CollectCharge`, `ReadChargeMix`, `TryIgniteCharge`, `BurnOutCharge` |
| the group | `BlockEntityFurnaceCore.FireboxCells` (`CellRole.Firebox`, never adjacency) |
| the runtime retexture | `ExShapeElements.Retextured` in exlib |
| the shape | `assets/iwex/shapes/furnace/firebox.json` |

The pool is a distribution rule, not shared storage. Each cell keeps its own units;
`BlockEntityFirebox.Charge` spreads a deposit across the owning furnace's `Firebox` cells and the furnace
sums them. Same arithmetic, same one-interaction fill, same per-cell cost - but no cell has to own the save,
and an orphaned firebox still holds exactly the fuel it is drawing.

`BurnOutCharge` does not interpolate by height here. A shaft's column is metres tall and the blast only
reached the bottom of it, so what survives depends on how high it sat. A firebox is one course of cells all
equally in the fire, so the `BfBurnoutFuelRetainedBottom` fraction is the only honest one to apply.

## Open

1. **Recipe.** The composition is settled (above); the quantities are not, so the block has no craft path
   and is creative-only today.
2. **Boiler shapes.** Being redrawn to carry the firebox internally; the behaviour now exists for them to
   adopt.

Ash is deliberately not modelled ([layered-charge](../layered-charge.md) records why). The layouts leave air
below the firebox where an ash pit would be, which is the right shape if that ever changes.
