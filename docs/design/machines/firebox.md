# Firebox

**Status** built 2026-08-03 - block, block entity, composable pool behaviour, runtime shape, and both
branch cutovers. The bed's geometry became per-block data 2026-08-23 and the Cornish boiler hosts one.
Still open: the Lancashire boiler's internal firebox.
**Mod** iiex (`IronIndustryExpanded`), shared by every fuel-bed machine in the suite

**Owns** - the facts this page is canonical for:

* the block that replaces free-placed `game:coalpile` in a firebox, and why that replacement is the same
  move as `iiex:chargepile` in a shaft;
* the shared pool rule - which fireboxes merge, and what "fill one, fill all" costs;
* the fuel floor (`combustibleProps.BurnTemperature` against `IiexValues.BoilerFuelMinTemp`) and where the
  metallurgical exclusion of low-rank coal lives - the furnace, not the bed;
* the layer model, its declared per-block geometry, and the per-fuel texture swap;
* the behaviour's host contract - what a machine has to declare to carry a bed without a firebox block;
* what its arrival breaks upstream - the ignition threshold, the `Firebox` role's glyph, and the second
  cutover.

**Does not own** - cited only: the Shaft/Firebox class split ([conventions](../conventions.md)); the cell-role
mechanism and the layout DSL ([multiblock](../mechanics/multiblock.md)); the heat model and the reverberatory
transfer loss ([heat balance](../mechanics/heat-balance.md), [crucible-furnace](crucible-furnace.md)); the
charge column, which is the shaft equivalent and deliberately a different model
([layered-charge](../layered-charge.md)).

**Depends on** [puddling-furnace](puddling-furnace.md) · [reheat-furnace](reheat-furnace.md) ·
[crucible-furnace](crucible-furnace.md) · [boiler-cornish](boiler-cornish.md) ·
[boiler-lancashire](boiler-lancashire.md) · [cowper](cowper.md) · [layouts.md](../../../workbench/layouts.md) § 1

---

## Why

`iiex:furnace-firebox` is the firebox's answer to `iiex:chargepile`: a real block, holding real state,
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

**Shape** `workbench/shapes/furnace-block-firebox.json`. Two zero-size top-level groups, which is what
`SelectiveElements` wants:

| Group | Contents |
|---|---|
| `Base` | `Masonry` - a 2-thick refractory rim at y 0–2 - plus `Firebar1/2/3`, cast-iron bars at y 1–3 running the full Z |
| `Coke` | `CokeL1`…`CokeL6`, each a full 16×16 slab 2/16 tall, stacked y 2→14 |

The firebars are part of the block, which is why `G` (`game:refractorybrickgrating`) leaves the puddling
and reheat layouts - the grate stopped being a separate cell. The grating stays available as a legend
option; it may be wanted again.

Bars are drawn from `workbench/shapes/molten-sandcellfilling-castrods.json` - the normal rod shape in
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

A bed takes any item whose own `combustibleProps.BurnTemperature` clears `IiexValues.BoilerFuelMinTemp` -
not a curated list, so a fuel another mod ships is admitted by declaring what it already declares. See
`BEBehaviorFirebox.IsFuel`.

| Fuel | Why it clears the floor |
|---|---|
| coke | the metallurgical default |
| bituminous ("black coal") | historically the reverberatory fuel - burning raw coal without contaminating the iron is the entire reason the reverberatory furnace exists |
| anthracite | the best natural coal for metalwork; already read by name in [cowper](cowper.md) (`BlockEntityCowperStove.cs:121-130`) |
| charcoal | the pre-coke fuel, and the iiex-tier fallback |
| lignite | clears the bed's floor comfortably - low-rank, high-moisture, high-ash coal still burns far hotter than a bed needs. A reverberatory hearth refuses it anyway, for want of a metallurgical heat - see `BlockEntityFireboxFurnace.AcceptsFireboxFuel`. A boiler burns it. |

One fuel per pool. The layer texture depicts what the player charged, so a pool holds a single fuel type
and refuses a mismatched deposit - the same rule the tall hopper's tank already enforces
(`iiex-hoppertall-wronggrade`).

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

### Settled: 2 coke per layer, per firebox cell — as the default

A one-cell firebox (puddling) is 2 units a layer, 12 units full; a two-cell firebox (reheat) is 4 a layer,
24 full. Six layers × 2 × cells.

Those two numbers are the **defaults**, not the model. A bed reads its own geometry from the behaviour's
declared properties and falls back to `IiexValues.FireboxLayersPerCell` / `FireboxUnitsPerLayer` when the
blocktype states neither, so every shipped furnace sits on 6 × 2 and a machine with different art declares
its own:

| Property | Default | Means |
|---|---|---|
| `layers` | `FireboxLayersPerCell` = 6 | drawn courses in one cell; `LayerCount` rounds up so a part-filled course still draws |
| `unitsPerLayer` | `FireboxUnitsPerLayer` = 2 | fuel units one course holds; capacity is the product |
| `bedElement` | `"Coke"` | the shape element group the courses live under |
| `layerPrefix` | `"CokeL"` | the per-course element name, suffixed 1..`layers` |

`Initialize` clamps `layers` and `unitsPerLayer` to at least 1 (`BEBehaviorFirebox.cs:91-97`), and
`ElementsFor(n)` builds the element paths from this bed's own pair rather than from the constants
(`:101-106`). The Cornish boiler is the one leaf off the defaults: 4 × 4 = 16, under `CoalLayers/L1`..`L4`.

⛔ Ignition follows the declaration but the charge *count* does not. `TryIgniteCharge` asks each bed its
own `IsFull` (`BlockEntityFireboxFurnace.cs:176-183`), so a hearth of oddly-sized cells still lights on a
full bed. `ChargeCapacityUnits` is `FireboxCellCount × FireboxMixPerCell` (`:264-265`), a flat 12 a cell,
and `FurnaceBranchGuards` measures against `DefaultCellCapacity` - both would misread a furnace whose
cells declared a different geometry. Nothing shipped does; the only leaf off the defaults is the boiler,
which takes no part in that branch at all because it holds its own lit bit rather than asking a furnace
core.

This makes the ignition threshold derivable. `ChargeCapacityUnits` is
`FireboxCellCount × IiexValues.FireboxMixPerCell`, and `FireboxMixPerCell` is 12 - exactly one cell's full
six layers. So "lit" means the bed is full, on a hearth of any size, and the number falls out of the drawn
shape.

The build guard moved with it: the old assertion measured against `BlockEntityCoalPile.MaxStackSize` (16,
read off vanilla at runtime - a ceiling nobody chose, and B8's first cause); the shipped one is expressed
against the firebox's own layer arithmetic. Keep the guard - it is the assertion whose absence let an
unreachable threshold ship twice.

The bed burns down layer by layer - the six layers in the shape are what let the player read remaining fuel
off the block instead of a tooltip.

### The pool is a behaviour, not a block feature

A boiler hosts a firebox internally - its own shape carries one, rather than a separate `F` cell in a
layout - so the fuel pool is a composable behaviour that either a standalone `iiex:furnace-firebox` block
or a machine's own block entity can host. The mod already had the pattern: `BEBehaviorMoltenCell` is
composable and any block can carry it ([molten-network](../mechanics/molten-network.md)).

**The host contract**, in full - what a machine that is not a firebox block has to do:

| | |
|---|---|
| Declare | `EntityBehavior<BEBehaviorFirebox>` on the leaf blocktype, with `layers` / `unitsPerLayer` / `bedElement` / `layerPrefix` for its own art (`BlockBoilerCornish.cs:49-56`) |
| Read it back | `GetBehavior<BEBehaviorFirebox>()`; a null answer *is* the "this machine has no bed" branch (`BlockEntityBoiler.Client.cs:144`) |
| Draw it | the bed's `ElementsFor(LayerCount)` names the courses to keep, and `ComposeOver` narrows a construction behaviour's whole-group entry down to exactly them, for a host whose mesh is filtered per stage rather than pruned per block entity (`BlockEntityBoiler.DrawnElements`); the fuel texture is the host's own `ITexPositionSource` problem, since a bed holds a code and not an atlas position |
| Own the fire | the behaviour holds units and a fuel code and nothing else - no lit state, no clock. A host that is not a furnace core keeps its own (`BlockEntityBoiler._lit`, `_fuelSeconds`) |
| Own the refusals | the bed answers `Accepts`; a host with a narrower rule states it and states its own refusal message ([fuels](../items/fuels.md) § Two taxonomies) |

The Cornish boiler is the built example: 4 × 4 = 16 units under `CoalLayers/L1`..`L4`, lit through its own
main hatch, drawn down by the charged fuel's own `burnDuration`
([Cornish boiler](boiler-cornish.md) § The internal firebox).

⛔ The take-a-course-back-out verb belongs to `BlockFirebox`, not to the behaviour. A host that wants it
implements it; the boiler deliberately does not, so a boiler bed is charged and burned, never dug out.

With the Lancashire converted too, every consumer of a coal-pile firebox goes away - so `CollectChargePiles`
/ `EnumerateChargePiles` can be deleted outright and `Patches/CoalPileBlastmixPatches.cs` with them.

The cowper stove is settled too, and it does not get a firebox. It is pending a remake that deletes the
burning coal pile outright: a gravitational filter on the exhaust converts it to fuel gas, and that is what
burns inside the stoves. So the last `@(air|coalpile)` holdout is not a migration target; it is being
removed. Nothing in the suite will touch a vanilla coal pile once the Lancashire takes an internal firebox
and the cowper is remade. See [cowper](cowper.md).

## Interaction

These are the **block's** verbs (`BlockFirebox`), not the behaviour's. A host that carries a bed without
the block gets none of them for free and offers what suits it - the boiler offers charge and light, and no
take (§ The pool is a behaviour, not a block feature).

| Verb | Effect |
|---|---|
| **charge** | adds fuel to the pool. One fuel type at a time; a mismatched deposit is refused, as the tall hopper's tank already does |
| **take** | removes fuel back out, exactly as a vanilla coal pile does. A firebox is not a one-way sink |
| **break** | drops the remaining fuel as items |
| **light** | works, even standing alone in a field |

A refused deposit says which of two things went wrong: not fuel at all, or fuel this machine will not
take. The second reason is the owning machine's, so the machine states it - `RefuseFireboxFuel` on
`BlockEntityFireboxFurnace`, overridden by the coke oven, paired with the `AcceptsFireboxFuel` that made
the refusal.

A standalone firebox burns and simply wastes the coal. No gate, no refusal, no special case: the player
lights a fuel bed with nothing above it to heat, and watches the fuel go.

Take-and-drop mirrors the [charge pile](../layered-charge.md)'s settled behaviour, and is simpler here: a
pool holds one fuel, so a break drops one stack rather than one per material.

---

## What it changed

| | |
|---|---|
| The `Firebox` role glyph | `F` (`iiex:furnace-firebox`) replaced `c` (`@(air\|coalpile)`) in the layouts - a required block, so the furnace can no longer complete with no firebox built |
| `BlockEntityFireboxFurnace.ReadChargeMix` | reads the block's pool (`BlockEntityFireboxFurnace.cs:108-119`) - the firebox branch's cutover, mirroring the shaft's |
| `BurnOutCharge` | runs over firebox cells against the pool, not piles |
| The Harmony side-table | `Patches/CoalPileBlastmixPatches.cs` is deleted |
| The Cornish boiler | takes the bed internally, as a hosted behaviour on its own block entity; it has no fuel cell and no layout at all ([Cornish boiler](boiler-cornish.md)) |
| The Lancashire boiler | still open - it lost its layout with the shared multiblock base but kept the coal-pile read, so its fire is a free-placed pile nothing requires ([Lancashire boiler](boiler-lancashire.md)) |
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
| the pool, composable | `mods/iiex/src/BlockStructures/Furnaces/BEBehaviorFirebox.cs` |
| its declared geometry | same file - `Initialize` (`:91-97`), `ElementsFor` (`:101-106`), `DefaultElementsFor` (`:113-118`) |
| the block | `.../Furnaces/Blocks/BlockFirebox.cs` - `iiex:furnace-firebox-{tier}-{side}` |
| the block entity (draw + HUD only) | `.../Furnaces/BlockEntities/BlockEntityFirebox.cs` |
| the branch cutover | `.../Furnaces/BlockEntities/BlockEntityFireboxFurnace.cs` - `CollectCharge`, `ReadChargeMix`, `TryIgniteCharge`, `BurnOutCharge`, `AcceptsFireboxFuel` / `RefuseFireboxFuel` |
| the group | `BlockEntityFurnaceCore.FireboxCells` (`CellRole.Firebox`, never adjacency) |
| the one non-block host | `.../Boiler/Blocks/BlockBoilerCornish.cs:49-56` declares it; `.../Boiler/BlockEntityBoiler.cs` owns the fire around it |
| the runtime retexture | `ExShapeElements.Retextured` in exlib |
| the shape | `mods/iiex/assets/iiex/shapes/furnace/firebox.json` |

The pool is a distribution rule, not shared storage. Each cell keeps its own units;
`BlockEntityFirebox.Charge` spreads a deposit across the owning furnace's `Firebox` cells and the furnace
sums them. Same arithmetic, same one-interaction fill, same per-cell cost - but no cell has to own the save,
and an orphaned firebox still holds exactly the fuel it is drawing.

`BurnOutCharge` does not interpolate by height here. A shaft's column is metres tall and the blast only
reached the bottom of it, so what survives depends on how high it sat. A firebox is one course of cells all
equally in the fire, so the `BfBurnoutFuelRetainedBottom` fraction is the only honest one to apply.

## Open

1. ~~**Recipe.**~~ **Closed 2026-08-21** (U6.11): firebars set in refractory brick, in the `reverberatory`
   group with the rest of the chassis. It was a required cell in both reverberatory layouts and had no
   recipe at all, so **neither machine could be built by a player** whatever else was fixed. A diagram is
   still not part of it - diagram crafting is creative-only until the design table can draft them.
2. **Boiler shapes.** ~~Being redrawn to carry the firebox internally.~~ **Half closed 2026-08-23**: the
   Cornish carries its bed in its own shape and its own block entity. The Lancashire's art is not redrawn,
   so it still wants a free-placed coal pile beside it - and no longer even requires one, since it lost its
   layout with the multiblock base ([Lancashire boiler](boiler-lancashire.md)).
3. **`ChargeCapacityUnits` does not read the bed.** It is `FireboxCellCount × FireboxMixPerCell`, a flat
   12 a cell, while the bed's real capacity is its own declared `layers × unitsPerLayer`. The two agree on
   everything shipped and would not on a furnace that declared different art (§ Settled).

Ash is deliberately not modelled ([layered-charge](../layered-charge.md) records why). The layouts leave air
below the firebox where an ash pit would be, which is the right shape if that ever changes.
