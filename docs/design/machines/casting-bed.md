# Sand Casting Bed (pig bed)
**Status** live — grid recipe + RCC construction, carving on both sides, basin interaction and harvest all
work; the runtime shape is still untracked in git (§ Open)   **Mod** iiex

**Owns**
- The bed's geometry as a slot model: 4 rows × 3 slots = 12 cells, which slots are molds and which are the
  runner spine, the per-row impression count, and the 20-casting bed capacity.
- The three slot states (`Sand` / `Runner` / `Mold`), the one-mold-serves-both-castings rule, and the fact
  that shake-out destroys the impression while leaving the sand.
- Every capacity the bed declares or computes (basin 200, runner 50, mold = impressions × pig units -
  derived on the declaration and re-applied on carve and load).
- The bed's carve / harvest verbs, the greedy pig-chunk-bit denomination on collection, and the
  slag-brick dispatch.
- The bed's RCC construction stages and their material costs, its variant groups, its shape, and its
  drops.
- The bed's own client surfaces (which cuboid each cell's molten renderer uses).
- The bed-specific open items in § Open.

**Does not own — cited only**
- [molten network](../mechanics/molten-network.md) — the `IMoltenCell` contract, `BEBehaviorMoltenCell`,
  the flow driver and how the bed's private copy differs from the network one, `MoltenFlowRate`,
  `MoltenMinFlowAmount`, the hard-coded `PullRatePerTick = 25`, the cooldown/solidify/hardened model, and
  the whole throughput-unification question.
- [density rule](../mechanics/density-rule.md) — 1 vx³ = 2.5 u, and the pig's 375 u mass.
- [pig](../items/pig.md) — the pig / pigchunk / pigbit item family and its canonical masses.
- [multiblock & filler structures](../mechanics/multiblock.md) — the filler footprint system, behaviour-capable
  fillers, and the interaction rerouting the bed's carve/harvest clicks ride on.
- [recipes & config](../mechanics/recipes-config.md) — code-first block defs, RCC stages, goldens.
- [molten canal](molten-canal.md) — the run that feeds the bed.
- [cold blast furnace](blast-furnace-cold.md) — the tap that fills the run, and its per-tick hand-down rate.
- [casting cell](casting-cell.md) — the other sand station, and the contrast the bed exists against.

---

## Role

The pig bed is the bulk caster: a canal-fed sand floor that takes a whole heat and freezes it into
carriable denominations, with no per-cast labour and no per-cast material. That is the opposite economics
from the [casting cell](casting-cell.md), which charges a ram-up per casting because it makes capital
goods. Liquid metal never becomes an inventory problem: the furnace taps into a
[canal](molten-canal.md), the canal ends at the bed, and what the player picks up is already solid.

Its second job is waste. One carve shape casts both products - iron makes pigs, slag makes bricks - so a
slag tap needs no second station and no second gesture (`BlockEntitySandCastingBed.cs:438-488`).

---

## Structure

A 3 × 1 × 4 filled megablock: one principal plus eleven invisible fillers. The footprint is generated from
the slot table rather than hand-typed, so the carved surface and the footprint cannot disagree about where
a slot is (`BlockSandCastingBed.cs:134-143`, `SandBedLayout.cs:177`). It is not in
`docs/internal/workbench/layouts.md` - it is a filler footprint, not an ASCII multiblock layout.

| | West (dx −1) | Centre (dx 0) | East (dx +1) |
|---|---|---|---|
| **Row 1** (dz 0) | Mold - 2 impressions | Principal - the pour basin | Mold - 2 impressions |
| **Row 2** (dz 1) | Mold - 3 | Runner | Mold - 3 |
| **Row 3** (dz 2) | Mold - 3 | Runner | Mold - 3 |
| **Row 4** (dz 3) | Mold - 2 | Runner | Mold - 2 |

- Slot identity is `BedSlot(Row, Side)` (`SandBedLayout.cs:40`); a centre slot is runner spine, a flank slot
  is mold (`:43`).
- Offsets: `OffsetOf` maps a slot to `(dx, dz)` in the north-orientation frame (`SandBedLayout.cs:115-124`),
  `SlotAt` inverts it (`:127-139`). The round trip is pinned
  (`test/…/Casting/SandBedLayoutTests.cs:66-78`).
- The principal is row 1's runner - it is the pour basin, and the only slot with no filler of its own
  (`BlockSandCastingBed.cs:151-165`, pinned at `SandBedLayoutTests.cs:58-64`).
- Every filler hosts a `BEBehaviorMoltenCell`: `RunnerCell` on the centre column, `MoldCell`
  (`drainFitting: true`, capacity derived from the slot's impression count) on the flanks
  (`BlockSandCastingBed.cs:35-47, 155`). The basin lives on the
  principal's own block entity as a `flowSource` cell (`:66-69`).
- Orientation: `StructureAngle = AngleFromSide(side) + 180` (`BlockSandCastingBed.cs:174-175`), paired with
  the shape's own `rotateYByType` (`:120-123`) because the model's body is authored extending the opposite
  way from the orientation convention. See Gotcha 1 for what that costs.

The bed narrows at both ends - row 1 gives ground to the basin's shoulders, row 4 to the back wall - so the
end rows take 2 impressions a side and the middle rows 3 (`SandBedLayout.cs:141-147`). That is the art's
geometry, and it is why a full bed is 20 castings and not a round 24 (`:150-151`).

---

## Assets

| Asset | Path | State |
|---|---|---|
| Runtime shape | `assets/iiex/shapes/casting/sandcastingbed.json` | untracked in git (`??`) - drawn but never committed |
| Editable source | `assets/editable/shapes/sandcasting-bed.json` | deleted (`D` in the working tree) |
| Sand texture | `GreenSandItemDefinitions.Texture` (`game:block/stone/sand/basalt` - green sand), bound to the shape's `andesite` key | `BlockSandCastingBed.cs:135` |
| Brick texture | `game:block/clay/brick/four/running/cream1` + `{brick}1` overlay | `:129-133` |
| Burned clay | `game:block/clay/vessel/sides/burned` | `:134` |
| Animation | none - the bed is static; the `Animatable` behaviour exists only so a `ConstructedAnimator` can tesselate the built elements | `:73`, `BlockEntitySandCastingBed.cs:69-83` |

The shape's element tree is the whole render model. The build stages add `Base`, `BaseExtension` and then
the `SandRunners` group; `SandBedLayout.Compose` drops the group entry and substitutes one path per slot
(`SandBedLayout.cs:241-259`), because the group on its own would draw every state of every slot stacked in
the same hole. Per slot the element is `Mold{row}{W|E}` / `RunnerCenter{row}`, with a `…Full` suffix meaning
uncarved (`:194-203`) - getting that backwards renders every finished bed as a carved one.

Element names are string literals against art, so a Blockbench re-export that re-rolls an auto-name produces
a silently missing chunk of bed. `SandBedLayoutTests` walks the shipped shape and asserts every emittable
name exists - which is also why the pre-rework `iiex:sandcasting-bed` shape must never come back
(`BlockSandCastingBed.cs:102-106`).

---

## Construction

A grid recipe places the bed block: `BBB,_H_` - 4 × `game:burnedbrick-*` (capturing `{brick}`) and a
hammer, out to `iiex:casting-sandbed-{brick}-n`
(`Recipes/Grid/CastingRecipeDefinitions.cs:48-64`). That places only the first course; the bulk of the cost
is charged by the construction stages.

Once placed, it is raised by right-click construction in three stages
(`BlockSandCastingBed.cs:74-103`):

| Stage | Requires | Adds elements | file:line |
|---|---|---|---|
| 1 | 8 × `game:burnedbrick-{brick}` (captures `{brick}`) | `Base` | `:78-84` |
| 2 | 16 × `game:burnedbrick-{brick}` | `BaseExtension` | `:86-89` |
| 3 | 12 × `iiex:greensand` *(the prepared moulding sand - the raw-sand `{sand}` variant group is gone)* | `SandRunners` | `:96-103` |

`brokenDropsRatio` is not set (`ConstructionStages.BrokenDropsRatio` is never called), so the bed salvages
at the behaviour default. No iiex `RccBrokenDropsRatio` config is registered with `ExRccSettings`, so
nothing overrides it.

The last stage finishes the bed uncarved - every slot is plain sand. The runners and molds are the player's
own work afterwards.

---

## Operation

```
canal delivers metal to any horizontal neighbour of the PRINCIPAL
      → basin (pull at PullRatePerTick)
      → basin-outward level equalisation across carved cells only
      → each mold hoards its charge and freezes
      → RMB a hardened mold: collect pigs / chunks / bits, impression destroyed, slot back to sand
```

| Verb | Where | Effect | file:line |
|---|---|---|---|
| RMB an empty filler cell | any slot cell | carve it - runner on the spine, impressions on the flanks. There is nothing to choose: the state is decided by which cell was clicked | `BlockEntitySandCastingBed.cs:343-363` |
| RMB a filler cell holding metal | any slot cell | harvest if `IsHardened`, else `iiex-castingbed-toohot` | `:371-409` |
| RMB the principal | basin cell | routes into the same carve/harvest path as the fillers; anything unhandled falls through to construction | `BlockSandCastingBed.cs:221-227` |

Routing: `OnCellInteract = TryHarvest(...) || TryCarveAt(...)` (`BlockEntitySandCastingBed.cs:337-338`) -
a cell holding metal is a harvest, an empty one is a carve, and anything else falls through to
construction. Filler clicks arrive through `IFillerInteractionTarget`; the principal's own click arrives
through the block's `OnBlockInteractStart` override (`BlockSandCastingBed.cs:221-227`), so every slot -
the basin included - reaches the same path.

**Intake.** Each server tick the bed drains any adjacent external `IMoltenCell` on any horizontal face of
the principal into the basin, at `PullRatePerTick` (`:267-287`). Because the bed's own cells live on a
behaviour, the `GetBlockEntity(...) is IMoltenCell` test matches only real canal nodes - never the bed's own -
which is what keeps the bed an isolated internal network while the canal outside carries a different metal.

**Flow.** Cells are ordered by Manhattan distance from the basin and each internal edge is driven once,
nearer → farther (`:221`, `:243-258`). An edge exists only where both ends are carved (`:250-255`):
uncarved and shaken-out sand is not a channel, so the heat runs exactly where the player cut it. Cells
still tick thermally either way, so a stranded charge cools. The edge rule itself is a near-copy of the
network's with one extra clause - a drain fitting never gives metal back (`:302-303`) - see
[molten network § the other two copies of the driver](../mechanics/molten-network.md).

**Harvest.** Units are denominated greedily into pigs / chunks / bits, conserving mass to within a sub-bit
crumb (`:416-424`). Slag denominates into `iiex:slagbrick` at one brick per pig cavity (`:475-480`). A
runner's stranded charge is never a casting - it comes back as recovered bits however good the metal was
(`:426-436`, `:459-473`). Shaking a casting out destroys the impression: the slot drops to plain sand
and stops being a channel until re-carved (`:402-406`).

**HUD.** The basin's fill and temperature, plus a ready/cooling mold count.
See Gotchas 1-2 for what that count gets wrong.

---

## Numbers

### Layout — `SandBedLayout.cs`

| key | value | file:line | what it does |
|---|---|---|---|
| `FirstRow` | `1` | `:85` | the basin's row |
| `Rows` | `4` | `:89` | bed depth |
| `LastRow` | `4` | `:91` | `FirstRow + Rows - 1` |
| `Slots.Length` | `12` | `:94-106` | 3 per row × 4 rows; the order is part of the save format |
| `ImpressionsPerMold(row)` | end rows `2`, middle rows `3` | `:147` | hard-coded, from the art's geometry |
| `BedCapacity` | `20` | `:150-151` | derived: `Σ impressions over the 8 mold slots` |
| `RunnerCapacity` | `50` | `:154` | hard-coded - a runner is a conduit, not a cavity |
| `CapacityOf(mold)` | `impressions × ItemPig.PigUnits` = 750 or 1125 | `:164-170` | re-applied on carve and on load |
| `RunnersGroup` | `"SandRunners"` | `:82` | the construction group `Compose` expands |
| `RowGroup(row)` | `"RunnerRow{row}"` | `:180` | element path segment |

### Block definition — `BlockSandCastingBed.cs`

| key | value | file:line | what it does |
|---|---|---|---|
| basin cell | `capacity 200`, `flowSource true` | `:66-69` | hosted on the principal; roots the internal ordering |
| `RunnerCell` declared | `capacity 50` | `:35-36` | the fallback while a spine slot is uncarved |
| `MoldCell` declared | `capacity = SandBedLayout.CapacityOf(slot, Mold)`, `drainFitting true` | `:43-47` | the fallback while a flank slot is uncarved - derived, never a literal |
| footprint | 11 filler cells | `:145-156` | generated from `SandBedLayout.FillerSlots` |
| `StructureAngle` | `AngleFromSide(side) + 180` | - | hard-coded +180 |
| resistance / mining tier / max stack | `3.5` / `0` / `1` | `:59-61` | |
| variants | `brick` (7) × `side` (4) - the `sand` group is gone | `:105-111` | |
| shape rotate | north 180 · east 90 · south 0 · west 270 | `:120-123` | |
| selection / collision box | `0..1` / `0..0.875` | `:136-137` | |

### Block entity — `BlockEntitySandCastingBed.cs`

| key | value | file:line | what it does |
|---|---|---|---|
| `PullRatePerTick` | *(see [molten network](../mechanics/molten-network.md) § Hard-coded)* | `:44` | hard-coded here, but the value and its conflict with the settled 50 u/s are that page's |
| server tick | `1000 ms` | `:70` | pull → flow → cool |
| client tick | `1000 ms` | `:82` | refresh the molten surfaces |
| basin pool surface | `Cuboidf(0, 12, 12, 16, 14, 14)` | `:505` | hard-coded |
| basin spout surface | `Cuboidf(6, 14, 10, 10, 16, 16)` | `:506` | hard-coded |
| runner cell surface | `Cuboidf(5, 1, 0, 11, 3, 16)` | `:519` | hard-coded |
| mold cell surface | `Cuboidf(2, 1, 3, 14, 4, 13)` | `:520` | hard-coded |
| save key | `bed_slots`, one byte per slot | `:637-660` | positional against `SandBedLayout.Slots` |

### Denominations (not this page's — see [pig](../items/pig.md) and [density rule](../mechanics/density-rule.md))

`Denominate` reads `ItemPig.PigUnits` / `ChunkUnits` / `BitUnits` (`ItemPig.cs:39-41`); the slag brick is
defined as `= PigUnits` so both products share one cavity (`SlagItemDefinitions.cs:27`). The pig is
375 u, so a carved mold holds 750 or 1125 and a fully carved bed holds 20 × 375 = 7500 u - see
[density rule](../mechanics/density-rule.md).

---

## Drops

| Path | Result | file:line |
|---|---|---|
| `Block.GetDrops` | empty array - the bed block itself never drops | `BlockSandCastingBed.cs:185-190` |
| Break | the RCC behaviour scatters the completed stages' materials at the salvage ratio, from `OnBlockBroken` (not `GetDrops`) | `ExRightClickConstructable.cs:55-66` |
| Metal standing in the bed | nothing. No `WouldSpillOnRemoval` handling, no recovery drop, no spill sound - a broken bed voids its charge | - |

---

## Code

| Type / member | file:line | Role |
|---|---|---|
| `SandBedLayout` | `SandBedLayout.cs:74` | pure; slot inventory, offsets, capacities, element paths |
| `SandBedLayout.Compose` | `:241` | the render seam - composes over the construction behaviour's list instead of replacing it |
| `BedSlot` / `BedSlotSide` / `BedSlotState` | `:40` / `:8` / `:21` | the slot model |
| `BlockSandCastingBed` | `BlockSandCastingBed.cs:25` | `BlockFilledMegastructure`, `IFillerHost`, `IFillerInteractionTarget`, `IExBlockDefProvider` |
| `BlockSandCastingBed.SlotCells` | `:151` | world position → slot, by zipping the resolved footprint against the specs it was generated from |
| `BlockSandCastingBed.FootprintPositions` | `:178` | the cell list the BE's flow and surface loops walk |
| `BlockEntitySandCastingBed` | `BlockEntitySandCastingBed.cs:40` | plain `BlockEntity`; hosts the basin as a behaviour |
| `…OnServerTick` | `:227` | pull → flow → cool, the whole model |
| `…FlowEdge` (static) | `:291` | the bed's private edge rule; drain fittings never give back (`:302`) |
| `…PullFromNeighbours` | `:267` | the intake; the isolation trick is at `:275` |
| `…OnCellInteract` / `TryCarveAt` / `TryHarvest` | `:330` / `:343` / `:371` | the player-facing verbs |
| `…Denominate` (static) | `:416` | pure, unit-tested mass conservation |
| `…YieldsCasting` (static) | `:436` | mold ⇒ casting, anything else ⇒ scrap |
| `…ApplyCapacity` | `:151` | re-sizes a slot's cell to the cavity actually cut into it |
| Tests | `test/…/Casting/SandBedLayoutTests.cs` (428 lines), `test/…/Blocks/Casting/SandCastingBedTests.cs` (167 lines) | layout + element existence; denomination + flow edge |

**Where a caller hooks in.** Nothing outside iiex consumes the bed. To feed it, end a
[molten canal](molten-canal.md) run on any horizontal neighbour of the principal cell - the intake reads
adjacent cells of the principal only (`:272-286`), so a canal touching a filler delivers nothing.

---

## Gotchas

1. **The runner/mold test is `cellPos.X == Pos.X`, which only holds facing north or south.**
   Used both to pick a cell's surface cuboid (`:517`) and to count "molds" in the HUD. At
   `StructureAngle` 90 or 270 (a west- or east-facing bed) the footprint's `dx`/`dz` swap, so the spine runs
   along X and the test inverts: runners get the mold surface and the HUD counts the spine instead of the
   flanks.

2. **`ready` in the HUD is `CellAmount / PigUnits` on every non-spine cell** - a slag-filled
   mold is reported as pigs, and an uncarved flank holding stranded metal is counted too.

3. **The class summary still says "the two side columns the double-molds"**
   (`BlockSandCastingBed.cs:19`). It predates the 4-row rework: middle rows hold three.

4. **The block entity's summary says "the four central runners → the eight side molds"**
   (`BlockEntitySandCastingBed.cs:28`). There are three runner fillers; the fourth centre cell is the
   basin. Minor, but it is what sends readers looking for a runner that is not there.

5. **Slot order is a save format.** `_slots` is persisted as a positional byte array;
   reordering `SandBedLayout.Slots` silently rewrites every saved bed. A test pins the first three and the
   last entry (`SandBedLayoutTests.cs:44-49`).

6. **The `…Full` element suffix means *uncarved*, not *poured*** (`SandBedLayout.cs:194-203`). "Full" reads
   as "full of sand".

7. **`_serverTick` / `_clientTick` are stored and never unregistered**; disposal
   relies on the block entity going away.

---

## Open

- **The runtime shape is untracked.** `assets/iiex/shapes/casting/sandcastingbed.json` is untracked in git,
  and its editable source (`assets/editable/shapes/sandcasting-bed.json`) is deleted in the working tree -
  the drawn bed has no committed source of truth (§ Assets).
- **Bed rotation.** Beds are meant to work in a pour / cool / slag rotation, with the bed count derived
  as cooling-and-clearing time ÷ pour time. Nothing in code models a rotation, a clearing time, or a bed
  count; the anchor number exists only as a design target. At the 375 u pig a fully carved bed holds
  20 × 375 = 7500 u - one full cold shaft, which is the arithmetic the anchor was chosen for.
- **Direct charging.** The [cold blast furnace](blast-furnace-cold.md) is meant to be able to charge a
  converter with hot metal instead of pouring beds. No direct-charge path exists in code; the only
  furnace-side exit is the tap into a canal start.
- **Slag bed as a cheaper build.** Whether a slag-only bed gets its own cheaper recipe (a slag run does not
  need the brickwork) is undecided.
- **Spill on break.** A bed broken with metal in it voids the charge silently. Every other molten holder in
  the mod either drops a recovery or plays a spill sound.
