# Reheat (Heating) Furnace

**Status** shell - the multiblock, all five part blocks and the hearth rows are built and tested. Missing:
heat-into-stock, the crosswise seating, and (like the [puddling furnace](puddling-furnace.md)) the ability
for the structure to complete at all
**Mod** iwex (`IronworkingExpanded`)

**Owns** everything specific to this machine: its multiblock layout and every cell offset in it, the
reverberatory geometry overrides, the hearth's contents model as built (one piece per row, three rows,
`TryLoad`/`TryTake` and how the reach rule applies on the way out), `HeatingHearthLayout`'s stock
recognition and element map, the `OnTesselation` prune path, the machine-side plan for both seatings, the
composed-stock renderer and the reheat rate law.

**Does not own** - cited only: the ≤ 32 / ≤ 48 handling limits, their slot counts, the LIFO ordering and the
definition of a soft-lock ([recoverability](../mechanics/recoverability.md)); the heat model, the FSM and
the shared firebox branch's charge model ([heat balance](../mechanics/heat-balance.md),
[firebox](firebox.md)); the stock ladder, `V/A` per piece and the cooling half of the law
([rolling mill](rolling-mill.md), [rolling](../processes/rolling.md)).

**Depends on**
[recoverability](../mechanics/recoverability.md) · [heat balance](../mechanics/heat-balance.md) ·
[multiblock & filler structures](../mechanics/multiblock.md) ·
[definitions, recipes & config](../mechanics/recipes-config.md) ·
[density rule](../mechanics/density-rule.md) · [rolling mill](rolling-mill.md) ·
[puddling furnace](puddling-furnace.md) (the same chassis, one row shallower) · [firebox](firebox.md) ·
[stock rack](stock-rack.md) (shares the planned `StockPile.Place`) ·
[rolling](../processes/rolling.md) · [layouts-workbench.md](../../internal/workbench/layouts.md) § 1

---

## Planned: the chimney becomes this machine's temperature dial *(designed 2026-08-02)*

The natural-draught work specified on the [crucible furnace](crucible-furnace.md) page - a player-built
chimney whose course count feeds `NaturalDraughtFor(courses, damper)` - lands on this furnace too.

Reheating hits a target band, not maximum heat: rolling wants ~1100-1250 °C, shingling hotter, annealing far
cooler. A chimney built up or taken down is therefore a heat-treatment dial on a machine that otherwise has
one temperature. Heat treatment has no mechanic today, and the "crucible-steel tool heads last longer" idea
waits on this system.

The chimney cannot melt the stock, for a physical reason rather than a capped constant. This furnace is
reverberatory: the flame crosses the bridge to reach the work and loses heat doing it. With draught
saturating at `natural = 0.85`, `T_in` tops out near 1906 °C; against a reverberatory `T_loss` of roughly
420 (120 radiation + ~250 transfer + ~50 charge), `T_process` ceilings at ≈1486 °C, just below iron's 1538.
Thirty courses still give only a very hot furnace. The same fact makes puddling a pasty-state process.

Caution: the transfer loss must be a virtual with a firebox-branch default, never a branch constant. The
crucible furnace shares this branch but is not reverberatory (its pots sit in the coke), so it overrides the
loss to ≈0; a branch constant would cap that machine out of existence. Arithmetic on the
[crucible furnace](crucible-furnace.md) page.

---

## Role

A vanilla forge tops out at an ingot. A shingled bloom or a cast slab will not fit in one, so this furnace
is the only way to put heat back into mill stock, and the [rolling mill](rolling-mill.md)'s "keep it hot or
it jams" loop depends on it. `RollingCoolRate` (`IwexConfig.cs`) is tuned so that a single pass finishes
comfortably but a full schedule on one heat does not.

The hearth's 3 × 2 footprint is where both handling limits of the
[recoverability invariant](../mechanics/recoverability.md) come from: "can this piece be reheated?" is
"does it fit on this bed?".

Second, planned job: roasting ore before the blast furnace, the reverberatory calciner's job, which is why
the same building serves both (`BlockHeatingFurnaceCore.cs:16-18`).

The shared core does not assume melting: this furnace overrides none of the molten-product members
(`BlockEntityFurnaceCore.cs`, see [Numbers](#geometry)).

---

## Structure

Anchor `iwex:furnace-heatingcore-{tier}-{side}`. Layout at `BlockHeatingFurnaceCore.cs:35-141`; shipped form
is the golden at `test/IronworkingExpanded.Tests/goldens/iwex/blocktypes/furnaces/heating-core.json`.

**Footprint** 8 wide (X) × 4 deep (Z) × 5 tall (Y), 109 declared cells. `Origin(-6, -2)`, the negation of
`C`'s (col 6, row 2). Same chassis as the [puddling furnace](puddling-furnace.md), one row deeper in Z,
which is what makes a slab fit; the origin is therefore the one line that must not be copied between the
two layouts (`BlockHeatingFurnaceCore.cs:59-66`).

| Glyph | Block | Count |
|---|---|---|
| `#` | `game:refractorybricks-good-tier*` | 68 |
| `-` | `game:brickslabs-fire-up-free` | 8 |
| `i` | `game:brickslabs-fire-south-free` (orientation-checked) | 2 |
| `K` | the firebox stoking door (`VanillaCodes.Sealing`, south) | 1 |
| `F` | `iwex:furnace-firebox-*` - the fuel bed, its own block ([firebox](firebox.md)) | 2 |
| `C` | `iwex:furnace-heatingcore-*` | 1 |
| `D` | `iwex:furnace-chargedoor-south` | 1 |
| `H` | `iwex:furnace-heatinghearth-north` | 1 |
| `f` | `exlib:structurefiller` | 12 |
| `a` / `A` | `game:air` - the ash pit under the firebox, the flame space and the flue bore (`A` carries `CellRole.Flue`) | 13 |

An empty firebox does not satisfy the structure: the fuel bed is a required block, its firebars are part of
the firebox block, and what is under it is air (the ash pit, which is not modelled). There are two firebox
cells here rather than the puddling furnace's one because this hearth is a row deeper and its firebox runs
the full depth beside it.

**Cells that matter** (anchor frame, `C` = `(0,0,0)`):

| Cell | What it is | Declared at |
|---|---|---|
| hearth principal - the near row's centre, at the door | layout `H` | `BlockHeatingFurnaceCore.cs` |
| the hearth's five fillers: the far row plus the near flanks | `BlockHeatingHearth.cs:46-57` | |
| `ShaftCentre` (sound origin) `(-2, 0, 1)` | `BlockEntityHeatingFurnace.cs:37` | |
| the firebox - two `F` cells along one side | layout, `CellRole.Firebox` | |
| charge door + its upper filler | `BlockChargeDoor.cs` | |
| the flue bore - the `a`/`A` column above the core | layout | |

**Interactive cells**: all six hearth cells (both rows of a column map to the same row - the far cell is
depth for a slab, not a fourth place to put something, `BlockHeatingHearth.cs:73-81`), and the charge door
plus its upper half.

There is no chimney damper on this furnace; the cap is a puddling-only part. Nothing regulates its draught
today.

---

## Assets

| Part | Editable | Runtime | Notes |
|---|---|---|---|
| core | - | `game:block/basic/cube` | `n` marker north, `hf` type label south (`BlockHeatingFurnaceCore.cs:48-58`) |
| hearth | `assets/editable/shapes/furnace-megablock-heatinghearth.json` | `assets/iwex/shapes/furnaces/heatinghearth.json` | elements `Base`, `BaseExtension`, `Bed`, `Items1`, `Items2`, `Items3`. No animations - pruned `OnTesselation` |
| charge door | `assets/editable/shapes/furnace-megablock-chargedoor.json` | `assets/iwex/shapes/furnaces/chargedoor.json` | elements `Bricks`, `Rails`, `Door`; animations `closed`, `open` |

Caution: the hearth art is authored per (row × stock form), 3 × 5 = 15 groups, and it does not agree with
the stock it is meant to hold. The beds draw a 16-long piece while the settled shingled bar is 18. Three of
the five forms it has art for (`castbillet`, `castbloom`, `castslab`) have no item behind them at all;
those three strings appear only in `HeatingHearthLayout.cs:72-77`. The fix is the composed-stock renderer in
[Open](#open).

---

## Construction

No recipe exists for the core, the hearth, the charge door or the firebox. `FurnaceRecipeDefinitions.cs`
defines recipe groups for the blast furnace, the tuyere, the tall hopper, the blower and the cupola, and
none of the iwex grid goldens outputs a heating-furnace part. No RCC construction stages either; every part
is a plain placed block. Creative-only today.

The vanilla half (refractory brick, fire-brick slabs, sealing door) is craftable already.

---

## Operation

### What works today

| Verb | Where | Effect | Code |
|---|---|---|---|
| RMB with a stock piece | any hearth cell | lays it in that row | `BlockHeatingHearth.cs` → `BlockEntityHeatingHearth.cs:57-69` |
| RMB empty-handed | any hearth cell | takes the piece out of that row | `BlockEntityHeatingHearth.cs:73-84` |
| RMB | charge door (either cell) | swings it | `BlockChargeDoor.cs` |
| Ctrl + Shift + RMB | core, hearth, door | build outline + shopping list | `BlockHeatingHearth.cs` |

One verb, decided by what the player holds, as at every other in-world station here. Refusals are named:
`iwex-hearth-rowfull`, `iwex-hearth-notstock`, `iwex-hearth-centreblocks`.

What the hearth accepts is a whitelist keyed on the item code's leading segment
(`HeatingHearthLayout.cs:64-79`). Whole stacks are never taken: each piece carries its own state, and the
stock items are `MaxStackSize(1)` for that reason (`StockItemDefinitions.cs:42`).

The reach rule cuts both ways. A loaded centre blocks both flanks going in (`HearthRows.cs:75-76`) and
coming out (`BlockEntityHeatingHearth.cs:77-80`), so a full hearth unloads centre-first. The same
reachability argument produces the crosswise LIFO ([recoverability](../mechanics/recoverability.md)).

### Firebox

Reading the fuel bed, counting it as pure fuel and igniting it belong to the shared firebox branch
(`BlockEntityFireboxFurnace`: `ReadChargeMix` at `:108`, `TryIgniteCharge` at `:141`). A reverberatory
firebox burns plain fuel, never a charge column, because nothing in the fire is being reduced. The heating
furnace overrides nothing there; see [firebox](firebox.md) and [heat balance](../mechanics/heat-balance.md).

### What is missing

`SmeltCycle` is empty (`BlockEntityHeatingFurnace.cs:53`): no heat goes into the stock. A lit reheat furnace
holds its own temperature and nothing more; the pieces on the bed keep cooling on vanilla's own temperature
attribute. There is no roasting mode and no damper.

### The two seatings

The bed's 3 × 2 footprint is read in two directions; which one applies is derived from the piece's length.
[Recoverability](../mechanics/recoverability.md) owns both limits, both slot counts and the ordering rule.

| Seating | Runs along | Built? |
|---|---|---|
| **lengthwise** - per-row, click the row wanted | the 2-cell depth | live (`BlockEntityHeatingHearth.cs:30`, three independent slots) |
| **crosswise** - one LIFO stack through any top filler | the 3-cell width | not built |

`HearthRows`' fixed `Left / Centre / Right` enum cannot express crosswise (`HearthRows.cs:20-25`), and
neither can the hearth's `ItemStack?[3]` (`BlockEntityHeatingHearth.cs:30`). A crosswise piece occupies all
three lengthwise slots, so the two modes are mutually exclusive and the block entity needs one contents
model carrying a mode tag, not three independent rows. `FromLocalOffset` (`HearthRows.cs:54-63`) likewise
assumes one row per X, and `BlockHeatingHearth.RowAt` flattens Z to 0, which is the assumption crosswise
breaks.

The mode must be derived, never stored: over the lengthwise limit forces crosswise; at or under it,
lengthwise if a lengthwise slot is free.

In crosswise mode the hearth is one block: every interaction routes through any top filler and the piece
spans three cells, so it must be drawn on the principal's mesh. Whether the outer cells get culled cannot
be settled headlessly.

### Reheat rate (proposed)

The soak is per piece and scales on `V/A` (surface area, not mass), which governs how fast heat crosses into
a solid. The same constant should drive the cooling in `RollingPass.Cool` (`RollingPass.cs:190`), which
today takes a flat `RollingCoolRate` rather than an area-derived one.

No ×2 furnace multiplier: under an area law it puts every piece below a vanilla ingot's forge time. The
hearth soaks at forge rate per piece; its advantages over a forge are that a forge cannot hold a slab at all
and that the hearth soaks two or three pieces at once. The per-piece `V/A` table belongs to
[rolling](../processes/rolling.md).

### Planned pile placement

The hearth should stop authoring stock and compose it from the item's own shape: for each loaded slot,
tesselate the stock item's own shape (the same literal the held item uses), translate to the row anchor,
and rotate once at the end. Fifteen authored groups become zero, every rolled stage renders for free, the
bed and the held item become the same mesh, and the `Items1 / Items3 / Items2` trap disappears.

The [stock rack](stock-rack.md) needs the same computation - seat the item from its own shape, stack it,
resolve its textures dynamically so any mod's items work - so `StockPile.Place` belongs in exlib and must be
one system for both. Crosswise is then a different `rowAnchor` plus a 90° base yaw.

Implementation traps, all live: textures must come from the five-arg `ShapeTextureSource` (item-atlas UVs
are wrong for `ITerrainMeshPool`); the atlas insert is main-thread only while `OnTesselation` runs on the
chunk worker, so the tesselation pass may only read the cache; jitter must be reproducible (`MurmurHash3`,
not an RNG); cache one mesh per `(form, stage)` and transform the clone, or the cache is unbounded. The full
write-up lives with the [stock rack](stock-rack.md).

---

## Numbers

Values this page owns. FSM tunables are shown as bindings; the values belong to
[heat balance](../mechanics/heat-balance.md).

### Geometry

| Key | Value | File:line | What it does |
|---|---|---|---|
| firebox cells | the two `F` cells beside the hearth | layout, `CellRole.Firebox` | layout-derived - the bounds of the fuel bed. The charge box = the firebox, two cells |
| `ShaftCentre` | `(-2,0,1)` | `BlockEntityHeatingFurnace.cs:37` | sound origin; the hearth cell, out of the fire |
| `RequiresBlast` | `false` | `BlockEntityFireboxFurnace.cs:38` | natural draught; nothing can starve it (shared firebox branch) |
| `CellRole.Tuyere` / `CellRole.GasOutlet` / `CellRole.Pool` | `[]` | the layout, by drawing none of them | no blast, no plumbed exhaust, no crucible |
| `TuyereIntakeVolume` / `BlastPressureThreshold` | `0f` | `BlockEntityFireboxFurnace.cs:48-49` | hard-coded, never read - declared only to satisfy the contract |
| molten-product members | none overridden | `BlockEntityFurnaceCore.cs` | no pool ⇒ nothing to fill, drain or freeze |
| `Origin` | `(-6,-2)` | `BlockHeatingFurnaceCore.cs:66` | |
| declared cells | 109 | layout / golden `heating-core.json` | 68 brick, 10 slab, 12 filler, 13 air, 2 firebox, 1 each core/charge door/hearth/firebox door |

### Hearth

| Key | Value | File:line | What it does |
|---|---|---|---|
| `Rows` | 3 | `HeatingHearthLayout.cs:31` | slots on the bed, one piece each - the honest capacity |
| contents model | `ItemStack?[3]` | `BlockEntityHeatingHearth.cs:30` | one slot per row; whole stacks are never taken |
| footprint | `###` / `#0#`, `Origin(-1,-1)` | `BlockHeatingHearth.cs:46-57` | 3 wide × 2 deep, principal on the near row |
| `Resistance` / `Replaceable` | 6 / 400 | `BlockHeatingHearth.cs` | |
| tree keys | `row0` … `row2` | `BlockEntityHeatingHearth.cs:135-153` | stacks are `ResolveBlockOrItem`'d on read or they silently fail to draw |

### Stock recognition (hard-coded code prefixes)

| Prefix | Form | Item exists? |
|---|---|---|
| `stock-shingledbar` | `ShingledBloom` | yes, `iwex:stock-shingledbar` (`StockItemDefinitions.cs:37`) - but no survival route. ⛔ the enum member still reads `ShingledBloom`; it names the hearth's drawn element group, which the rename did not touch |
| `stock-shingledslab` | `ShingledSlab` | yes, `iwex:stock-shingledslab` - no survival route |
| `castbillet` | `CastBillet` | no item defines this code |
| `castbloom` | `CastBloom` | no item |
| `castslab` | `CastSlab` | no item |

`HeatingHearthLayout.cs:64-79`. Anything else is refused outright.

### Element map (hard-coded strings aimed at art)

| Row | Element group | Child suffix | Example |
|---|---|---|---|
| Left | `Items1` (pivot x0) | *(none)* | `Items1/ShingledBlooms/*` |
| Centre | `Items3` (pivot x16) | `3` | `Items3/ShingledBlooms3/*` |
| Right | `Items2` (pivot x32) | `2` | `Items2/ShingledBlooms2/*` |

`HearthRows.cs:41-48`, `HeatingHearthLayout.cs:35-58`. Always-drawn groups: `Base/*`, `BaseExtension/*`,
`Bed/*`.

### FSM bindings (values owned by [heat balance](../mechanics/heat-balance.md) / [rolling mill](rolling-mill.md))

| Member | Bound to | File:line |
|---|---|---|
| `MeltingPoint` | `IwexValues.RollingTempC` - a reheat target, not a melting point | `BlockEntityHeatingFurnace.cs:61` |
| ready-line lang key | `iwex:heatingfurnace-ready` | `BlockEntityHeatingFurnace.cs:67-68` |

---

## Drops

| Broken | Returns |
|---|---|
| core | itself (`maxstacksize 4`) |
| hearth / charge door | itself; fillers cleared first (`BlockFilledMegastructure.cs`) |
| walls, slabs, firebox door | the vanilla blocks, individually |
| firebox | see [firebox](firebox.md) |

Caution: loaded stock is destroyed. Neither `BlockHeatingHearth` nor `BlockEntityHeatingHearth` overrides
`GetDrops` or `OnBlockBroken`, so breaking a loaded bed silently eats up to three pieces of stock, each a
unique, unstackable work piece carrying its own per-side thickness and heat. Same hole as the
[puddling furnace](puddling-furnace.md)'s hearth, and worse here because the contents are irreplaceable
rather than re-craftable.

---

## Code

| Thing | Where |
|---|---|
| `BlockHeatingFurnaceCore : BlockFurnaceCoreBase` | `BlockStructures/Furnaces/Blocks/BlockHeatingFurnaceCore.cs:22` - def + layout only |
| `BlockEntityHeatingFurnace : BlockEntityFireboxFurnace` | `.../BlockEntities/BlockEntityHeatingFurnace.cs`; `ShaftCentre` `:37`; empty `SmeltCycle` `:53`; `MeltingPoint` `:61` |
| `BlockEntityFireboxFurnace` (shared branch) | `.../BlockEntities/BlockEntityFireboxFurnace.cs:33`; firebox charge `:108`, ignition `:141` |
| `BlockHeatingHearth : BlockFilledMegastructure` | `.../Blocks/BlockHeatingHearth.cs`; `RowAt` `:77-81`; `HandleInteract` |
| `BlockEntityHeatingHearth : BlockEntityFurnacePart` | `.../BlockEntities/BlockEntityHeatingHearth.cs:26`; `TryLoad` `:57-69`; `TryTake` `:73-84`; `OnTesselation` `:99-126` |
| `BlockChargeDoor` / `BlockEntityChargeDoor` | shared with the puddling furnace - see [that page](puddling-furnace.md#code) |
| `HeatingHearthLayout` (pure) | `BlockStructures/Furnaces/HeatingHearthLayout.cs:18` |
| `HearthRows` (pure, shared) | `BlockStructures/Furnaces/HearthRows.cs:17` |
| tests | `FurnacePartsTests.cs` - layout codes resolve; every heating-hearth element exists in the shipped shape; structural groups; the row groups are not in name order; access rule; offset → row; stock recognition, both directions |

**Where a caller hooks in.** Heat-into-stock goes in `SmeltCycle` (`BlockEntityHeatingFurnace.cs:53`); the
core calls it on the melt cadence once `_internalTemp` clears `MeltingPoint`. It needs a
`BlockEntityHeatingHearth` handle, which the core does not resolve today: add the lookup at
`GlobalOf(ShaftCentre)` in `OnStructureCompleted`, alongside `ScanForOutlets`. The stock's temperature is
vanilla's own attribute, so raising it is `SetTemperature` on the held stack, not a new field.

---

## Gotchas

**The class doc is stale.** `BlockEntityHeatingFurnace.cs:20-23` says the hearth rows and the heat that goes
into the pieces are not built. The hearth rows, their access rule and their renderer are live and pinned by
tests (`FurnacePartsTests.cs`); only heat-into-stock is absent.

**`Items1 / Items3 / Items2` are not in positional order.** The three groups' children are byte-identical,
so the group pivot decides where each draws: `Items1` at x0 = left, `Items3` at x16 = centre, `Items2` at
x32 = right (`HearthRows.cs:33-39`, `HeatingHearthLayout.cs:44-48`). Reading "Items2 = middle" off the name
puts every loaded piece one cell out, silently: selective-element matching drops an unknown name without an
exception, and a wrongly-known name draws in the wrong place. Pinned by `FurnacePartsTests`. The
composed-stock renderer deletes this trap outright.

**The element tree is pruned, not selectively matched.** `OnTesselation` loads the shape and calls
`ExShapeElements.Pruned` (`BlockEntityHeatingHearth.cs:108-119`) rather than the engine's
`selectiveElements`, whose per-segment prefix rule has silently kept or dropped the wrong subtree here
before. It returns `true` (`:126`) so the default block mesh is suppressed; otherwise every stock form would
show in every row at once.

**Stacks read off a tree carry no resolved collectible.** `FromTreeAttributes` calls `ResolveBlockOrItem`
per row (`BlockEntityHeatingHearth.cs:150`); without it the render path reads a null `Code` and the piece
silently fails to draw.

**`Melting` here means soaking.** This furnace's `MeltingPoint` is a reheat target
(`BlockEntityHeatingFurnace.cs:61`), so the shared FSM's `Melting` state is what a lit reheat furnace sits
in. It does nothing today because `SmeltCycle` is empty. The FSM itself belongs to
[heat balance](../mechanics/heat-balance.md).

**A fuller firebox runs the furnace cooler.** The charge-mass loss term scales with how full the charge box
is, and here the charge box is the firebox. The HUD prints both sides of the ledger. Model owned by
[heat balance](../mechanics/heat-balance.md).

**Six filler cells no part can produce, so the structure can never complete.** The layout declares 12
`exlib:structurefiller` cells. The hearth supplies five (`BlockHeatingHearth.cs:46-57`) and the charge door
one. The remaining six, the whole layer directly above the bed, have no producer.
`exlib:structurefiller` is `HandbookExclude()` with no creative entry (`BlockStructureFiller.cs`) so a
player cannot place one, and the completion walk matches filler cells like any other
(`BlockEntityMultiblockStructure.cs`; `IsAutoFilled` only hides them from the shopping list). Verified
against the golden. Identical in kind to the [puddling furnace](puddling-furnace.md)'s five. The fix is a
second footprint layer on the hearth, or, if those cells are meant to be the flame space over the bed, they
should be `a` rather than `f`.

**This furnace uses no charge lid.** `iwex:furnace-chargelid-{side}` exists, but it is the coke oven's and
the crucible furnace's part; the code at `BlockHeatingFurnaceCore.cs` is the authority for what this layout
requires.

---

## Open

| # | Gap | Size |
|---|---|---|
| 1 | The structure cannot complete - 6 orphan filler cells (above). Blocks everything else in game | small |
| 2 | Heat into stock. `SmeltCycle` is empty; the core does not resolve its own hearth | medium |
| 3 | Reheat rate on `V/A`, no ×2, and the matching cooling law in `RollingPass.Cool` (today flat `RollingCoolRate`). `k` is the loop's pacing knob and nothing fixes it yet - tune in play | medium |
| 4 | The crosswise seating. One contents model with a mode tag; `HearthRows`' 3-row enum and the `ItemStack?[3]` both have to go. Verify in game that the outer cells are not culled | medium |
| 5 | Composed-stock renderer + `StockPile.Place` in exlib, shared with the [stock rack](stock-rack.md). Deletes `HeatingHearthLayout`'s 15 authored groups and the `Items1/3/2` trap | medium |
| 6 | No recipe for core, hearth, charge door or firebox | small |
| 7 | Three of five recognised stock forms (`castbillet` / `castbloom` / `castslab`) have no item; the two that do have no survival route. Until the mill and the long cell produce stock, the hearth has nothing to hold | - |
| 8 | Roasting mode - the second job this machine was designed for, and the route that should displace hand-prepared fettle (`FettleRecipeDefinitions.cs`) | medium |
| 9 | No damper. This furnace has no air control of any kind; the [puddling](puddling-furnace.md) cap is not in its layout | small |
| 10 | Loaded stock is destroyed on break (above) | small |
| 11 | A deeper furnace raises both handling limits at once - a far better upgrade reward than a throughput multiplier. Not designed | - |
