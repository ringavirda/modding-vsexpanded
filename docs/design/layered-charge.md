# Layered charge — the two-stream burden and the counter-current furnace

**Status** built — the column model and counter-current furnace landed 2026-08-06, the burdenmaker and
the death of the grade model 2026-08-07   **Mod** iiex (smex's hot furnace and the cupola inherit)

Overview of how a shaft furnace is charged and why it runs. Every detail is owned by an entity page -
the links are the contract; nothing here is canonical beyond the summary itself.

---

## The two streams

Settled 2026-07-31: burden is ore + flux, and fuel is charged separately, in alternating courses.
Real furnaces were charged in alternating rounds of fuel and ore-bearing burden: in the cohesive zone
the ore layers soften nearly gas-tight and the fuel layers are the ventilation slits.

What follows from it:

* [burden](items/burden.md) carries one quality - its flux ratio, in three bands.
* The [burdenmaker](machines/burdenmaker.md) - two hoppers over an integrated bunker, no mechanism, no
  MP - is the only source of burden. It replaced the ore mixer and the ore bunker, both deleted from
  the tree, along with `iiex:remeltburden`: the [cupola](machines/cupola.md) takes pig and scrap
  directly, layered with coke.
* Fuel is priced by carbon ([fuels](items/fuels.md)): coke 2, charcoal 1. A charcoal course is a
  legitimate course that runs cooler and shorter than a coke course of the same height.
* Burden on its own cannot burn, so there is no lit-pile-outside-a-furnace state to model: the only
  thing that burns is fuel, and the only place it burns is the raceway.

## The column model

Owned by [blast-furnace-cold](machines/blast-furnace-cold.md) (the model) and
[charge-pile](machines/charge-pile.md) (the block).

The charge is not stored in blocks. Each column `(x, z)` of the shaft owns an ordered segment list,
raceway end first, and the furnace owns the columns - keyed structure-local, so the model is
rotation-correct by construction. A band is 2 items, 16 bands to a block; capacity is geometry -
each column holds what its own height allows, from its own floor, with no per-cell cap anywhere. The
world blocks (`iiex:furnace-chargepile`) are near-stateless windows the furnace materialises and
reconciles (`SyncChargeBlocks`); descent is one subtraction per column, so nothing falls and nothing
collapses. A player takes bands off a column's top by hand, and breaking a pile splices that window's
units out and drops them - the recovery route for a chill, which sits at the bottom of the shaft by
definition.

## Charging

Owned by [blast-furnace-cold](machines/blast-furnace-cold.md) and [tall-hopper](machines/tall-hopper.md).

The hopper is a buffer, not a dispenser: load fuel, it lays a fuel course; load burden, it lays burden
on top. Two loads per course, and the ratio between them is the coke dial. One rule does all the
bookkeeping - fuel may be added to a column only above the last burden, never beneath it, and each
load fills the lowest columns first (the rule tests the fuel role, so a second fuel cannot stack a
fuel course onto a fuel course). The stockline self-levels, a short course stays visibly short, and
there is no layer object, no gate and no overflow case.

## The counter-current furnace

Owned by [heat-balance](mechanics/heat-balance.md) (the model) and
[blast-furnace-cold](machines/blast-furnace-cold.md) (the behaviour).

All combustion happens at the raceway, in front of the tuyeres. The hot gas rises and warms the
descending charge, so a band is warmed by fuel that burned beneath it while it descended - its
temperature rides on the segment. Carbon burned per second is the only throttle, metered per
tuyere: flame temperature, rising gas, descent, production and campaign length all follow from it, and
production is carbon burned × the coke rate (`BfBurdenPerCarbonUnit`) × the melt-speed factor - never
a rate of its own. Burden melts iff the temperature it carried down clears the melt line; a cold band
stops its column (the chill), and a fully hung shaft derives `Firing` with no halt rule anywhere.
Consequences: a taller shaft is more efficient, hot blast is Neilson (more iron from the same carbon),
campaign length is the fuel charged, and the temperature profile - hot at the raceway, dark at the
stockline - is visible on the shaft wall.

State on the shaft is a derived read, not a machine: Idle / Firing / Melting are recomputed every
tick from the columns, with no timers, no fire threshold and no stored flags. Ignition is positional
and pneumatic - a complete bottom course on every column, carbon at the raceway - with no quantity
constant. Breach and choke are opposites: a breached furnace keeps burning at natural draught and can
never re-ignite; a choked one smothers. The firebox furnaces (puddling, heating, reheat) keep their
stored state machine and the `Firebox*` cadence - a fuel bed needs a soak.

## The hearth

Owned by [blast-furnace-cold](machines/blast-furnace-cold.md).

Today the pools are a float pair on the furnace, capped by config, drained by the two taps and frozen
into `iiex:hearthmetal-{metal}` at extinguish. The typed taps are built - `iiex:furnace-irontap` /
`furnace-slagtap`, orientation-pinned in the layouts, both notches at hearth level. Settled and not yet
built: the pool becomes a live layered molten cell in the crucible cells (iron under slag, the same
`BEBehaviorMoltenCell` substrate the canal uses), capacity becomes a visible band height instead of a
constant, the crucible cells stop being chargeable, and the drawn per-type tap shapes land - the slag
channel floor at 10/16 then is the iron pool cap.

## Units

One currency: charge units are metal units (`1 vx³ = 2.5 u`). A band is 2 items for every material -
what an item is worth appears only at conversion. An ore-shaft block is 32 items; a cupola pile is
3 000 metal units over its coke, because a 5 u bit, a 25 u chunk and a 375 u [pig](items/pig.md) all go
into the same pile. Yield is per unit of ore content - 8.5 u/nugget raw against the bloomery's 5, the
recovery ladder of [roasting](processes/roasting.md) and [ironmaking](processes/ironmaking.md).

## Still design-only

* the blow-in ritual - torch on an open tap, the clay plug as the tap's closed state, and the lit
  front climbing the shaft pile-to-pile;
* the live crucible above;
* the slag pile conversion ([charge-pile](machines/charge-pile.md) Open 2);
* hand-charging through the pile blocks (take works, add does not).

## Owner pages

| Detail | Owner |
|---|---|
| shaft structure, columns, charging, yields, pools, taps | [blast-furnace-cold](machines/blast-furnace-cold.md) |
| the pile block, take, break, render | [charge-pile](machines/charge-pile.md) |
| raceway rate model, chill, derived state, blast demand | [heat-balance](mechanics/heat-balance.md) |
| the hopper's tank and drip | [tall-hopper](machines/tall-hopper.md) |
| burden item, flux bands | [burden](items/burden.md) |
| fuel carbon values, coke vs charcoal | [fuels](items/fuels.md) |
| the burdenmaker | [burdenmaker](machines/burdenmaker.md) |
| the cupola's unit scale and charge | [cupola](machines/cupola.md) |
| pig, the 375 u item | [pig](items/pig.md) |
| process chain, blow-in, recovery ladder | [ironmaking](processes/ironmaking.md) |
| molten cells, canals, chisel-out | [molten-network](mechanics/molten-network.md) |
| layout DSL, roles, oriented parts | [multiblock](mechanics/multiblock.md) |
