# Hot blast furnace

**Status** live   **Mod** smex

**Owns** - the facts this page is canonical for:

* the existence and identity of `siex:blastfurnacecore` as a distinct registered blocktype - its
  tier-3-only refractory faces, its `BF/H` label, and the fact that it carries no `tier` variant group;
* the hot furnace's shipped 147-cell layout: the census per glyph, the per-layer cell counts, and every way
  it differs from the cold furnace's;
* the two gas-outlet cells and the air throat between them - the only structural difference that makes this
  furnace "hot" - and the exhaust budget they emit;
* the charging pair this furnace introduces: `siex:hopperreinforced` (the charge tank) and
  `siex:hopperbell` (magazine + drip), including their capacities and how the drip reaches the shaft;
* the construction of all three smex blocks, and the fact that the core is absent from the recipe-cost
  catalogue;
* the enlarged draft layout in [layouts.md](../../../workbench/layouts.md) § 1 - recorded as deferred.

**Does not own** - cited only, never restated:
[heat balance](../mechanics/heat-balance.md) (the `T_process` law, the preheat term, the raceway rate model,
exhaust volume/temperature constants, and every `Bf*` key) ·
[cold blast furnace](blast-furnace-cold.md) (the shaft machinery this furnace inherits - the charge-column
model, the taps, tuyeres, yields, pools and drains; the iiex part blocks `iiex:furnace-tuyere`,
`iiex:furnace-irontap` / `-slagtap` are defined there) · [charge-pile](charge-pile.md) (the pile block the
columns draw) · [burdenmaker](burdenmaker.md) (where burden is made) · [burden](../items/burden.md) (the
ore + flux item and its flux stamp) · [fuels](../items/fuels.md) (coke and charcoal carbon values) ·
[cowper](cowper.md) (the preheat source) · [smokestack](smokestack.md) (the exhaust sink) ·
[pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md) · [molten network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/molten-network.md) ·
[multiblock](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/multiblock.md) · [recipes & config](../mechanics/recipes-config.md) ·
[ironmaking](../processes/ironmaking.md) · [cupola](cupola.md) · [density rule](../mechanics/density-rule.md)

---

## Role

The steel tier's ironmaking anchor. It is the same machine as the
[cold blast furnace](blast-furnace-cold.md) - `BlockEntityBlastFurnaceHot` is an empty subclass of
`BlockEntityShaftFurnace` - fitted with a closed top with exhaust outlets, so the furnace's own waste heat
can be recovered by a [cowper](cowper.md) and blown back in as preheated air.

"Hot blast" is the preheat term in [heat balance](../mechanics/heat-balance.md)'s `T_in`, and the only
reason this furnace can make it non-zero is that its layout marks two `iiex:pipe-outlet` cells
`CellRole.GasOutlet` - cells the cold furnace's drawing does not have. Run the same structure on unheated
air and it behaves as a cold furnace. There is no flag, no branch and no second copy of the model.

### What the player buys with it

| | Cold | Hot |
|---|---|---|
| Refractory tier | any (`tier*`) | tier 3 only |
| Charging | one `iiex:hopper-tall` | reinforced hopper + bell hopper over a sealed top |
| Top | open stack | sealed: bell hopper over a 1-cell air throat |
| Exhaust | none - the open top is the chimney | 2 outlets, 48 L/s, feeding cowpers + a smokestack |
| Blast | ambient air off the [twin-tub blower](twin-tub-blower.md) | preheated air off a charged [cowper](cowper.md) |
| Structure | 160 cells | 147 cells |

The cell count is smaller and the machine is strictly better. The tier gate is not the build: the two
outlets are useless without a gas network, cowpers and a stack, and the blast pressure comes from a steam
engine's air blower rather than a hand-fed blower.

---

## Structure

Anchor: `siex:blastfurnacecore-{side}` at the bottom centre of the furnace, in the hearth floor directly
under the shaft. The hot core passes no brick tiers to the shared `BlockFurnaceCoreBase.Core(...)`
fragment, so unlike `iiex:furnace-blastcore-{tier}-{side}` it is a single, tier-less block.

Layout authored in the anchor's own north frame with `Origin(-3, -2)` - the negation of the `C` glyph's
(col, row), per [multiblock](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/multiblock.md).

| Source | Where |
|---|---|
| Definition (nine ASCII cross-sections, y = 0 → y = 8) | `BlockBlastFurnaceCoreHot.cs` |
| Golden (the arbiter) | `mods/siex/tests/goldens/siex/blocktypes/blastfurnace/core.json` |
| Round-tripped copy for editing | [layouts.md](../../../workbench/layouts.md) § Section 2, "Hot blast furnace" |

### Cell census — 147 offsets

| Glyph | Required block | Count |
|---|---|---|
| `#` | `game:refractorybricks-good-tier3` - exact, no wildcard | 99 |
| `C` | `siex:blastfurnacecore-*` (the anchor) | 1 |
| `T` | `iiex:furnace-irontap`, facing west - the east wall, pours out to (3, 0, 0) | 1 |
| `S` | `iiex:furnace-slagtap`, facing east - the west wall, one course higher, pours to (−3, 1, 0) | 1 |
| `Y` / `y` | `iiex:furnace-tuyere`, orientation n / s | 2 |
| `P` | `iiex:pipe-outlet*` - the hot furnace's own addition | 2 |
| `R` | `siex:hopperreinforced` | 1 |
| `B` | `siex:hopperbell` | 1 |
| `c` | the shaft - the same air / pile / hearth-metal alternation as the cold furnace's | 36 |
| `p` | the crucible floor - same alternation plus `iiex:hearthmetal-*`, marked `Chargeable` and `Pool` | 2 |
| `a` | `game:air` - the throat under the bell | 1 |

Per layer: y0 20 · y1 16 · y2 23 · y3 23 · y4 21 · y5 21 · y6 9 · y7 9 · y8 5.

The taps and tuyeres are orientation-pinned in the legend, exactly as on the cold furnace: a tap or tuyere
installed the wrong way round does not complete the structure. Each tap's runout cell is left unclaimed by
the drawing; claiming it back would leave that tap nowhere to pour, silently. The slag tap's notch does not
mirror the cold furnace's - this drawing keeps the narrow two-cell crucible and the high slag tap. Whether
it should follow the cold furnace's wider hearth is a design question for the smex remake, not something to
fix by copying cells across.

### Functional cells — read off the drawing

All roles are layout marks read through `CellsWithRole`; the hot subclass declares no cell literals of its
own.

| Role | Cells |
|---|---|
| `Chargeable` | 38 - the 2-cell crucible floor at y = 1 plus 3×3 at y = 2…5 |
| `Pool` | the 2 crucible cells `(0, 1, 0)`, `(1, 1, 0)` |
| `Tuyere` | `(0, 1, −1)`, `(0, 1, 1)` |
| `GasOutlet` | `(0, 6, −1)`, `(0, 6, 1)` - the only furnace of the three that declares any |
| `MetalTap` | `(2, 1, 0)` |
| `SlagTap` | `(−2, 2, 0)` |

### The shaft and the top

The shaft is a 3 × 3 column stack from y = 2 to y = 5 plus the two crucible cells: 38 chargeable cells,
1 216 units at the ore scale (32 items a block; the column model, bands and courses are the
[cold blast furnace](blast-furnace-cold.md)'s). Above it the two furnaces diverge:

| Level | Cold furnace | Hot furnace |
|---|---|---|
| y = 6 | brick ring + `iiex:hopper-tall` + air | `P` `a` `P` - the two gas outlets flanking a one-cell throat |
| y = 7 | brick ring + hopper filler + air | brick ring + `B` bell hopper |
| y = 8 | brick + air (open stack) | brick cross + `R` reinforced hopper |

The sealed bell top is literal: nothing above y = 5 is chargeable, the only opening is the single
`game:air` cell at `(0, 6, 0)`, and the only way in is the bell hopper's drip.

### Orientation

The core's plain `side` variant, stamped by vanilla `HorizontalOrientable` at placement, read through
`ExOrientation.AngleFromSide` - no stored yaw, no offset; the layout grids are the core's north frame.
Covered in all four facings by `FurnaceOrientationMatrixTests`. The two hoppers sit on the vertical centre
line, so rotation never moves them relative to each other, and the bell's neighbour lookups (`Pos.UpCopy()`
for the tank, the anchor scan for the core) are rotation-free by construction.

---

## Assets

The furnace has no shape of its own - it is vanilla tier-3 refractory brick plus five small blocks, two of
which are smex's.

| Asset | Path | State |
|---|---|---|
| core block model | `game:block/basic/cube` | vanilla cube, per-face textured |
| core faces | `game:block/clay/refractory/tier3/front1` on `all`; `siex:block/furnace/n` overlay north, `siex:block/furnace/bfh` overlay south | live - the "BF/H" label is what tells the three furnace anchors apart |
| reinforced hopper shape | `mods/siex/assets/siex/shapes/blastfurnace/hopper-reinforced.json` | live; no animation |
| bell hopper shape | `mods/siex/assets/siex/shapes/blastfurnace/hopper-bell.json` | live; no animation |
| charge contents mesh | `iiex:shapes/ore/burden.json`, tesselated at runtime by the reinforced hopper | live - the only moving part either hopper shows |
| tuyere / tap shapes | iiex - see [cold blast furnace](blast-furnace-cold.md) | |
| outlet shape | `iiex:pipes/outlet` | see [pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md) |

Editable sources: only `workbench/shapes/furnace-block-hopperreinforced.json` exists. The bell hopper
has no editable counterpart and cannot be re-edited from source.

The reinforced hopper's fill is shown by raising the contents mesh between 9/16 and 14/16 of a block in
proportion to `TankCount / Capacity`. The bell hopper is visually static; its drip is signalled by
`ExParticles.FallingDust` + `ExSounds.StoneCrush`.

Player-facing help: `mods/siex/assets/siex/config/handbook/01-blastfurnace.json` ↔
`mods/siex/docs/handbook/01-blastfurnace.html` (lang key `siex:handbook-blastfurnace-text`). It teaches burden
charging: the [burdenmaker](burdenmaker.md), the alternating coke / burden rounds, positional ignition, the
hang, and the tap-and-pool loop.

---

## Construction

No RCC: three grid recipes plus hand-laid brick. All in
`mods/siex/src/Recipes/Grid/HotBlastFurnaceRecipeDefinitions.cs`; golden
`mods/siex/tests/goldens/siex/recipes/grid/hotblastfurnace.json`.

| Output | Pattern | Ingredients |
|---|---|---|
| `siex:blastfurnacecore-n` | `BRP,BN_,BRP` | 4 × `game:refractorybrick-fired-tier3` (fixed, not `{tier}`), 2 × rod, 4 × nails, 2 × plate |
| `siex:hopperreinforced` | `_H_,PSP,SPS` | 4 × plate, 3 × nails, hammer |
| `siex:hopperbell` | `GHG,PSP,SPS` | 4 × plate, 3 × nails, hammer, 4 × gear |

The bell hopper is authored twice, once per gear source - `game:gear-rusty` and `iiex:gear-*` - the
craftable-gear compatibility pattern, not a duplicate.

Plus the raw structure: 99 `game:refractorybricks-good-tier3` (tier 3 exactly - a tier-1 or tier-2 wall
will not complete this furnace), 2 × `iiex:furnace-tuyere`, the iron and slag taps and 2 × `iiex:pipe-outlet`
(all from other mods; their recipes are the [cold blast furnace](blast-furnace-cold.md)'s and
[pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md)'s).

### The core is missing from the recipe-cost catalogue

`SiexRecipeConfig.Defaults()` lists the cowper, smokestack and converter blocks, the air blower and both
hoppers (`hopperbell-grid`, `hopperreinforced-grid`), but no entry for `siex:blastfurnacecore-*`.
`/exmod steel cheap` therefore discounts every smex machine except the hot furnace core. One missing line;
see [recipes & config](../mechanics/recipes-config.md) for the catalogue contract.

---

## Operation

### Inputs → outputs

| In | Out |
|---|---|
| [burden](../items/burden.md) (ore + flux, from the [burdenmaker](burdenmaker.md)) and fuel (coke or charcoal), in alternating courses through the hopper pair | molten pig iron down the iron tap - the yields and pools are the [cold blast furnace](blast-furnace-cold.md)'s |
| preheated, pressurised air at both tuyeres, off a charged [cowper](cowper.md) | molten slag down the slag tap |
| — | exhaust out of the two outlets → cowpers → [smokestack](smokestack.md) |

### The exhaust budget — this furnace's own number

Each outlet is written once per production tick with `ExhaustVolumePerTick` (24 L) at a fixed factor of the
furnace's internal temperature (`BlockEntityFurnaceCore.cs:274`; the constants and cadence are
[heat balance](../mechanics/heat-balance.md)'s). Two outlets is this layout's fact, so the furnace emits
48 L/s total, which matches one [smokestack](smokestack.md) (`SmokestackGasIntakeVolume` 48) or two
[cowpers](cowper.md) (`CowperIntakeVolume` 24 each). One furnace balances either two stoves or one stack,
and a working plant runs one stove charging, one blowing and a stack taking the surplus.

An exhaust push the network refuses sets `IsChoked`, and a choked furnace smothers - the mechanism and
thresholds are [heat balance](../mechanics/heat-balance.md)'s. The hot furnace is the only furnace that can
choke, because it is the only one with outlets.

### Charging — the reinforced hopper + bell pair

Two blocks, one job, and neither mixes anything: the [burdenmaker](burdenmaker.md) makes the burden, and
the flux stamp rides through both hoppers untouched.

Reinforced hopper (`R`, at `(0, 8, 0)`) - a single-`ItemStack` charge tank, 48 units, so one material and
one grade at a time: one load lays one band type. What is chargeable is delegated to the anchored furnace
core (`IsChargeItem`), so the tank takes burden and fuel - it is the hot furnace's only charging cell, and
a burden-only gate would leave the shaft unfuellable. A mismatched grade is refused with the in-game error
`smex-hopper-wronggrade`. The tank is sized for skip-hoist feeding, and the skip hoist does not exist.

| Gesture | Effect |
|---|---|
| RMB with charge | deposit 1 unit |
| Ctrl+RMB with charge | deposit the whole held stack, capped by room |
| RMB empty-handed | withdraw the whole tank |
| Ctrl+RMB empty-handed or holding anything the furnace does not charge | toggle the bell's dropping |

Bell hopper (`B`, at `(0, 7, 0)`) - a 48-unit magazine plus a drip, on its own 1 s server tick. Each tick
it pulls up to its remaining room from the tank directly above (the single-grade rule enforced again - a
different grade waits until the magazine drains), then drops `HopperDropAmount` units onto the column the
furnace nominates. The selection rule is the core's, not this block's: `NextChargeColumn` - lowest column
first, fuel only above the last burden - so the drip reaches every column at any facing and the stockline
self-levels. The furnace this bell charges is resolved by the same bounded multiblock anchor scan every
furnace part uses; a bell over no furnace drips nothing.

Dropping is on by default, so a freshly built furnace feeds itself without the Ctrl+RMB toggle, and stops
by itself when every column is at its own capacity (`IsFurnaceFull` checks per column, because the crucible
columns hold one block more than the rest).

At a 48-unit tank, a full 1 216-unit charge is many hand loads; the drip itself runs at 4 u/s. There is no
automation between the burdenmaker and this hopper.

### States, rates and product

Entirely inherited. Ignition is positional and pneumatic, combustion is metered by carbon burned at the
raceway, and the yields, pools, taps and burn-out are the [cold blast furnace](blast-furnace-cold.md)'s.
Nothing on this page changes any of them.

The hot blast changes only the preheat term: at the shipped calibration a standard charge on hot blast
clears the melt line where the same charge on cold blast stalls short of it - the arithmetic is
[heat balance](../mechanics/heat-balance.md)'s.

---

## Numbers

`SiexValues.X` is a generated accessor over `SiexConfig.X`; the file:line is the config declaration.

### Owned — `mods/siex/src/SiexConfig.cs`

| Key | Value | file:line | What it does |
|---|---|---|---|
| `HopperReinforcedCapacity` | 48 u | SiexConfig.cs:86 | Reinforced-hopper tank. Small by design (skip-hoist fed) |
| `HopperMaxMagazineCapacity` | 48 u | SiexConfig.cs:89 | Bell-hopper magazine |
| `HopperDropAmount` | 4 u | SiexConfig.cs:92 | Units dripped into the shaft per bell tick (1 s) ⇒ 4 u/s |

### Owned, hard-coded — not config

| Constant | Value | Where | What it does |
|---|---|---|---|
| bell tick interval | 1000 ms | `BlockEntityHopperBell` | The bell runs its own listener, not the production-machine tick |
| outlets | 2 | the layout's `P` glyphs | the exhaust budget above |

### Cited — owned elsewhere, values not repeated here

| Key / constant | Owner |
|---|---|
| `ExhaustVolumePerTick`, `ExhaustTempFactor`, the raceway rate keys, the preheat term, both extinguish thresholds and every other `Bf*` key | [heat balance](../mechanics/heat-balance.md) |
| `BfIronPerOreUnit`, `BfSlagPerOreUnit`, the pool caps, the tap drain keys, `ChargeItemsPerBand` and the column capacity arithmetic | [cold blast furnace](blast-furnace-cold.md) |
| burden composition and the flux stamp | [burden](../items/burden.md) |
| fuel carbon values (coke 2, charcoal 1) | [fuels](../items/fuels.md) |
| `CowperIntakeVolume` (SiexConfig.cs:143), the regenerator rates | [cowper](cowper.md) |
| `SmokestackGasIntakeVolume` (SiexConfig.cs:259) | [smokestack](smokestack.md) |
| `BlastPressureThreshold`, `AirBlowerOutputPerSecond` | unowned - no page covers the smex air blower yet; see Open #4 |
| `LitresPerPipe`, burst, leak | [pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md) |

---

## Drops

| Broken block | Returns |
|---|---|
| core | itself, the same `-{side}` variant |
| reinforced hopper | itself plus the whole charge stack still in the tank, grade preserved |
| bell hopper | itself plus the magazine's own stack, grade preserved - the magazine stack is returned directly, never re-minted through an item lookup |
| tuyere / tap / outlet / brick / frozen pool | see [cold blast furnace](blast-furnace-cold.md) and [pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md) |

Breaking the core while lit extinguishes first, so the molten pool freezes onto the crucible rather than
vanishing - [cold blast furnace](blast-furnace-cold.md).

---

## Code

| Type / member | Where | Notes |
|---|---|---|
| `BlockBlastFurnaceCoreHot` | `…/HotBlastFurnace/Blocks/BlockBlastFurnaceCoreHot.cs` | `partial`, `[BlockRegister]`, `IExBlockDefProvider`; only a code-first def and the layout - no behaviour |
| `BlockEntityBlastFurnaceHot` | `…/BlockEntities/BlockEntityBlastFurnaceHot.cs` | empty body - `class … : BlockEntityShaftFurnace { }` |
| `BlockHopperReinforced` / `BlockEntityHopperReinforced` | `…/Blocks/` · `…/BlockEntities/` | the tank: `Accepts` (delegated to the core), `TryDeposit`, `TryWithdraw`, `DrawBurden`, the bell toggle, the contents mesh, the HUD |
| `BlockHopperBell` / `BlockEntityHopperBell` | same | the magazine: `PullFromTankAbove`, `DripIntoShaft` (via `NextChargeColumn`), `IsFurnaceFull`, the drop toggle |
| `BlockEntityShaftFurnace` (abstract) | iiex | the actual machine - see [cold blast furnace](blast-furnace-cold.md) |
| `BlockEntityFurnaceCore` | iiex | the heat model and the outlet loop - see [heat balance](../mechanics/heat-balance.md) |

### Where a caller hooks in

* Anything that changes how this furnace melts belongs in `BlockEntityShaftFurnace`'s product virtuals, not
  here. The hot subclass is empty and stays that way.
* Ferroalloys ([cold blast furnace](blast-furnace-cold.md) § second act) would be a charge family plus a
  metal descriptor; this block needs no change.
* An automated charge feed (skip hoist) hooks at `BlockEntityHopperReinforced.TryDeposit` / `.Accepts` -
  the tank already validates identity and grade, so a feeder only has to hand it stacks.

### Tests

| File | Covers |
|---|---|
| `Blocks/HotBlastFurnace/FurnaceGeometryTests.cs` | every offset lands on the right glyph, via iiex's shared `FurnaceLayoutRig`; outlets are non-empty |
| `Blocks/HotBlastFurnace/FurnaceOrientationMatrixTests.cs` | the same geometry in all four facings |
| `Blocks/HotBlastFurnace/BlastFurnaceTests.cs` / `BlastFurnaceLifecycleTests.cs` | shaft state, melt, extinguish freeze + burn-out, tree round-trips |
| `Blocks/HotBlastFurnace/HopperReinforcedBeTests.cs` | deposit (1 and whole-stack), capacity and grade refusals, withdraw, `DrawBurden`, the Ctrl toggle, round-trip |
| `Blocks/HotBlastFurnace/BellHopperTests.cs` | dropping default (incl. a saved tree that omits the flag), magazine round-trip, `IsFurnaceFull`, pull-from-tank, drip |
| `Scenarios/BlastFurnaceScenarioTests.cs` | the whole plant: build → blow → melt → tap into a canal, hot vs cold blast, starvation, live retune |
| `Scenarios/HotBlastScenarioTests.cs` | the cowper/stack half - see [cowper](cowper.md) and [smokestack](smokestack.md) |

The hot furnace, not the cold one, carries the suite's only end-to-end blast-furnace plant coverage.

---

## Gotchas

1. The outlet legend is a bare wildcard. The taps and tuyeres are facing-pinned in the layout, but `P` is
   `iiex:pipe-outlet*`, so an outlet fitted the wrong way round completes the structure and then joins the
   wrong neighbour - read in game as "I built it and it does not vent".

2. The bell runs on its own tick, outside the machine framework. It is a plain `BlockEntity` with a 1000 ms
   listener, not a `BlockEntityProductionMachine`, so it gets no `dt` catch-up clamp and no away-catch-up.
   A furnace left loaded-but-unattended and one reloaded from disk charge differently.

3. The hopper pair is found by bare vertical neighbour reads. The bell pulls from whatever
   `BlockEntityHopperReinforced` is directly above it, and the multiblock never validates the pair. A
   detached bell charges nothing (the drip needs the anchored core), but the only warning is
   `siex:hopper-info-nobell` on the hopper's info line, never on the bell's.

4. The exhaust outlets are write-only and the furnace never verifies them. The outlet cells are resolved
   off the layout's `CellRole.GasOutlet` marks, and the tick loop calls `TryProduce` on whatever
   `IPipeNode` is there. A cell holding something that is not an `IPipeNode` is silently re-scanned every
   tick and does not count as a failure, so the furnace reports not choked while venting nothing, and the
   cowpers starve with no error anywhere.

5. `blockdesc-blastfurnacecore*` calls the core "the refractory hearth grate"
   (`mods/siex/assets/siex/lang/en.json:32`). It is a plain cube; the grate shape is gone.

6. The core def has no `Handbook(...)` grouping, so its four `side` variants list separately in the in-game
   handbook where the cowper and smokestack intakes group into one entry.

---

## Open

1. Deferred - the enlarged draft. [layouts.md](../../../workbench/layouts.md) § 1 carries a 4 × 4-shaft
   redesign (four tuyeres, four exhaust outlets, four bell cells, no reinforced hopper). It is recorded,
   not scheduled. It predates the charge-column cutover and the typed-tap rename, so its legends are known
   to be stale (block codes that no longer exist, a slag-tap legend that duplicates the metal tap's) and it
   drops the reinforced hopper the bell pulls from. Landing it means reconciling it against the current
   part blocks and the column model, not editing the ASCII.

2. `siex:hopperreinforced` is a skip-hoist buffer with no skip hoist. The 48-unit tank is sized for a
   feeder that does not exist, so in play a full charge is many hand loads. Either build the feeder or
   raise the tank.

3. The core is missing from the recipe-cost catalogue (above, Construction).

4. No page owns the smex air blower. `BlockEntityEngineAirBlower` is the suite's only pressurised air
   source at this tier and the thing that makes the tuyeres' pressure gate reachable, yet it has no design
   page. When writing one: `DoWork` multiplies the config value by a bare literal
   (`SiexValues.AirBlowerOutputPerSecond * 3`), so the shipped output is three times what the config key
   and its doc-comment advertise.
