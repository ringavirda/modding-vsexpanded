# Puddling Furnace

**Status** live - built and closed 2026-08-21 (U6, all eleven tasks): the furnace runs a whole heat - fettle,
charge, fire, melt down, rabble, draw out, clean - and the chassis is craftable. Not yet seen in game
**Mod** iiex (`IronIndustryExpanded`)

**Owns** everything specific to this machine: its multiblock layout and every cell offset in it, the
reverberatory geometry (`CellRole.Firebox` on the drawing's one `F` cell - whose bounds are the base's
charge box, so the redirection costs no C#; `ShaftCentre` = the hearth, the one offset still hand-declared),
the hearth's charge model (3 rows × 3 pigs, fettle-before-pig, the reachability rule as this hearth applies
it), the `PuddlingHearthLayout` element-name map and the Blockbench numbering trap in it, the chimney damper
and the two-door charge door as blocks, the "cleaned, not tapped" slag decision, and the exact reason this
process temperature (B8, closed 2026-08-21).

**Depends on**
[heat balance](../mechanics/heat-balance.md) - owns `T_process = T_in − T_loss`, the coke/air/preheat and
loss terms, the Idle → Firing → Melting FSM and its timers, and every `Bf*` / `Cupola*` tunable value ·
[firebox](firebox.md) - owns the shared fuel-bed block, its pool and the ignition arithmetic ·
[multiblock & filler structures](../mechanics/multiblock.md) - owns the layout DSL, origin-is-the-negation,
oriented parts, invisible fillers and the completion walk ·
[definitions, recipes & config](../mechanics/recipes-config.md) - owns code-first defs, goldens and the
config file layout · [density rule](../mechanics/density-rule.md) - owns `1 vx³ = 2.5 u` and every mass
derived from it, including the pig · [blast furnace](blast-furnace-cold.md) - the pig source ·
[cupola](cupola.md) - the other pig consumer · [rolling mill](rolling-mill.md) and
[reheat furnace](reheat-furnace.md) - where the wrought product goes next ·
[ironmaking](../processes/ironmaking.md) · [layouts-workbench](../../../workbench/layouts.md) § 1

---

## Role

Puddling is the only route to wrought iron in the suite. The blast furnace makes pig; the
[cupola](cupola.md) remelts pig into cast iron; nothing else decarburises it. Without this machine the
forming line ([rolling mill](rolling-mill.md), the shear, the fastener line) has no wrought stock.

It is the first furnace in the suite where the fuel never touches the work: coke burns in a firebox off to
one side, the flame is drawn across a low roof and reverberates down onto a fettled bed, so pig is
decarburised by an oxidising flame and an iron-oxide bed rather than melted in contact with fuel. In code
that is one coordinate change - the furnace core walks its shaft box for charge, and that box is the bounds
of whichever fuel role the drawing marks, so marking one cell `CellRole.Firebox` beside the hearth instead
of a column under the charge is the entire conversion, with no override anywhere.

Puddling is a batch: nine pigs at a time, worked through a door with hand tools.

---

## Structure

Anchor `iiex:puddlingfurnacecore-{tier}-{side}`. Layout authored at
`BlockPuddlingFurnaceCore.cs:67-175`; the shipped form is the golden at
`mods/iiex/tests/goldens/iiex/blocktypes/furnace/puddlingcore.json`. Layout grammar,
origin rule and filler semantics: [multiblock](../mechanics/multiblock.md).

**Footprint** 8 wide (X) × 3 deep (Z) × 8 tall (Y), 91 declared cells. `Origin(-6, -1)` - the negation
of the core glyph's `(col 6, row 1)`, so `C` lands on the placed block.

| Glyph | Block | Count |
|---|---|---|
| `#` | `game:refractorybricks-good-tier*` | 60 |
| `-` | `game:brickslabs-fire-up-free` (vertical, never rotates) | 8 |
| `i` | `game:brickslabs-fire-south-free` (orientation-checked, turns with the build) | 2 |
| `K` | `game:cokeovendoor*` - the firebox stoking door | 1 |
| `C` | `iiex:puddlingfurnacecore-*` | 1 |
| `H` | `iiex:puddlinghearth-north` | 1 |
| `D` | `iiex:puddlingchargedoor-south` | 1 |
| `M` | `iiex:puddlingchimneycap-north` | 1 |
| `F` | `iiex:furnace-firebox` (any tier, any facing) - `CellRole.Firebox` | 1 |
| `f` | `exlib:structurefiller` | 6 |
| `a` | `game:air` - the ash pit and roof voids | 5 |
| `A` | `game:air` - the chimney bore, `CellRole.Flue` | 4 |

There is no grating glyph: the firebars are part of the firebox block itself, and the cell under it is
left as air - the ash pit, which is not modelled ([firebox](firebox.md)).

**Cells that matter**, in the anchor's own north frame (`C` = `(0,0,0)`):

| Cell | What it is | Declared at |
|---|---|---|
| `(0, 0, 0)` | the core / anchor | layout `C` |
| `(-2, 0, 0)` | hearth principal, `ShaftCentre` | `BlockEntityPuddlingFurnace.cs:43` |
| `(-3, 0, 0)`, `(-1, 0, 0)` | hearth left / right rows (fillers, and the interface) | `BlockPuddlingHearth.cs:50-56` |
| `(-5, 1, 0)` | the firebox - the drawing's one `F` cell, marked `CellRole.Firebox`; its bounds are the base's charge box | `BlockPuddlingFurnaceCore.cs` (the layout) |
| `(-2, 1, 1)` | charge door principal; `(-2, 2, 1)` its upper filler | `BlockChargeDoor.cs` (`UpperHalf`) |
| `(-1, 7, 0)` | chimney cap | layout `M`; its own filler offsets at `BlockPuddlingChimneyCap.cs:49-57` |
| `(0, 3..6, 0)` | the chimney bore (`A`), `CellRole.Flue` | layout |

**Interactive cells**: the three hearth cells (each picks its own row), the charge door and its upper filler
(both route to the same block entity), and the chimney cap. Everything else is wall.

**Orientation** comes off the core's `side` variant via the vanilla `HorizontalOrientable` behaviour
(`BlockEntityFurnaceCore.cs:356-362`); each part re-derives its own footprint angle the same way
(`BlockPuddlingHearth.cs:64`, `BlockChargeDoor.cs:98-99`, `BlockPuddlingChimneyCap.cs:69`).

> The slab shoulders (`-` / `i`) around the doorway are load-bearing on the gameplay: the half-height course
> opens the mouth far enough that a player can reach all three hearth rows through it, which is what lets the
> hearth's flanking fillers be the interface instead of needing a split mesh
> (`BlockPuddlingFurnaceCore.cs:76-81`).

---

## Assets

| Part | Editable | Runtime | Notes |
|---|---|---|---|
| core | - | `game:block/basic/cube` | a plain cube; the `n` marker and `pf` type label are texture overlays on the north/south faces (`BlockPuddlingFurnaceCore.cs:38-40`) |
| hearth | `workbench/shapes/furnace-megablock-puddlinghearth.json` | `mods/iiex/assets/iiex/shapes/furnaces/puddlinghearth.json` | elements `Base`, `BaseExtension`, `Bed`, `Fettle` (3 cubes), `Pigs` (9). No animations - drawn by `OnTesselation` with a pruned element set |
| charge door | `workbench/shapes/furnace-megablock-puddlingchargedoor.json` | `mods/iiex/assets/iiex/shapes/furnaces/puddlingchargedoor.json` | elements `Bricks`, `Rails`, `Door`, `Tools`. Animations `closed-main`, `open-main`, `open-small`, `rabbling`, `paddle` |
| chimney cap | `workbench/shapes/furnace-megablock-puddlingchimneycap.json` | `mods/iiex/assets/iiex/shapes/furnaces/puddlingchimneycap.json` | elements `Base`, `ChimneyCap`, `ControlRod`; animations `idle` (= shut) and `open` |

The charge door's `rabbling` and `paddle` clips are drawn but unreachable: code only ever plays
`main` / `mainShut` / `small`, read from block attributes (`BlockEntityChargeDoor.cs:34-37`, `:70-95`).

Textures are vanilla (`fire1`, `burned`, `iron5`, `cast-iron1` on the door; `burned`, `cast-iron1`, `fettle`,
`iron` on the hearth). The hearth's fettle texture is shared with the
[fettle item](../items/fettle.md) so the stuff in hand and the stuff on the bed read as one material.

---

## Construction

There is no recipe for any of it. This is a blocker, and it is broader than B1.

`iiex` ships twenty grid-recipe goldens (`mods/iiex/tests/goldens/iiex/recipes/grid/`) and
none of them outputs `puddlingfurnacecore`, `puddlinghearth`, `puddlingchargedoor` or
`puddlingchimneycap`. `FurnaceRecipeDefinitions.cs:16-17` defines exactly two recipe groups, blast furnace
and cupola. The only puddling-adjacent recipe that exists is the consumable:

| Recipe | Output | File |
|---|---|---|
| Puddling Fettle | `iiex:puddlingfettle` ×3 from 3 × any `fettlestock` | `FettleRecipeDefinitions.cs:29-42` |

The furnace is creative-only today, with no RCC construction stages either - every part is a plain placed
block, not a [right-click-constructable](../mechanics/recipes-config.md). The bricks, slabs, grating and
firebox door are vanilla and craftable already; only the four `iiex` blocks are missing. Unlike the
[blast furnace](blast-furnace-cold.md), nothing here needs a pipe, so B1 does not apply: a puddling-furnace
recipe can be written today without unblocking anything else.

---

## Operation

### What works today

| Verb | Where | Effect | Code |
|---|---|---|---|
| RMB holding `iiex:puddlingfettle` | any hearth cell | lays fettling in that row, consumes 1 | `BlockPuddlingHearth.cs:103-110` |
| RMB holding `iiex:pig` | any hearth cell | lays 1 pig in that row (row must be fettled) | `BlockPuddlingHearth.cs:112-121` |
| RMB | charge door (either cell) | swings the main door | `BlockChargeDoor.cs:118-121` |
| Sneak + RMB | charge door | swings the small working door | `BlockChargeDoor.cs:118-119` |
| RMB | chimney cap | throws the damper | `BlockPuddlingChimneyCap.cs:79-80` |
| Ctrl + Shift + RMB | core, hearth, door, cap | build-outline projection + shopping list | `BlockPuddlingHearth.cs:89`, etc. |

Charge order is the mechanic. Fettle first, then pig: an unfettled row refuses pig
(`BlockEntityPuddlingHearth.cs:67`), because pig laid on a bare bottom plate would weld itself to it, which
is why fettling is a running cost rather than a build cost. Re-fettling a row that already carries fettle
or pig is likewise refused (`:52-53`).

Reach: a loaded centre row blocks both flanks (`HearthRows.cs:75-76`), so the flanks are loaded first and
drawn last, and the centre is the fast lane for a single piece. The same rule governs the
[reheat furnace](reheat-furnace.md)'s hearth.

### What the shell does not do

Everything after charging. `BlockEntityPuddlingFurnace` overrides only geometry and tunables; its
`SmeltCycle` is an empty override (`BlockEntityPuddlingFurnace.cs:74`), so there is no cycle, no rabbling,
no balling-up and no product. `ClearBed()` (`BlockEntityPuddlingHearth.cs:83-91`), the design's answer to
where the slag goes, has no caller anywhere in the tree. Nor do `PigCount` / `IsFullyCharged` outside the
hearth's own HUD: the furnace core never reads its own hearth. `IsVenting` on the door
(`BlockEntityChargeDoor.cs:44`) and `IsOpen` on the damper (`BlockEntityPuddlingChimneyCap.cs:22`) have no
consumers, so the furnace's advertised "only air control" controls nothing.

### The intended cycle (not built)

Fettle 3 rows → charge 9 pigs → light the firebox → melt down → rabble through the small door → ball up →
draw the balls out one at a time → clean the bed, which returns the spent fettle and the tap cinder
together as next heat's `fettlestock`. Balls go to the helve hammer to be shingled into blooms, not straight
to bar. Product masses and the ball/bloom ladder belong to the
[density rule](../mechanics/density-rule.md).

No slag tap, by decision: a puddling furnace makes far too little slag to plumb and what it makes is stiff
tap cinder, not a pour, so the hearth is cleaned, not tapped. There is no metal tap either - puddled iron
leaves as pasty balls through the charge door. The layout marks neither role, so `MetalTapPos` and
`SlagTapPos` are null on both reverberatory hearths, and nothing reads them: the tap block's HUD and
`DrainProducts` are shaft-branch only (the pool readouts are overridden only on `BlockEntityShaftFurnace`).

---

## Numbers

Values this page owns. FSM tunables are shown as the binding; their numeric values belong to
[heat balance](../mechanics/heat-balance.md).

### Geometry

| Key | Value | File:line | What it does |
|---|---|---|---|
| `ShaftBox` | `(-5,1,0)` … `(-5,1,0)` | `BlockPuddlingFurnaceCore.cs` (the layout) | layout-derived - the bounds of `CellRole.Firebox`. The charge box = the firebox. One cell |
| `ShaftCentre` | `(-2,0,0)` | `BlockEntityPuddlingFurnace.cs:43` | sound origin + the hearth cell |
| `CellRole.Tuyere` | `[]` | the layout, by drawing no `Y` | No blast; the absence needs no declaration |
| `CellRole.GasOutlet` | `[]` | the layout, by drawing no pipe outlet | The open stack is the exhaust; nothing plumbs it |
| `CellRole.Pool` | `[]` | the layout, by marking no crucible | a hearth pools no metal |
| `RequiresBlast` | `false` | `BlockEntityFireboxFurnace.cs:39` | the firebox branch: natural draught only - the furnace can never air-starve |
| `CellRole.SlagTap` | `[]` → `SlagTapPos == null` | the layout, by drawing no tap | cleaned, not tapped |
| `CellRole.MetalTap` | `[]` → `MetalTapPos == null` | the layout, by drawing no tap | pasty balls leave through the door |
| `Origin` | `(-6,-1)` | `BlockPuddlingFurnaceCore.cs:72` | negation of `C`'s `(col 6, row 1)` |
| declared cells | 91 | golden `puddlingcore.json` | 60 brick, 10 slab, 6 filler, 9 air, 1 each of firebox door/core/hearth/door/cap/firebox |

### Hearth

| Key | Value | File:line | What it does |
|---|---|---|---|
| `PigsPerRow` | 3 | `PuddlingHearthLayout.cs:18` | a pig is triangular in section, so two lie on the bed and a third nests in the groove |
| `PigCapacity` | 9 | `PuddlingHearthLayout.cs:21` | the design's "9 pigs" - one full heat |
| rows | 3 | `HeatingHearthLayout.cs:31` (shared) | the puddling hearth sizes its arrays off the heating hearth's `Rows` constant |
| `PerHearthCell` (fettle) | 1 | `Items/FettleItemDefinitions.cs:46` | a full ram-up costs 3 |
| footprint | `#0#`, `Origin(-1,0)` | `BlockPuddlingHearth.cs:47-52` | 3 × 1, one filler each side |
| `Resistance` / `Replaceable` | 6 / 400 | `BlockPuddlingHearth.cs:55-57` | |

### Element-name maps (hard-coded strings aimed at art)

| Row | Fettle element | Pig elements | File:line |
|---|---|---|---|
| Left | `Fettle/Cube13` | `Pigs/Pig1..3` | `PuddlingHearthLayout.cs:27-32`, `:43-48` |
| Centre | `Fettle/Cube11` | `Pigs/Pig4..6` | " |
| Right | `Fettle/Cube12` | `Pigs/Pig7..9` | " |

Always-drawn structural groups: `Base/*`, `BaseExtension/*`, `Bed/*` (`PuddlingHearthLayout.cs:56`).

### FSM bindings (values owned by [heat balance](../mechanics/heat-balance.md) and [firebox](firebox.md))

| Member | Bound to | File:line |
|---|---|---|
| `MeltingPoint` | `IiexValues.PuddlingProcessTempC` = 1400 - a process temperature, not a melting point *(2026-08-21)* | `BlockEntityPuddlingFurnace.cs` |
| `StackCourses` | `LocalCellsWithRole(CellRole.Flue).Count` = 4, off the drawing *(2026-08-21)* | `BlockEntityFireboxFurnace.cs` |
| `DamperOpen` / `Venting` | the chimney cap's `IsOpen` and the door's `IsVenting` - the two operating inputs | `BlockEntityFireboxFurnace.cs` |
| `ChargeLossFull` / `TransferLoss` | `FireboxChargeLossFull` 100 / `ReverberatoryTransferLoss` 100 *(2026-08-21)* | `BlockEntityFireboxFurnace.cs` |
| `DisruptionMixFloor` | half the bed, sealed - B8's fifth cause *(2026-08-21)* | `BlockEntityFireboxFurnace.cs` |
| `RequiresBlast` / `TuyereIntakeVolume` / `BlastPressureThreshold` | `false` / `0` / `0` - the firebox branch | `BlockEntityFireboxFurnace.cs:39-50` |
| `ShaftHoldsLayeredCharge` | `false`, sealed - a hearth can never inherit a shaft's charge column | `BlockEntityFireboxFurnace.cs` |
| `ChargeCapacityUnits` | `FireboxCellCount × FireboxMixPerCell`, sealed - "lit" means the bed is full | `BlockEntityFireboxFurnace.cs:224-225` |
| `SmeltCycle` | empty - the cycle is unbuilt | `BlockEntityPuddlingFurnace.cs:74` |

B8 closed on 2026-08-21; see [Gotchas](#gotchas) for what each of its five causes turned out to be.

---

## Drops

| Broken | Returns |
|---|---|
| core | itself (`maxstacksize 4`) |
| hearth / charge door / chimney cap | itself, one each; their fillers are cleared first (`BlockFilledMegastructure.cs:85-95`) |
| walls, slabs, grating, firebox door | the vanilla blocks, individually |

Hearth contents are destroyed. Neither `BlockPuddlingHearth` nor `BlockEntityPuddlingHearth` overrides
`GetDrops` or `OnBlockBroken`, so breaking a fettled and charged bed silently eats up to 3 fettle and 9 pigs.
The [reheat furnace](reheat-furnace.md) has the identical hole.

Extinguish residue is the shared core's and belongs to [ironmaking](../processes/ironmaking.md); on this
furnace the structure cannot yet complete ([Open](#open) #1), so it is unreachable today.

---

## Code

| Thing | Where |
|---|---|
| `BlockPuddlingFurnaceCore : BlockFurnaceCoreBase` | `BlockStructures/Furnaces/Blocks/BlockPuddlingFurnaceCore.cs:23` - def + layout only, no behaviour |
| `BlockEntityPuddlingFurnace : BlockEntityFireboxFurnace` | `BlockStructures/Furnaces/BlockEntities/BlockEntityPuddlingFurnace.cs:31` - geometry + tunables only: `ShaftCentre` `:43`, `MeltingPoint` `:63`, empty `SmeltCycle` `:74` |
| `BlockEntityFireboxFurnace` (the shared reverberatory branch) | `BlockStructures/Furnaces/BlockEntities/BlockEntityFireboxFurnace.cs:34` - `RequiresBlast` false, sealed charge members; the fuel pool is [firebox](firebox.md)'s |
| `BlockPuddlingHearth : BlockFilledMegastructure` | `.../Blocks/BlockPuddlingHearth.cs:25`; `RowAt` `:73-78`, `HandleInteract` `:80-124` |
| `BlockEntityPuddlingHearth : BlockEntityFurnacePart` | `.../BlockEntities/BlockEntityPuddlingHearth.cs:28`; `TryFettle` `:48-57`, `TryChargePig` `:63-72`, `ClearBed` `:83-91` (uncalled), `OnTesselation` `:108-131` |
| `BlockChargeDoor` (2 blocktypes from 1 class) | `.../Blocks/BlockChargeDoor.cs:30`; `Definitions` `:38-56`; the shared `Door()` fragment `:58-93` |
| `BlockEntityChargeDoor` | `.../BlockEntities/BlockEntityChargeDoor.cs:20`; `ToggleMain` `:49-54`, `ToggleSmall` `:57-64`, `ApplyPose` `:70-95`, `IsVenting` `:44` (unread) |
| `BlockPuddlingChimneyCap` | `.../Blocks/BlockPuddlingChimneyCap.cs:22` |
| `BlockEntityPuddlingChimneyCap` | `.../BlockEntities/BlockEntityPuddlingChimneyCap.cs:19`; `Toggle` `:25-30`, `IsOpen` `:22` (unread) |
| `PuddlingHearthLayout` (pure) | `BlockStructures/Furnaces/PuddlingHearthLayout.cs:15` |
| `HearthRows` (pure, shared with reheat) | `BlockStructures/Furnaces/HearthRows.cs:17` |
| `BlockEntityFurnacePart` (anchor link + toggle animator) | `BlockStructures/Furnaces/BlockEntityFurnacePart.cs:32` |
| tests | `mods/iiex/tests/Blocks/Furnaces/FurnacePartsTests.cs:34` (every layout code resolves), `:61` (every hearth element exists in the shipped shape), `:94` (nine distinct pig elements), `:105` (the row groups are not in name order), `:125` (the access rule) |

**Where a caller hooks in.** The cycle goes in `SmeltCycle(chargeHandle)` - the core calls it on the melt
cadence (`BlockEntityFurnaceCore.cs:710-715`). It will need the hearth, which the core does not currently
resolve at all: add a `BlockEntityPuddlingHearth` lookup at `GlobalOf(ShaftCentre)` in
`OnStructureCompleted` (`BlockEntityFurnaceCore.cs:364`), the way the taps are resolved by `ScanForOutlets`.

---

## Gotchas

> **Closed 2026-08-21, all five causes.** The arithmetic below is the finding as it stood; what shipped is
> in the two sections that follow it. The furnace now settles at **1421.6 °C** against a process
> temperature of **1400** - twenty-one degrees of headroom, carried entirely by the chimney: the same
> hearth on a bare flue reads 1192.5 °C, with the damper shut 907.1, with the main door open 1105.0.

**B8 - the furnace cannot melt.** (Its first half, an unreachable ignition threshold, closed with the
[firebox](firebox.md): "lit" is now the bed being full, derived from the drawn shape.) With no tuyeres,
`blastSupplyFrac` is 0, so the air factor pins at the natural-draught value and `T_in` has a hard ceiling.
Running that ceiling through `ComputeHeatBalance` with the fuel factor at its clamp gives T_in ≈ 1512.5 °C;
`T_loss` is never below the radiation base (120 °C) even with an empty hearth, so `T_process ≤ ~1392.5 °C`
against an inherited melt point of 1482 °C, and a charged firebox sits lower still once the charge-loss term
bites. Every term is [heat balance](../mechanics/heat-balance.md)'s; the arithmetic is the finding. A
reverberatory furnace needs either its own melt point (puddling works pig in the pasty state, well below
1482) or a draught model that the damper actually feeds. The code carries the defect knowingly
(`BlockEntityPuddlingFurnace.cs:57-63`).

#### What actually shipped, 2026-08-21

Three pieces, in the order they landed:

1. **The losses became per-machine.** `ChargeLossFull` and `TransferLoss` are virtuals on
   `BlockEntityFurnaceCore`, defaulting to the blast furnace's behaviour so nothing moved for it. A firebox
   pays **100 °C** for its bed rather than the shaft's 310 for a descending column, and **100 °C** across
   the bridge to the work. `TransferLoss` is deliberately left virtual rather than made a branch constant:
   the crucible furnace is a firebox machine whose pots stand in the coke with no bridge at all, and a
   constant here would make it unbuildable.
2. **The draught became a curve.** `StackDraught.NaturalDraughtFor(courses, damperOpen, venting)` replaces
   the flat `BfNaturalDraughtFactor` read - `base + gain·√courses − friction·courses²`, which rises,
   peaks and declines rather than clamping. The damper and the doors scale it, which is the first use
   either `IsOpen` or `IsVenting` has ever had.
3. **`StackCourses` is read off the drawing**, not walked up the world:
   `LocalCellsWithRole(CellRole.Flue).Count`, which is 4 here and 2 on the reheat furnace. **Ruled
   2026-08-21 (owner): this furnace has no player-built chimney** - *"it is fixed in layout because of the
   cap"* - so there is nothing above the cap for a walk to find, and no cache to invalidate. The counted
   walk arrives with the coke oven and crucible furnace, which do have player-built stacks. The cap moved
   from `(-1,7,0)` to `(0,7,0)`, over the flue it caps, with its housing filler at `(0,7,1)`.

The transfer loss landed at 100 °C rather than the 250 this page proposed, and the reason is the curve:
250 was computed against a draught model that saturated at 0.85, giving `T_in` ≈ 1906. The settled peaking
curve at four courses gives 1741.6, so the bridge loss comes down to keep the `T_process` the design
intended. Every coefficient here is still a proposal to calibrate in play; what is settled is the shape.

`IsVenting` also changed meaning: it is now **the main door only**. Not dumping the heat is the entire
reason the small working door exists, and rabbling happens through it - if it vented, the bath would cool
on every one of the sixteen gestures.

#### B8's fifth cause, found and fixed 2026-08-21 - the hearth went out before it could get hot

Ahead of every temperature argument below sat a plainer defect: a lit hearth **extinguished itself after
30 seconds**, so no ceiling was ever tested in play. `DisruptionMixFloor` was a flat `144` on
`BlockEntityFurnaceCore` - a shaft number, never overridden - and a lit furnace holding less than the floor
counts a disruption on every tick and goes out when the extinguish grace expires. A firebox holds
`cells x 12` units: **12** for the puddling furnace's one cell, **24** for the reheat furnace's two. Both
are far under 144, so a full firebox was at once full enough to light and too empty to stay lit.

The fix is on the branch, not the leaf: `BlockEntityFireboxFurnace` now derives its floor from its own
capacity (`FireboxDisruptionFloorFraction`, half by default) and seals it, so a leaf cannot reintroduce a
hand-picked number. The floor still bites from the other side - fuel raked back out of a lit box below half
the bed puts the fire out on the grace - which is the mechanic it was always meant to be.

It is firebox-only. The shaft branch derives its state per column (`DerivesState`) and never reaches the
disruption block, so nothing about the shaft's own floor is blessed by this.

The first thing the fix uncovered: the **reheat furnace now reaches its melt phase**, which no reverberatory
hearth in this mod had ever done. Its `MeltingPoint` is `RollingTempC`, well inside what a firebox reaches -
only the inherited floor stood between it and working.
`FireboxTickTests.A_lit_hearth_crosses_into_its_melt_phase` is the check;
`FurnaceBranchGuards.NoFireboxCarriesAFloorAboveItsOwnCapacity` is the law that keeps it.

#### Settled 2026-08-05 - `PuddlingProcessTempC = 1400 °C`, and it is both of those fixes

1400 is the window the process happens in, bracketed by this mod's own two melting points:

> `CupolaCastIronMeltingPoint` **1200** &nbsp;<&nbsp; **1400** &nbsp;<&nbsp; `BfIronMeltingPoint` **1482**

Hot enough to melt pig down; too cool for decarburised iron to stay liquid. As carbon leaves the bath the
metal's own melting point climbs from ~1200 toward pure iron's ~1538, crosses the bath temperature partway
through, and the iron "comes to nature" - it balls up. The ball is emergent from the temperature window, not
a scripted stage. Both alternatives break it: 1482 (the inherited value) melts the wrought iron, and 1200
freezes the decarburised metal hard instead of leaving it pasty. Neither gives a ball.

1400 sits 7.5 °C above the flat-draught ceiling computed above (1392.5 °C), which is what makes the chimney
load-bearing: at zero courses the furnace misses its own process temperature, and the player buys the
difference by building the stack taller. So the damper fix and the melt-point fix are the same fix - the
right melt point is the one the draught model has to reach.

Calibration consequence: if ~1400 needs more courses than a player will plausibly build, the losses move,
not this number (the per-machine `ChargeLossFull` / `TransferLoss` virtuals). A shut damper means less
draught and a cooler fire (settled 2026-08-05).

**Three filler cells no part can produce, so the structure can never complete.** The layout declares 6
`exlib:structurefiller` cells. Parts supply three - the hearth's two flanks at y 0
(`BlockPuddlingHearth.cs:50-56`) and the charge door's upper half (`BlockChargeDoor.cs`, `UpperHalf`). The
remaining three, `(-3,1,0)`, `(-2,1,0)` and `(-1,1,0)` above the hearth, have no producer.
`exlib:structurefiller` is `HandbookExclude()` with no creative entry (`BlockStructureFiller.cs:33-51`), so
a player cannot place one, and `IncompleteBlockCount` (`BlockEntityFurnaceCore.cs` →
`BlockEntityMultiblockStructure.cs:304-327`) matches every offset including filler cells - `IsAutoFilled`
(`:428-430`) only hides them from the shopping list. The hearth wants a second footprint layer.

**Stale source comments.**
* `BlockPuddlingFurnaceCore.cs:30-34` says "origin x=-3/z=-2" and lists `G` (grating) and `c` (fuel)
  glyphs; the code is `Origin(-6,-1)` (`:72`) and the legend has neither - the fuel cell is `F`, the
  firebox block.
* The core's class doc (`:17-19`) describes "a flue course under the hearth (the grating cells)"; the
  layout has no grating at all.
* `BlockEntityPuddlingFurnace.cs:26-28` says the layout has five filler cells with no producer; the
  current drawing has three.
* `ItemPig.cs:11` summarises the pig as 150 units; the shipped constant is `PigUnits = 375` (`:39`).

**Invariants easy to break.**
* `PuddlingHearthLayout`'s `Fettle` cubes are not in positional order - `Cube13` is left, `Cube11`
  centre, `Cube12` right, verified against absolute X in the shipped shape
  (`PuddlingHearthLayout.cs:24-26`). Blockbench numbers elements in creation order and a re-export re-rolls
  it. Selective-element matching drops an unknown name without raising anything, and a name that exists
  but belongs to the wrong cell draws the charge one row over. Pinned by `FurnacePartsTests.cs:61`
  and `:105`.
* `_pigs[i]` is clamped on read from the tree (`BlockEntityPuddlingHearth.cs:154`) so a hand-edited save
  cannot ask for a pig element that does not exist.
* `OnTesselation` returns `true` (`:130`) - the block entity draws the whole mesh, so the default block
  mesh must not also be drawn or every pig and both fettle beds render at once.
* `TryFettle` / `TryChargePig` both write `!HearthRows.CanReach(row, CentreLoaded) && row != Row.Centre`
  (`:50`, `:65`). The second clause is redundant, since `CanReach` already returns true for the centre
  (`HearthRows.cs:75-76`), but harmless.
* The chimney cap's rest pose is the `idle` clip held, not stopped (`BlockEntityPuddlingChimneyCap.cs:34`):
  the shape is drawn by the animator, so with no clip running the cap vanishes.

---

## Open

| # | Gap | Size |
|---|---|---|
| 1 | ~~The structure cannot complete - 3 orphan filler cells above the hearth~~ **Closed 2026-08-21.** The hearth's second course is declared and the chimney cap moved over the flue it caps; `FurnaceFillerAccountingTests` is the guard, and it covers all four furnace layouts in both directions | - |
| 2 | ~~B8~~ **Closed 2026-08-21.** `PuddlingProcessTempC = 1400`, per-machine losses, and the draught curve the damper and doors feed. The furnace settles at 1421.6 °C with its four courses pulling, 1192.5 with no stack, 907.1 with the damper shut | - |
| 3 | ~~No recipe for any of the four blocks~~ **Closed 2026-08-21** (U6.11). All four, plus the shared firebox, the reheat core and hearth, and the plain charge door - one `reverberatory` group, because grid-pattern collision is a property of the file. The rabble and paddle have their own grid recipes | - |
| 4 | ~~The whole cycle~~ **Closed 2026-08-21** (U6.8-U6.10). Melt-down on the core's cadence, one ball per rabbling stroke through the small door, drawing out on the paddle, and `ClearBed` returning the spent fettle with exactly 3 tap cinder - which is what closes the fettle loop. A 3 s cooldown paces both strokes; the bath freezes if the fire goes out | - |
| 5 | ~~The core does not resolve its own hearth~~ **Closed 2026-08-21** (U6.7). Resolved in `ScanForOutlets` beside the damper and the door, so all three refresh on the schedule the taps do; rotation is free through `GlobalOf` | - |
| 6 | ~~The damper and the doors are read by nothing~~ **Closed 2026-08-21.** `StackDraught.NaturalDraughtFor(courses, damperOpen, venting)` reads both, and the block info names the draught and what is costing it | - |
| 7a | ⛔ **Nothing has been seen in game.** The bath element, the two tool items, the working strokes on the door and the eight recipes are all unwalked | - |
| 7 | Wrought-ball mass is unsettled - it belongs to [density rule](../mechanics/density-rule.md) and must land before yields here can be written. (The pig shipped at 375 u, `Items/ItemPig.cs:39`) | - |
