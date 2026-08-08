# Charge pile

**Status** live (charge-column cutover, 2026-08-06)   **Mod** iwex

**Owns** - the facts this page is canonical for:

* the pile block itself - `iwex:furnace-chargepile`, its band geometry, collision, glow, render path;
* what interacting with a pile does: the top-band take, and what breaking one drops;
* the rule that the pile stores nothing and the furnace holds the charge.

**Does not own** - cited only, never restated: the column model, charging rules and shaft capacity
([blast-furnace-cold](blast-furnace-cold.md)), the raceway and counter-current heat model
([heat balance](../mechanics/heat-balance.md)), fuel carbon values ([fuels](../items/fuels.md)),
the hopper drip ([tall hopper](tall-hopper.md)).

---

## Role

One horizontal slice of a shaft column, drawn as up to 16 stacked bands - the block a player sees and
clicks when they look into a charged furnace. It is a renderer, not a container: no inventory, no stack
size, no per-block save, and nothing of `BlockEntityItemPile` in its ancestry. The charge lives in the
furnace core's `ChargeColumn`s; the pile at height *h* is a window onto bands `h·16 … (h+1)·16` of its
column.

The shaft does not use `game:coalpile`. Vanilla's pile collapses its own column from inside its block
entity (`TriggerPileChanged → TryPartialCollapse`, both private, reached from interaction and merge),
which forces top-down consumption and makes per-layer temperature untenable. None of that machinery is
inherited: the collapse is absent, not suppressed. Descent is one subtraction on the column - nothing
falls, nothing collapses, and blocks appear and disappear only as a column's height crosses a multiple
of 16.

## How it works

* **The furnace materialises it.** `BlockEntityFurnaceCore.SyncChargeBlocks` places and removes piles as
  each column's height changes, walking that column's own ordered cell list (`ChargeCellsOf`). A cell
  holding anything but air or the furnace's own pile is skipped - the column draws one block short and
  heals when the cell frees up, with no units lost. The pile has no creative-inventory entry and no
  handbook page; a hand placement outside a shaft cell is refused
  (`iwex-chargepile-notinshaft`).
* **Bands do not snap to block boundaries.** The window is cut from the column's continuous band
  sequence, so a course taller than one block runs across the boundary as one stripe. Runs are
  run-length encoded (`ChargeBandRun`), and each material draws its own shape element, so coke, charcoal
  and burden read apart on the wall - a charcoal course carries half a coke course's carbon and the
  mismatch must be visible.
* **Collision and selection follow the fill height**, as vanilla's pile does. An orphan (its furnace
  broken out from under it) renders nothing, keeps a one-band selection floor so it can still be
  removed, and breaks plainly with no drops.
* **Glow is the hottest band the block draws**, published through `GetLightHsv` and pushed by
  `OnColumnChanged` - the counter-current temperature profile's only visible output: incandescent at
  the raceway, dark at the stockline. Redraws are sent only when a stripe or the glow level actually
  moved; the shaft would otherwise emit a packet per block per second.
* **The mesh reads a snapshot.** Tesselation runs on its own thread, so `OnTesselation` reads an
  immutable `RenderSlabs` array republished from the main thread and touches nothing else - not the
  column, not the core.

### Taking by hand

Right-click empty-handed lifts one band's worth off the top of the column, not of the clicked block. A
column is one continuous stack and the stockline is the only end a player can reach, so a take undoes the
last load whichever window it was clicked through. The take is clamped to the top segment as well as to
one band, so it always hands back a single material with its stamped mix.

Adding by hand is not implemented: it needs the band-order rule (fuel only above the last burden, lowest
columns first), and a held stack currently does nothing. See Open.

### Breaking

Breaking a pile splices that block's own units out of the column (`ChargeColumn.TakeSpan`,
`BlockEntityChargePile.TakeWindow`) and drops them one stack per material and grade - two burden grades in
one window cannot honestly merge, the same rule the hoppers enforce. Everything above falls: the column is
simply shorter, `SyncChargeBlocks` re-materialises the wall one block down, and the mined pile comes
straight back if charge still reaches that height. The resync runs after the block is gone; run first it
removes the top pile and the break then punches a hole mid-column.

This is the recoverability route for a chilled furnace: a chill sits at the bottom of the shaft, out of
reach of a stockline-only take, so without it a chilled furnace is permanently bricked. A material whose
item no longer resolves is left in the column rather than destroyed.

The pile's own drop table is empty: the furnace holds every unit the block draws, so dropping from the
block and keeping the column would duplicate the charge.

## Numbers

| Constant / key | Value | Where | What it does |
|---|---|---|---|
| `ChargeColumn.BandsPerBlock` | 16 | `ChargeColumn.cs` (const, not config) | one band per voxel layer of the 16-voxel block |
| `ChargeItemsPerBand` | 2 | `IwexConfig.cs` | a band is 2 items, so an ore-shaft block is 32 items |
| `CupolaChargeMetalUnitsPerBlock` | 3000 u | `IwexConfig.cs` | a remelt pile counts metal units, not items - a 5 u bit and a 375 u pig go into the same pile |
| `ChargeColumn.TempMergeEpsilon` | 1 °C | `ChargeColumn.cs` (const) | float-drift tolerance for coalescing consecutive loads into one stripe |
| `replaceable` | 100 | block def | vanilla's default - a high value would let stray placements overwrite a charged shaft |

The per-block quantum is the furnace's `ChargeUnitsPerBlock`; the pile derives band boundaries from it
by multiplying before dividing, so both the item scale and the cupola's 187.5 u/band stay exact.

## Code

`BlockChargePile.cs` (band geometry, boxes, break, placement guard) ·
`BlockEntityChargePile.cs` (window, take, splice, render snapshot) ·
`ChargeColumn.cs` (the data the pile draws) - all under
`src/IronworkingExpanded/BlockStructures/Furnaces/`. Tests: `ChargePileTests`,
`ChargeMaterialisationTests`.

## Open

1. **Hand-charging.** The band-order rule (fuel only above the last burden, lowest columns first) is
   enforced in `NextChargeColumn` for the hopper, but the pile's own interact path does not add charge
   yet - a held stack is ignored.
2. **Proposed: the slag block becomes a slag pile.** `iwex:slag-block` is still a plain cube whose
   `SlagCount` has no in-world producer; the settled design converts it to a player-placeable layered
   pile (place and take a course at a time, drops scaled to layers), which makes the player the
   producer without inventing a mechanic. Not built.
3. **Cupola pig and scrap art.** When the cupola takes pig and scrap directly, they draw as coke until
   their own shape elements are added (`BlockChargePile.ElementOf` falls back to coke by design).
