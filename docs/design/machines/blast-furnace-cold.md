# Cold blast furnace

**Status** live   **Mod** iiex

**Owns** - the facts this page is canonical for:

* the cold furnace's structure: the 160-cell layout, its origin and orientation source, the 39-cell
  shaft, and the cell counts per glyph;
* the charge-column model as every shaft furnace runs it: per-column geometry capacity, bands, the
  item scale, world piles, and the charging rules;
* the product identity: molten pig iron out of the lower tap, slag out of the upper, and the
  `iiex:hearthmetal-pigiron` block the pool freezes into;
* the yield and pool keys - `BfIronPerOreUnit`, `BfSlagPerOreUnit`, `BfMaxMoltenIron`,
  `BfMaxMoltenSlag` - and the tap drain keys `TapDrainPerTick` / `TapIronStackFactor` /
  `TapSlagStackFactor`;
* the iiex part blocks this machine introduces - `iiex:furnace-tuyere`, `iiex:furnace-irontap` /
  `iiex:furnace-slagtap`, `iiex:hopper-tall`. The smex hot furnace and the [cupola](cupola.md) reuse
  these blocks; they do not redefine them. The hopper's tank and drip are
  [tall-hopper](tall-hopper.md)'s; the pile block is [charge-pile](charge-pile.md)'s;
* the construction cost of the whole furnace.

**Does not own** - cited only, never restated:
[heat balance](../mechanics/heat-balance.md) (the `T_process` law, the raceway rate model and every
carbon/gas constant, the derived state, blast demand) ·
[molten network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/molten-network.md) (canal start, flow, per-cell capacity, back-pressure) ·
[pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md) (the blast main the tuyeres draw from, pressure, burst) ·
[multiblock](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/multiblock.md) (the layout DSL, origin-is-negation, fillers, the build
outline) · [recipes & config](../mechanics/recipes-config.md) (code-first defs, goldens, recipe costs) ·
[burden](../items/burden.md) (the ore + flux item and its flux bands) · [fuels](../items/fuels.md)
(coke and charcoal, carbon values) · [ironmaking](../processes/ironmaking.md) (the process chain,
recovery ladder, burn-out payout) · [burdenmaker](burdenmaker.md) ·
[twin-tub-blower](twin-tub-blower.md) (the MP air source) · [cowper](cowper.md) (the preheat this
furnace has none of) · [cupola](cupola.md) ·
[density rule](../mechanics/density-rule.md)

---

## Role

The iron tier's primary smelter, and the only machine in the suite that reduces ore. Everything else
that handles iron either re-melts metal that is already metal ([cupola](cupola.md)), decarburises it
([puddling furnace](puddling-furnace.md)) or refines it downstream (smex).

Charged in alternating courses of fuel and [burden](../items/burden.md), it hands liquid metal straight
into a [molten canal](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/molten-network.md); no ingot becomes an inventory item on the way
out. It costs a 160-cell structure, a blast main kept pressurised, and the fuel : burden ratio of every
course laid.

Cold and hot are the same machine. `BlockEntityBlastFurnaceCold` is an empty subclass of
`BlockEntityShaftFurnace`. "Hot blast" is the preheat term in the
[heat balance](../mechanics/heat-balance.md), and "cold blast" is that term being zero because nothing
at the iron tier heats the blast.

### Its second act: ferroalloys (designed, not built)

The cold furnace is never obsoleted: its steel-era job is the ferroalloy family, run as different
charge through the same block, a data override like the [cupola](cupola.md). The trade is fuel against
throughput - the cold furnace only just reaches a ferroalloy's higher process temperature on a very
carbon-rich charge, where the hot furnace does it cheaply but spends time it owes the steel tier.
Nothing of this exists in `src/`.

---

## Structure

Anchor: `iiex:furnace-blastcore-{tier}-{side}` at the bottom centre of the furnace, in the hearth floor
directly under the shaft. The layout is authored in the anchor's own north frame with `Origin(-3, -2)`,
the negation of the `C` glyph's (col, row) so the core lands on its own (0, 0, 0) - see
[multiblock](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/multiblock.md).

Source of truth: the nine ASCII cross-sections in `BlockBlastFurnaceCoreCold.cs` (y = 0 hearth floor →
y = 8 open stack; the open top is the cold furnace's chimney - it takes no exhaust outlets). Golden:
`mods/iiex/tests/goldens/iiex/blocktypes/furnace/blastcore.json`.

### Cell census — 160 offsets

| Glyph | Required block | Count |
|---|---|---|
| `#` | refractory bricks, any single tier | 111 |
| `C` | `iiex:furnace-blastcore-*` (the anchor) | 1 |
| `I` | `iiex:furnace-irontap`, facing west | 1 |
| `S` | `iiex:furnace-slagtap`, facing east | 1 |
| `Y` / `T` | `iiex:furnace-tuyere`, orientation n / s | 2 |
| `H` | `iiex:hopper-tall`, facing east | 1 |
| `f` | `exlib:structurefiller` (the hopper's own top cell) | 1 |
| `c` | the shaft - `*:@(air\|coalpile\|furnace-chargepile\|hearthmetal-.*)` | 36 |
| `h` | the crucible floor - same alternation, own glyph for its second role | 3 |
| `a` | `game:air` - the stack throat over the hopper | 3 |

The taps and tuyeres are orientation-pinned in the legend: a tap or tuyere installed the wrong way
round does not complete the structure. Both taps face into the furnace and pour outward -
`BlockEntityFurnaceTap.TryPourMetal` pours to `Pos + facing.Opposite`, one down - so the iron tap in
the east wall is declared west and pours to (3, 0, 0), the slag tap in the west wall is declared east
and pours to (−3, 0, 0). That runout cell is left unclaimed by the drawing; claiming it would leave the
slag tap nowhere to pour, silently.

### Functional cells — read off the drawing

The roles below are marked on the layout glyphs and read back through `CellsWithRole`; none are
declared as C# offset literals. The one hand-declared cell is `ShaftCentre`.

| Role | Cells | Glyph |
|---|---|---|
| `Chargeable` | 39 - the 3-cell crucible floor at y = 1 plus 3×3 at y = 2…5 | `c` + `h` |
| `Pool` | the 3 crucible cells `(−1…1, 1, 0)` | `h` |
| `Tuyere` | `(0, 2, −2)`, `(0, 2, 2)` - the wall face at the top of the hearth | `Y`, `T` |
| `MetalTap` | `(2, 1, 0)` | `I` |
| `SlagTap` | `(−2, 1, 0)` | `S` |
| `GasOutlet` | empty - the drawing carries no outlet glyph | - |

The crucible cells carry `Chargeable` and `Pool`: charge rests on them while the furnace runs, and the
molten pool freezes onto them when it goes out. The settled design makes them pool-only (a 39 → 36 cell
shaft) once the live crucible lands - see Open.

### Orientation

`UpdateStructureRotation` reads the core's plain `side` variant - stamped by the vanilla
`HorizontalOrientable` behaviour at placement - through `ExOrientation.AngleFromSide`. There is no
stored yaw and no offset: the layout grids are the core's north frame. `FurnaceOrientationMatrixTests`
covers the other three facings.

---

## The charge columns

The shaft is 9 columns, not 39 block entities. Each column `(x, z)` - keyed structure-local on the
core, so the model is rotation-correct by construction - owns an ordered list of segments, raceway end
first:

```
column (x,z) = [ {coke, 3 u, 1180 °C}, {burden, 9 u, 1140 °C}, {coke, 3 u, 980 °C}, … ]
                 ↑ raceway end                                    ↑ the hopper adds here
```

* A band is 2 items (`ChargeItemsPerBand`), 16 bands to a block, so a block holds 32 items and the
  full 39-cell shaft holds 1 248 units. Capacity is geometry: each column holds what its own height
  allows (`ChargeColumn.BlocksTall`, counted from that column's own floor), and there is no per-cell
  cap anywhere. `ChargeCellsOf(x, z)` is the single source of "which cell is the n-th block of this
  column" - an ordered, bottom-up list that placement fills from 0 and `ChargeColumnAt` reads indices
  out of, so the two cannot drift.
* Each column has its own height and its own floor. The crucible row's three columns start a block
  lower than the other six. A furnace charged unevenly stands unevenly, and descent eats each column at
  its own rate.
* The world blocks are windows. `SyncChargeBlocks` reconciles the world to the columns after every
  change - charging, descent, burn-out, a break - placing and removing `iiex:furnace-chargepile`
  blocks as a column's height crosses block boundaries. A cell holding anything but air or the
  furnace's own pile is skipped and heals when it frees; the units are never dropped. The pile block
  itself - bands, glow, the top-band take, and the break that splices a window out mid-column - is
  [charge-pile](charge-pile.md)'s.
* A course is a span, not a cell. Fuel laid first, burden on top, both free-sized; block boundaries
  quantise nothing, and a course renders as one continuous stripe across block seams. A band stores one
  material code, so a course is coke or charcoal, never a blend; the two are priced apart by carbon
  ([fuels](../items/fuels.md)) and drawn apart on the wall.
* Segments carry their own temperature, inherited by both halves when a segment splits on partial
  consumption, which is what makes the melt condition exact at unit granularity.

### Charging

The [tall hopper](tall-hopper.md) is a buffer, not a dispenser: loaded with as much fuel as the course
should carry, it drips down (`HopperTallDropPerSecond`) and lays fuel onto the columns; loaded with
burden, it lays burden on top. Two loads per course, and the ratio between them is the coke dial. Each
drip asks the core where to lay (`NextChargeColumn` - lowest column first), so one hopper reaches every
column at any facing and the stockline self-levels.

One rule governs where a load goes:

> **Fuel may be added to a column only above the last burden - never beneath it.**
> Each load fills the lowest columns first.

The rule tests the fuel role, not material equality - coke onto coke and charcoal onto coke are refused
alike, or a second fuel could lay a fuel course straight onto a fuel course and the furnace could never
make iron from it. Switching material starts a new course by itself; excess continues into the next
course; a full shaft makes the hopper hold its tank. A short course stays visibly short.

The hopper delegates what is chargeable to the core's `IsChargeItem`, so one hopper block serves every
furnace and each admits its own fuel and charge. Its HUD names the course being laid (the fuel's own
item name, or "mixed fuel"), reports the charge's carbon %, and shows the shaft fill.

Hand-charging through the pile blocks is take-only today ([charge-pile](charge-pile.md) Open 1).

---

## Assets

The furnace has no shape of its own: it is vanilla refractory brick plus the small iiex part blocks.
The core is a vanilla cube with per-face refractory textures, an orientation marker on the north face
and a "BF/C" type label on the south, so the three furnace anchors read apart at a glance. Each tap draws
its own shape (`iiex:furnace/irontap` / `iiex:furnace/slagtap`), and the two encode their notch heights -
the iron channel at model Y 2-3, the cinder channel at Y 10-11 - so both taps sit at layout y=1 and the
course between them is art, not geometry. Neither shape carries an animation: the closed state is the
`ClayPlug` element, drawn or pruned. A lit furnace is signalled by sounds and by the charge piles' own
glow; there is no looping furnace animation.

Player-facing help: `mods/iiex/assets/iiex/config/handbook/02-coldblastfurnace.json` ↔
`mods/iiex/docs/handbook/02-coldblastfurnace.html`.

---

## Construction

No RCC, no crafting station: the core and fittings are grid recipes and the rest of the furnace is
hand-laid brick. All recipes live in `mods/iiex/src/Recipes/Grid/FurnaceRecipeDefinitions.cs`;
the golden is `mods/iiex/tests/goldens/iiex/recipes/grid/blastfurnace.json`.

| Output | Ingredients |
|---|---|
| `iiex:furnace-blastcore-{tier}-n` | 4 × fired refractory brick (tier captured), 2 × rod, 4 × nails, 2 × plate |
| `iiex:furnace-irontap-s` | 2 × tier-3 refractory brick, 12 × fire clay, 1 × plate, hammer, chisel |
| `iiex:furnace-slagtap-s` | 2 × tier-3 refractory brick, 12 × fire clay, hammer, chisel |
| `iiex:furnace-tuyere-s` | 2 × tier-3 refractory brick, 1 × `iiex:pipe-plated-straight*` (`FurnaceRecipeDefinitions.cs:66`), hammer, chisel |
| `iiex:hopper-tall-n` | 4 × plate, 3 × nails, hammer |
| `iiex:furnace-twintubblower-n` | 2 × leather, planks, nails, hammer - the iron tier's only air source |

Plus the raw structure: 111 refractory bricks of any single tier and one `exlib:structurefiller`,
which the tall hopper places itself. The cores take any refractory tier and inherit it; the taps and
tuyere are pinned to tier 3.

---

## Operation

### Inputs → outputs

| In | Out |
|---|---|
| fuel courses (coke or charcoal) and [burden](../items/burden.md) courses, dripped by the hopper | molten pig iron down the lower tap into a [canal start](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/molten-network.md) |
| pressurised air at both tuyeres, off the [twin-tub blower](twin-tub-blower.md) through the [pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md) | molten slag down the upper tap |
| | on extinguish: the pool frozen as `iiex:hearthmetal-pigiron` on the hearth floor, plus burnt-out salvage in the columns |

### The player's verbs

| Gesture | Where | Effect |
|---|---|---|
| place core | ground | stamps the `side` variant, starts the multiblock |
| Ctrl+Shift+RMB | core, tap, tuyere, hopper or pile | toggles the build outline while incomplete |
| RMB with charge | the hopper's top filler cell | deposits 1 unit; Ctrl+RMB deposits the whole stack |
| RMB empty-handed | same filler cell | withdraws the tank |
| RMB empty-handed | a charge pile | takes one band off that column's top |
| break a charge pile | the shaft | splices that window's units out and drops them - [charge-pile](charge-pile.md) |
| RMB empty-handed | a plugged tap | breaks the clay plug out and opens it; the plug is destroyed |
| RMB with a lit flame | an open tap | blows the furnace in - it stays dark until this happens |
| RMB with fire clay | an open tap | stops it again, consuming `TapPlugClayCost` |
| break any `#` | anywhere | structure lost - the furnace keeps burning but can never re-ignite (see below) |

There is no door and no on/off switch. Clearing a dead furnace means digging its charge back out
through the piles or breaking its walls.

### Ignition, running, going out

State is derived every tick, not stored; the model is the shaft branch of the
[heat balance](../mechanics/heat-balance.md). The behaviour this furnace shows:

* Ignition is a flame plus a position, with no quantity threshold. A player has to blow the furnace in -
  a lit flame through an open tap-hole, which sets `BlownIn` - and the drawing decides whether the charge
  takes: carbon standing at every column's raceway (the complete bottom course, the furnace's own column
  count, no constant) and air at pressure through the tuyeres. A shaft piled high in one column never
  lights, however much is in it, and neither does one nobody has torched. `BlownIn` clears when the
  furnace goes out, so a recharged shaft is lit again from scratch.
* Combustion happens at the raceway only, in front of the two tuyeres, and everything the furnace does
  per second follows from the carbon burned there: flame temperature, rising gas, descent, product.
  Coke burns whenever the furnace is lit - a furnace melting nothing is still burning its charge away -
  and a campaign ends when the raceway runs out of carbon. There is no fuel clock.
* The chill. Under-fuelled burden arrives at the raceway cold, cannot melt, and stops its column: the
  column hangs (`IsHung` / `HungColumnCount`) while well-fuelled neighbours keep descending. A hung
  column stops bringing fresh carbon down, so a fully chilled furnace goes out within minutes.
  Recovery is breaking the bottom piles out - [charge-pile](charge-pile.md).
* Breach and choke are opposites. Breaking a wall opens the shaft: the fire keeps burning at natural
  draught (no product - too cold to smelt) and the furnace can never re-ignite while incomplete.
  Blocking the air chokes it: the fire smothers and it reads Idle. Neither stores a flag; the condition
  that ended the campaign is still standing.
* Burn-out is irreversible without a stored bit: salvage retention at the raceway is zero
  (`BfBurnoutFuelRetainedBottom` = 0), so a dead furnace has no carbon at its own raceway and cannot
  relight off its own residue.

### Products, pools and taps

Melting renders burden at the raceway into two pools held on the furnace
(`BlockEntityShaftFurnace._moltenIron` / `_moltenSlag`):

* Yield is per unit of ore content: a melted burden unit renders `BfIronPerOreUnit` × the band's own
  stamped ore share (unstamped charge falls back to 0.75). Slag likewise at `BfSlagPerOreUnit`. The
  recovery ladder this implements - 8.5 u/nugget raw against the bloomery's 5 - is
  [ironmaking](../processes/ironmaking.md)'s and is pinned by `OreRecoveryGuardRailTests`.
* The crucible holds `HearthUnitsPerBand` per pool cell, per metal - the cells' own capacity, not a
  furnace-level cap. Product rendered over a full crucible is lost, not queued, so an untapped furnace
  wastes its campaign.
* An open tap drains its pool every tick - up to `TapDrainPerTick` units considered, stack size
  `ceil(units × factor)`, so at shipped values the iron tap passes up to 30 u/s and the slag tap
  40 u/s. Pour target is `Pos + facing.Opposite`, one down.
* Extinguishing freezes nothing: the metal was written into `iiex:hearthmetal-pigiron` as it was made
  and its cells latch solid on their own thermal update. What extinguish does is burn the columns out to
  salvage - fuel retention interpolates from 0 at the hearth to 0.4 at the stockline, so what the player
  digs back out is richer in coke toward the top.

---

## Numbers

`IiexValues.X` is a generated accessor over `IiexConfig.X`; the file:line is the config declaration.

### Owned — `mods/iiex/src/IiexConfig.cs`

| Key | Value | file:line | What it does |
|---|---|---|---|
| `BfIronPerOreUnit` | 8.5 u | IiexConfig.cs:470 | Molten pig per unit of ore content melted |
| `BfSlagPerOreUnit` | 8.5 ÷ 6 u | IiexConfig.cs:478 | Molten slag per unit of ore content - the 6 : 1 iron-to-slag ratio stated directly |
| `BfMaxMoltenIron` | 2400 u | IiexConfig.cs:555 | Pig pool ceiling; reaching it stalls production |
| `BfMaxMoltenSlag` | 600 u | IiexConfig.cs:558 | Slag pool ceiling, same |
| `TapDrainPerTick` | 50 u | IiexConfig.cs:587 | Units of pool considered per drain tick |
| `TapIronStackFactor` | 0.6 | IiexConfig.cs:591 | Iron stack = `ceil(units × 0.6)` - up to 30 u/s |
| `TapSlagStackFactor` | 0.8 | IiexConfig.cs:595 | Slag stack = `ceil(units × 0.8)` - up to 40 u/s |
| `HopperTallCapacity` | 128 u | IiexConfig.cs:719 | Tall-hopper tank |
| `HopperTallDropPerSecond` | 8 u/s | IiexConfig.cs:723 | Drip rate into the shaft |
| `ChargeItemsPerBand` | 2 | IiexConfig.cs:759 | The band quantum - 32 items a block; capacity is cells × this × 16 |

No per-cell pile cap and no melt interval: capacity is geometry, the cadence is the descent, throttled
by carbon burned at the raceway ([heat balance](../mechanics/heat-balance.md)).

### Cited — owned elsewhere, values not repeated here

| Key | Owner |
|---|---|
| `BfIronMeltingPoint`, the raceway rate keys (`BfRacewayCarbonPerTuyerePerSecond`, `BfBurdenPerCarbonUnit`, `BfRacewayGasPerCokeUnit`, `BfShaftGasTransferFrac`, `BfFuelCarbonReference`), the heat-in/heat-out terms, `BfMeltMargin*` / `BfMeltSpeed*`, blast demand (`BfBlastPressure*`, `BfTuyereDraw*`, `TuyereIntakeVolume`), `BfStarvationSupplyFrac`, `MaxAwayCatchupSteps` | [heat balance](../mechanics/heat-balance.md) |
| burden composition, the three flux bands, stack size | [burden](../items/burden.md) |
| fuel carbon values (coke 2, charcoal 1) | [fuels](../items/fuels.md) |
| `BfBurnoutFuelRetainedBottom` / `Top`, the recovery ladder | [ironmaking](../processes/ironmaking.md) |
| `MoltenFlowRate`, canal/start capacities, cooldown | [molten network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/molten-network.md) |
| pipe burst pressure, litres, leak | [pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md) |
| `TwinTubBlowerOutputPerSecond`, `TwinTubBlowerMaxPressure` | [twin-tub-blower](twin-tub-blower.md) |
| `CupolaChargeMetalUnitsPerBlock`, cupola melt point and unit scale | [cupola](cupola.md) |

---

## Drops

| Broken block | Returns |
|---|---|
| core | itself, the same `{tier}-{side}` variant |
| iron / slag tap | itself, normalised to the definition's own default facing |
| tuyere | the fallback-orientation variant |
| tall hopper | itself plus the whole stack still in the tank |
| a charge pile | that window's charge, spliced out of the column - [charge-pile](charge-pile.md) |
| `iiex:hearthmetal-pigiron` (the frozen pool) | metal bits × the stamped count |
| refractory brick | vanilla |

---

## Code

| Type / member | Where | Notes |
|---|---|---|
| `BlockBlastFurnaceCoreCold` | `…/Furnaces/Blocks/BlockBlastFurnaceCoreCold.cs` | code-first def and the nine-layer layout; no behaviour of its own |
| `BlockEntityBlastFurnaceCold` | `…/Furnaces/BlockEntities/BlockEntityBlastFurnaceCold.cs` | empty subclass - everything is `BlockEntityShaftFurnace`'s |
| `BlockEntityShaftFurnace` (abstract) | `…/BlockEntities/BlockEntityShaftFurnace.cs` | the machine: raceway, columns, melt, pools, drains, burn-out. The [cupola](cupola.md) and the smex hot furnace are its other leaves |
| ↳ product virtuals | `MetalProductCode`, `ProductPerUnit`, `SlagPerUnit`, `MaxMoltenProduct`, `MaxMoltenSlagPool`, `SolidProductBlock` | the cupola's whole override surface |
| ↳ `ConsumeForMelting` / `MeltBurden` | round-robin over the columns; burden only - coke leaves a column by burning and by nothing else | |
| ↳ `DrainIronTap` / `DrainSlagTap` | the tap path and the drain keys above | |
| ↳ `BurnOutCharge` | height-interpolated fuel retention, per block | |
| `BlockEntityFurnaceCore` (abstract) | `…/Furnaces/BlockEntityFurnaceCore.cs` | the fired core: columns, `SyncChargeBlocks`, `NextChargeColumn`, roles, heat - see [heat balance](../mechanics/heat-balance.md) |
| `BlockFurnaceTap` / `BlockEntityFurnaceTap` | `…/Blocks/BlockFurnaceTap.cs` / `…/BlockEntities/BlockEntityFurnaceTap.cs` | both tap types; `TryPourMetal` pours to `facing.Opposite`, one down |
| `BlockTuyere` / `BlockEntityTuyere` | `…/Blocks/BlockTuyere.cs` | a single-faced pipe node + build-outline forwarding |
| `BlockHopperTall` / `BlockEntityHopperTall` | `…/Blocks/BlockHopperTall.cs` | see [tall-hopper](tall-hopper.md) |
| `BlockChargePile` / `BlockEntityChargePile` | `…/Blocks/BlockChargePile.cs` | see [charge-pile](charge-pile.md) |
| `BlockHearthMetal` / `BlockEntityHearthMetal` | `…/Products/` | the frozen pool, metal in the code (`hearthmetal-{pigiron\|castiron}`) |

### Where a caller hooks in

* A new product on this machine (ferroalloys, the cupola) overrides the product virtuals on
  `BlockEntityShaftFurnace` and nothing else.
* A new blast source implements `IPipeNode` and is joined to the tuyere cells; the furnace only calls
  `TryConsume` and reads `Medium` / `Pressure` / `Temperature`.
* A new destination for the tap implements the canal-start contract
  ([molten network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/molten-network.md)); `TryPourMetal` returns accepted and the pool is
  decremented by exactly that, so a partial accept is safe.

### Tests

| File | Covers |
|---|---|
| `mods/iiex/tests/Blocks/Furnaces/FurnaceGeometryTests.cs` | every offset resolves to the right glyph; no exhaust outlets |
| `…/FurnaceOrientationMatrixTests.cs` | the same geometry in all four facings |
| `…/ShaftColumnsTests.cs` | structure-local column keying, the asymmetric-drawing rotation case, per-column floors |
| `…/ChargeColumnTests.cs` / `ChargePileTests.cs` / `ChargeMaterialisationTests.cs` | the column data model, the pile window, the world sync |
| `…/HopperTallTests.cs` | deposit/withdraw, drip, mismatched-deposit refusal, save round-trip |
| `…/BlastFurnaceTapTests.cs` | the pour handoff and its targets in all four facings |
| `…/FurnaceTapPlugTests.cs` | the clay plug: both verbs, their cost, the legacy save key, the drawn elements |
| `…/FurnaceBlowInTests.cs` | the blow-in: the gate, the gesture, once per campaign, the legacy save key |
| `…/OreRecoveryGuardRailTests.cs` | the 8.5 u/nugget yield against the bloomery floor |
| `…/HeatBalanceTests.cs` | the heat model ([heat balance](../mechanics/heat-balance.md)) |
| `mods/iiex/tests/Scenarios/ColdBlastFurnaceScenarioTests.cs` | the charge → light → melt → tap → extinguish walk on this furnace |

---

## Gotchas

1. The tap's `side` variant is the direction it faces into the furnace, and the pour goes out the
   opposite face, one down. The layout legend pins both taps and both tuyeres, so a furnace built to
   the outline cannot get this wrong; a hand-substituted tap can, and then completes nothing.

2. The two taps share one sound throttle (`_lastTapSoundMs` on the shaft furnace), so a furnace
   pouring metal and slag together plays half the pour hisses it should.

3. A full crucible loses product silently. The melt keeps consuming burden while the cells sit at
   capacity, so everything rendered over it evaporates. The disruption machinery that would stall a
   firebox furnace on `LiquidCapacityReached` does not run on the shaft branch - keep the taps open.

---

## Open

1. ~~The live crucible.~~ **Built** (2026-08-20/21). The pool stands in `iiex:hearthmetal-*` blocks the
   furnace places into its `Pool` cells as it melts, each carrying an iron cell and a slag cell at
   `HearthUnitsPerBand` apiece; the float pair and the two pool-cap keys are gone, the crucible glyph
   carries `Pool` alone (39 → 36 chargeable cells), and the drawn per-type tap shapes are adopted.

2. ~~Blow-in ritual.~~ **Built** (2026-08-21). A tap arrives plugged; an empty hand breaks the plug out
   and destroys it, a lit flame through the open hole sets `BlownIn`, and `TapPlugClayCost` fire clay
   stops it again - break, torch, re-plug, blast on, one plug a campaign. The positional gate is
   unchanged and still decides whether the charge catches. A lit front climbing the shaft pile-to-pile
   is still design only.

3. Direct charging to a converter is designed, not built. A ready converter is just a different canal
   destination - see [direct-charging](../processes/direct-charging.md). Nothing in `src/`
   distinguishes a converter destination from any other.

4. Ferroalloys - the whole second act - is unbuilt. No third charge family, no ferroalloy metal
   descriptor, no recipes. The block needs no change; the data does.

5. Hot blast is 4×4 in design only. `BlockEntityBlastFurnaceHot` inherits the cold furnace's 9-column
   geometry today; the enlarged drawing is [blast-furnace-hot](blast-furnace-hot.md)'s.
