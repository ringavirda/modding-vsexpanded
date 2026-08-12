# Cupola furnace

**Status** live - buildable in survival end to end   **Mod** iwex

**Owns** - the facts this page is canonical for:

* the statement that the cupola is a pure data override of the blast furnace, and the exact list of members
  it overrides - nothing else in the machine differs;
* its structure: the 64-cell layout, its origin, the 5-cell single-column shaft, one tuyere, two taps, a
  side-charging hopper, and the cell counts per glyph;
* its charge identity - metal charged directly (pig, chunks, bits, scrap) plus fuel, measured in metal
  units, and the `CupolaChargeMetalUnitsPerBlock` quantum;
* the `Cupola*` config keys not already claimed by [heat balance](../mechanics/heat-balance.md):
  `CupolaCastIronPerMeltCycle`, `CupolaSlagPerMeltCycle`, `CupolaBlastMixPerMeltCycle`,
  `CupolaMaxMoltenCastIron`, `CupolaMaxMoltenSlag`, `CupolaChargeMetalUnitsPerBlock`;
* its product identity - cast iron out of the lower tap, `iwex:hearthmetal-castiron` on the hearth - and its
  yield ratio to the blast furnace;
* its construction recipe;
* the design fact that the cupola is small on purpose and multiplied, and that its third job is melting
  ferromanganese for the ladle.

**Does not own** - cited only, never restated:
[blast furnace (cold)](blast-furnace-cold.md) - the machine itself, the shared `BlockEntityShaftFurnace`,
the charge-column model, the tuyere / tap / tall-hopper part blocks and the tap drain path ·
[heat balance](../mechanics/heat-balance.md) - the `T_process` law, the raceway rate model, blast demand,
and `CupolaCastIronMeltingPoint` / `CupolaTuyereIntakeVolume` ·
[charge-pile](charge-pile.md) - the pile block and the band scale ·
[molten network](../mechanics/molten-network.md) · [pipe network](../mechanics/pipe-network.md) ·
[multiblock](../mechanics/multiblock.md) · [recipes & config](../mechanics/recipes-config.md) ·
[fuels](../items/fuels.md) · [ironmaking](../processes/ironmaking.md) (residue payout) ·
[twin-tub-blower](twin-tub-blower.md) · [ladle](ladle.md) · [crucible furnace](crucible-furnace.md)

---

## Role

The iron tier's recycler and parts foundry. It melts metal that is already metal - pig, pig chunks, pig bits,
iron and steel scrap - into **cast iron**, and hands it to the
[molten network](../mechanics/molten-network.md) for casting into machine parts. It is the only route to cast
iron in the suite. Scrap has somewhere to go, and a player who needs one `castframe` does not have to fire
the 160-cell [blast furnace](blast-furnace-cold.md) and reduce ore to get it.

Cast iron melts at 1200 °C against wrought iron's 1482, so the cupola clears its own line on an ordinary
charge while ore reduction does not: remelting scrap is an iron-age job, reducing ore is not. The numbers
behind that are [heat balance](../mechanics/heat-balance.md)'s.

### It is small on purpose

A cupola is a batch furnace the player lights, taps and lets out; the answer to "I need more cast iron" is
build another one, not build a bigger one. Its granularity matches what it feeds: a `castframe` or
`castplate-heavy` is one or two small dense charges, where a whole blast-furnace heat would be the wrong
shape of decision. A bank buys throughput only - nothing rewards the grouping itself, per
[plant-layout](../mechanics/plant-layout.md).

### Its third job: ferromanganese for the ladle *(designed, not built)*

Beyond cast iron and scrap, the cupola's steel-era job is melting ferroalloys for the [ladle](ladle.md). In a
cupola the fuel and the metal are in contact, so the charge carburises - ruinous for tool steel (that is the
[crucible furnace](crucible-furnace.md)'s job) but free for ferromanganese and spiegeleisen, which are
high-carbon by definition. This duty survives the 2026-08-07 two-reagent ruling
([recarburising](../processes/recarburising.md)): a small recarburising trim is thrown in solid, but every
large addition must arrive molten from here - the ~1000 u of high-C ferromanganese a hadfield heat takes, and
the 540–810 u spiegeleisen dose of the rail-grade route - which is why Bessemer plants kept a cupola melting
spiegeleisen. Nothing of this exists in `src/`: no ferroalloy metal descriptor, and no `Ladle` type anywhere.

---

## Structure

Anchor: `iwex:furnace-cupolacore-{tier}-{side}`, bottom centre of the lowest layer directly under the
shaft - the same arrangement as the blast furnaces. `Origin(-1, -1)`, so the `C` glyph lands on the anchor's
own (0, 0, 0).

Source of truth: the seven ASCII cross-sections in `BlockCupolaFurnaceCore.cs` (y = 0 hearth floor → y = 6
open stack). Golden: `test/IronworkingExpanded.Tests/goldens/iwex/blocktypes/furnace/cupolacore.json`.

### Cell census — 64 offsets

| Glyph | Required block | Count |
|---|---|---|
| `#` | refractory bricks (`VanillaCodes.Refractory`) | 52 |
| `C` | `iwex:furnace-cupolacore` (the anchor) | 1 |
| `I` | `iwex:furnace-irontap`, facing east - the west wall, pours out west | 1 |
| `S` | `iwex:furnace-slagtap`, facing west - the east wall, pours out east | 1 |
| `T` | `iwex:furnace-tuyere`, orientation `n` - the cupola is blown from one wall only | 1 |
| `H` | `iwex:hopper-tall`, facing west | 1 |
| `f` | `exlib:structurefiller` (the hopper's own top cell) | 1 |
| `c` | the shaft - the air / pile / hearth-metal alternation | 4 |
| `h` | the crucible floor - same alternation, own glyph for its pool role | 1 |
| `a` | `game:air` - the stack throat | 1 |

64 cells against the cold blast furnace's 160. The taps, tuyere and hopper are all orientation-pinned in the
legend, so a part fitted the wrong way round does not complete the structure.

### Functional cells — read off the drawing

The roles are layout marks; the one hand-declared cell left is `ShaftCentre`.

| Role | Cells | Glyph |
|---|---|---|
| `Chargeable` | 5 — the single column `(0, 1, 0)` … `(0, 5, 0)` | `c` + `h` |
| `Pool` | `(0, 1, 0)` — one crucible cell | `h` |
| `Tuyere` | `(0, 2, −1)` — one | `T` |
| `MetalTap` | `(−1, 1, 0)` | `I` |
| `SlagTap` | `(1, 1, 0)` | `S` |
| `GasOutlet` | empty — the drawing carries no outlet glyph; the open top is the stack | — |
| `ShaftCentre` | `(0, 3, 0)` — declared on the BE, a geometric point | |

The shaft box is exactly the shaft - the cupola is the only furnace whose box is its charge volume rather
than merely containing it. The two taps sit on opposite walls of the one hearth cell: cast iron out low to
the west, slag off the top of the bath to the east.

### Orientation and interactive cells

Orientation comes from the core's `side` variant exactly as the blast furnace's does - see
[blast furnace § Orientation](blast-furnace-cold.md#orientation). The interactive cells are the same set,
minus one tuyere: core, two taps, one tuyere, and the hopper's top filler cell.

---

## Assets

The cupola introduces one asset of its own; everything else it borrows.

| Asset | Path | State |
|---|---|---|
| core block model | `game:block/basic/cube` | vanilla cube |
| core faces | `game:block/clay/refractory/{tier}/front1`, with `iwex:block/furnace/n` north and `iwex:block/furnace/cf` south | `assets/iwex/textures/block/furnace/cf.png` — the cupola's only own art; the "CF" label is what makes a built cupola read apart from a blast furnace |
| tuyere, tap, tall hopper, hearth metal | see [blast furnace § Assets](blast-furnace-cold.md#assets) | shared; not redefined here |

No shape and no animation are unique to the cupola. There is no handbook page either -
`assets/iwex/config/handbook/` ships `00-ironworking` … `04-designtable` and nothing for the cupola.

---

## Construction

One grid recipe for the core; the rest is the shared fittings plus 52 refractory bricks and one
`exlib:structurefiller` (placed by the hopper itself).

| Output | Pattern | Ingredients |
|---|---|---|
| `iwex:furnace-cupolacore-{tier}-n` | `BRB,PCP,BRB` (3×3) | 4 × `game:refractorybrick-fired-*` (tier captured as `{tier}`), 2 × rod, 2 × plate, 8 × fire clay |

A full 3×3 with fire clay at its heart, so it can never collide with the blast-furnace core's pattern, and
cheaper than it. Golden: `test/IronworkingExpanded.Tests/goldens/iwex/recipes/grid/cupola.json`.

The tuyere recipe takes `iwex:pipe-straight*`. Details:
[blast furnace § Construction](blast-furnace-cold.md#construction).

---

## Operation

### It is the blast furnace, with different data

`BlockEntityCupolaFurnace : BlockEntityShaftFurnace` contains no methods - only property overrides. The
raceway, columns, melt, taps, HUD and extinguish freeze are all inherited unchanged. The whole override
surface:

| Group | Members |
|---|---|
| **Charge identity** | `IsChargeItem` / `IsChargeCode` = scrap role or fuel; `ChargeUnitsPerBlock` = `CupolaChargeMetalUnitsPerBlock` |
| **Product identity** | `MetalProductCode` = `"castiron"`, `MoltenProductInfoLangKey`, `SolidProductBlock` = `iwex:hearthmetal-castiron` |
| **Tunables** | `MeltingPoint`, `TuyereIntakeVolume`, `ProductPerUnit`, `SlagPerUnit`, `MaxMoltenProduct`, `MaxMoltenSlagPool` |
| **Geometry** | `ShaftCentre` only — everything else is layout roles |

Both charge seams are overridden, and that is not belt-and-braces: a furnace that overrides `IsChargeItem`
and not `IsChargeCode` gates its hand-placed piles and its layered columns differently, with nothing failing
visibly. The `|| IsFuelCode` half cannot be dropped either - coke rounds are charge too, and written
scrap-only the cupola's coke would stop counting toward its fullness, carbon and heat balance.

What it does not override, and why each matters:

* `BlastPressureThreshold` - the cupola demands the same reference blast pressure as the blast furnace.
* `RequiresBlast` (true) - a dead blower starves and eventually snuffs a cupola exactly as it does a blast
  furnace.
* Ignition, extinguish and exhaust behaviour - the shaft branch's, unchanged. The cupola has no cadence
  keys: a shaft's campaign ends when its carbon does, and its melt cadence is the descent
  ([heat balance](../mechanics/heat-balance.md)).

### Inputs → outputs

| In | Out |
|---|---|
| metal, charged directly - pig, pig chunks, pig bits, iron/steel scrap (the `scrap` material role) - plus fuel in alternating rounds | molten cast iron down the lower tap |
| pressurised air at its single tuyere | molten slag down the upper tap |
| — | on extinguish: `iwex:hearthmetal-castiron` on the one hearth cell + burnt-out salvage in the column |

Charging the metal itself avoids an intermediate remelt-burden item, which would need a producing machine of
its own and would tie cast iron's survival source to that machine. Ore burden charged into a cupola burns but
never converts - it still counts toward the fire, still lights, still burns out to salvage, and pins
`ConversionBlocked`.

### The player's verbs

Identical to the [blast furnace's](blast-furnace-cold.md#the-players-verbs), on a smaller structure: build
the core, Ctrl+Shift+RMB anywhere on core/tap/tuyere/hopper for the outline, charge through the hopper's top
filler cell (or hand-place charge in the column), join the tuyere to the blast main, and RMB the taps with an
empty hand once a canal start is in place. The hopper asks the core where to lay (`NextChargeColumn`), so it
charges the column correctly at any facing.

### Rates

Yield is 5.0 u of cast iron and 2/3 u of slag per unit of charge - the 60 : 8 : 12 ratio stated per unit
(`ProductPerUnit` / `SlagPerUnit`). The yield is flat, not scaled by an ore share: a cupola remelts rather
than reduces, so there is no ore fraction for the recovery ladder to be stated against. Compare the blast
furnace's 8.5 u per unit of ore content.

Production is metered by the carbon burned at the raceway, like every shaft furnace. The cupola is slower
than a blast furnace because its single tuyere burns less carbon, and because its pile is far denser -
3 000 metal units a block against an ore shaft's 32 items, so the same carbon rate stretches over a much
larger charge. The pacing is geometry, not a constant.

---

## Numbers

`IwexValues.X` is a generated accessor over `IwexConfig.X`. Only keys this page owns carry values.

### Owned — `src/IronworkingExpanded/IwexConfig.cs`

| Key | Value | What it does |
|---|---|---|
| `CupolaCastIronPerMeltCycle` | 60 u | Numerator of the yield ratio (60 per 12 charge units) |
| `CupolaSlagPerMeltCycle` | 8 u | Slag numerator |
| `CupolaBlastMixPerMeltCycle` | 12 | The ratio's denominator ⇒ 5.0 u metal per charge unit |
| `CupolaChargeMetalUnitsPerBlock` | 3000 u | The remelt pile's per-block quantum — a 5 u bit, a 25 u chunk and a 375 u pig all count their own units |
| `CupolaMaxMoltenCastIron` | 1200 u | Cast-iron pool ceiling — half the blast furnace's |
| `CupolaMaxMoltenSlag` | 300 u | Slag pool ceiling |

### Owned, hard-coded — not config, source-only

| Constant | Value |
|---|---|
| metal token | `"castiron"` → `assets/iwex/config/metals/castiron.json` |
| solid product block | `iwex:hearthmetal-castiron` |
| charge identity | the `scrap` material role, or fuel |
| `ShaftCentre` | `(0, 3, 0)` |

### Cited — owned elsewhere, values not repeated here

| Key | Owner |
|---|---|
| `CupolaCastIronMeltingPoint`, `CupolaTuyereIntakeVolume`, and every `Bf*` heat/blast key the cupola inherits | [heat balance](../mechanics/heat-balance.md) |
| `HopperTallCapacity`, `HopperTallDropPerSecond`, the tap drain keys | [blast furnace (cold)](blast-furnace-cold.md) |
| the band scale (a cupola band is 187.5 u) | [charge-pile](charge-pile.md) |
| `BfBurnoutFuelRetained*`, the recovery ladder | [ironmaking](../processes/ironmaking.md) |

---

## Drops

| Broken block | Returns |
|---|---|
| cupola core | itself, same `{tier}-{side}` variant; `MaxStackSize` 4 |
| tap / tuyere / tall hopper / refractory brick / charge pile | see [blast furnace § Drops](blast-furnace-cold.md#drops) — the same blocks, the same drops |
| `iwex:hearthmetal-castiron` | metal bits × the stamped count |

Cast iron and pig iron both drop `game:metalbit-iron`: mod alloys shatter to vanilla bits as scrap via their
`solidDrop`, so there is no branch.

---

## Code

| Type / member | Where | Notes |
|---|---|---|
| `BlockCupolaFurnaceCore` | `…/Furnaces/Blocks/BlockCupolaFurnaceCore.cs` | `partial`, `[BlockRegister]`; only a code-first def and the layout |
| `BlockEntityCupolaFurnace` | `…/Furnaces/BlockEntities/BlockEntityCupolaFurnace.cs` | property overrides only — zero methods |
| `BlockEntityShaftFurnace` (base) | iwex | everything the cupola does; see [blast furnace § Code](blast-furnace-cold.md#code) |
| `BlockEntityFurnaceCore` (base) | iwex | the fired core; see [heat balance](../mechanics/heat-balance.md) |
| `BlockHearthMetal` | `…/Products/Blocks/BlockHearthMetal.cs` | the frozen pool, metal in the code |

### Where a caller hooks in

The cupola is the hook: a new melting furnace on this machine overrides the same groups - charge identity,
product identity, tunables - plus its geometry, and inherits the rest. A ferroalloy furnace is the next
instance of the pattern.

### Tests

| File | Covers |
|---|---|
| `test/IronworkingExpanded.Tests/Blocks/Furnaces/FurnaceGeometryTests.cs` | every cupola offset resolves to the right layout glyph; exactly one tuyere; no exhaust outlets |
| `…/FurnaceOrientationMatrixTests.cs` | the same geometry in all four facings |
| `…/ChargeCodeGateTests.cs` | the two charge seams agree — item gate and code gate answer alike |
| `…/FurnaceHudDistributionTests.cs` | the cupola's metal tap says cast iron, not pig iron |
| `test/LowPressureExpanded.Tests/Scenarios/CupolaScenarioTests.cs` | the full lifecycle: ignition, melt, tapping cast iron and slag, extinguish freeze + salvage, the ore-burden refusal |

---

## Gotchas

1. **A cupola dies fast from an empty raceway.** Ignition is positional - a complete raceway course of
   carbon and air at pressure - and `RacewayHoldsCarbon` fails the tick the lowest course runs out of
   carbon, with no grace. On a one-column furnace the lever is entirely what is in front of the tuyere, not
   how much is loaded above it.

2. **The cupola inherits `BlastPressureThreshold` from the blast furnace**, so its blast main must hold the
   same pressure - easy to miss when reading `BlockEntityCupolaFurnace.cs`, which overrides only
   `TuyereIntakeVolume`.

3. **`ShaftCentre` is inherited-looking but load-bearing.** `(0, 3, 0)` happens to be the same literal as
   the blast furnace's, but the cupola re-declares it because its shaft is a column - that cell is where the
   ignition whoosh, the fire ambience and the extinguish hiss play. Changing the shaft drawing without
   moving it puts the audio outside the shaft.

4. **The extinguish freeze has exactly one cell to land on.** The cupola's `Pool` role is a single cell, and
   the core refuses to overwrite a pile it did not place - so a cupola with rejected charge standing on its
   hearth cell at the moment it goes out has nowhere to freeze its pool. The blast furnace has three cells
   to try.

5. **Charge art is borrowed.** Pig and scrap in the pile draw as coke until their own shape elements are
   added - `BlockChargePile.ElementOf` falls back to coke by design ([charge-pile](charge-pile.md) § Open).

---

## Open

1. **Ferroalloy melting is unbuilt**, and it is the cupola's designed third job. It needs a ferroalloy
   metal descriptor in `assets/iwex/config/metals/`, a charge identity that carries it, and a
   [ladle](ladle.md) - which does not exist as a type anywhere in `src/`.

2. **No handbook page.** Every other live iwex machine family has one.

3. **The cupola's scenario suite lives in the wrong project.** `CupolaScenarioTests` and `CupolaScenes` are
   in `test/LowPressureExpanded.Tests/`, testing an iwex machine - against the per-mod test-homing rule.

4. **The [layouts.md](../../internal/workbench/layouts.md) workbench copy has drifted**: its cupola section still
   spells the retired `iwex:cupolafurnacecore` anchor code and a `Y` tuyere glyph where the shipped drawing
   uses `T`. Regenerate it from the golden.
