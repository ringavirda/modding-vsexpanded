# Density rule

**Status** designed - settled as an authoring rule, implemented for exactly one item, not generated anywhere
**Mod** cross-cutting (an authoring convention). The only code that applies it today is iiex.

**Owns**
* the constant 1 voxel³ = 2.5 units, and the fact that it is the sole source of every metal mass in the
  suite;
* its derivation from vanilla - the measured shape volumes, their melt-back masses, the per-item ratios and
  the mean;
* the measurement procedure (what counts as "solid volume" in a VS shape file) and the rounding rule;
* the audit of every mass currently shipped against the rule, and which are stale;
* the fact that the rule appears in neither `conventions.md` nor `materials.md`, and that
  `materials.md` still carries a third, contradictory set of masses;
* the plan to generate masses from shapes, and the four things blocking it.

**Does not own** - cited only: the canonical mass of any individual item (that belongs to its own page),
the rolling ladder and its cut arithmetic ([rolling](../processes/rolling.md)), casting cavity capacities
([casting cell](../machines/casting-cell.md)), and the recipe-cost catalogue
([recipes & config](recipes-config.md)).

**Depends on** nothing. This is a root fact.
**Depended on by** [pig](../items/pig.md) · [stock](../items/stock.md) ·
[cast parts](../items/cast-parts.md) · [rolled parts](../items/rolled-parts.md) ·
[rolling](../processes/rolling.md) · [casting](../processes/casting.md) ·
[casting cell](../machines/casting-cell.md)

---

## Role

The rule makes mass a function of geometry: a part is drawn, and its mass follows. Nobody picks a number,
so a mass that disagrees with its art is a detectable defect rather than a matter of opinion.

The product ladder falls out of the same arithmetic. Vanilla's own items obey the same density, so a
rolled rod drawn at vanilla `rod` geometry is 100 u: the mill route cannot mint or destroy a unit against
the hand route.

---

## How it works

### The rule

> **1 voxel³ = 2.5 units.** Measure the solid volume of the drawn shape in voxel³, multiply by 2.5, round to
> a value that divides cleanly through the product ladder.

It is metal-blind: cast iron and wrought iron at the same volume are the same number of units. This is not
the same thing as the `MaterialDensity` on an item definition - see Gotchas #1.

### Derivation — measured off vanilla

Volumes read from the installed game at `D:/Gaming/Others/Vintagestory`:

| Vanilla item | Shape file | Element | Dimensions | vx³ | Mass | u/vx³ |
|---|---|---|---|---|---|---|
| `ingot` | `assets/survival/shapes/item/ingot.json` | `ingot` `[6.5,0,4.5]→[9.5,2,11.5]` | 3 × 2 × 7 | 42 | 100 | 2.381 |
| `rod` | `assets/survival/shapes/item/rod.json` | `Cube2` `[3,0,7]→[13,2,9]` | 10 × 2 × 2 | 40 | 100 | 2.500 |
| `metalplate` | `assets/survival/shapes/item/plate.json` | `Cube1` `[3.5,0,3.5]→[12.5,1,12.5]` | 9 × 1 × 9 | 81 | 200 | 2.469 |

Mean 2.450 u/vx³, spread ±2.5 %.

The masses are vanilla's own, cross-checked two ways:
* an ingot is 100 u - the mold fill quantity, and the scale the mod already uses (`MoldDefaultUnits` = 100,
  `src/IronIndustryExpanded/IiexConfig.cs:138`; vanilla's tool molds are on the same scale at
  `assets/survival/blocktypes/clay/fired/toolmold.json:91-124`, 100 / 200 / 900);
* the smithing recipes confirm the ingot count. `assets/survival/recipes/smithing/rod.json` is a 2-layer
  2 × 10 pattern = 40 voxels, inside one ingot's 42 → one ingot → 100 u.
  `assets/survival/recipes/smithing/plate.json` is a single-layer 9 × 9 pattern = 81 voxels, past one
  ingot's 42 → two ingots → 200 u.

### Why 2.5 and not the measured 2.450

1. It is within 2.5 % of vanilla's mean - inside vanilla's own spread.
2. It lands `rod` exactly: 40 vx³ × 2.5 = 100 u, at vanilla `rod`'s own 2 × 2 × 10 geometry. A rolled rod
   drawn to that shape is a drop-in for `game:rod`.
3. It divides. The pig denomination chain (150 → 25 → 5) and the mill's crop counts all need exact division;
   2.45 produces fractions everywhere.

### Measuring a shape

VS shape space is 16 units per block edge, and one unit is one voxel edge, so an element's
`(to − from)` product is directly its voxel³ volume. What the measurement must get right:

* Children are drawn in the parent's local frame. A child that is offset out of the parent is
  additional solid (`assets/iiex/shapes/item/castbillet.json`: `CastBillet1` at `z 0…12` with child
  `CastBillet11` at `z −12…0` - two adjacent halves, total 3 × 3 × 24). A child that overlaps the parent is
  decoration and must not be counted.
* Hollow and toothed geometry is not a box. `item/cast-barrel.json` (a cored vessel drawn as four wall
  slabs plus a base) sums to 603 vx³ naively against a shipped 200 u; `item/gearbevel.json` sums to 488 vx³
  against a shipped 40 u. Neither is derivable by summing elements.
* Rotated elements (`rotationOrigin` / `rotation`) make the axis-aligned box product wrong; every shape
  measured for a mass so far is axis-aligned, and that is a constraint on the art, not a coincidence.

### Rounding

Round to a number that divides through everything downstream. The rule produces a candidate; the ladder
picks the nearest workable value. The drawn pig measures 156 vx³ → 390 u and the settled mass is 375
(= exactly 150 vx³), because 375 divides where 390 does not.

---

## Numbers

### The rule itself

| Constant | Value | Where | Status |
|---|---|---|---|
| units per voxel³ | 2.5 | `src/IronIndustryExpanded/Items/PigBreaking.cs:36-37` - the only place in the codebase that expresses it, and even there it is derived (`ItemPig.PigUnits / PigVoxels`) rather than declared | no shared constant exists |
| `PigBreaking.PigVoxels` | 150 | `PigBreaking.cs:22` | the anvil work-item footprint, chosen so 375 / 150 = 2.5 exactly; documented in-source as moving with `PigUnits` |
| pig anvil footprint | 5 × 3 × 10 = 150 vx | `Items/ItemPig.cs:141-148` | a solid block positioned to cover the small `smithing/pig` recipe shape; not the drawn pig shape |

### Settled masses

| Item | Settled mass | Implied shape | Code today |
|---|---|---|---|
| pig | 375 u | 150 vx³ | 375 u (`Items/ItemPig.cs:39`) - matches |
| castplate-heavy | 500 u | 10 × 2 × 10 = 200 vx³ | 160 u (`Items/CastPartItemDefinitions.cs:35`) - stale, and the drawn shape is 12 × 2 × 12 |

### Audit — every shipped mass against the rule

"Drawn" is the sum of the shape's elements as measured above. Neither the shape nor the constant is
authoritative on its own; the table records where they disagree.

| Item | Constant (file:line) | Ships | Shape file | Drawn vx³ | Rule says | Verdict |
|---|---|---|---|---|---|---|
| `pig` | `Items/ItemPig.cs:39` | 375 | `assets/iiex/shapes/pig.json` (5×2×12 + 3×1×12) | 156 | 390 → settled 375 | matches the settled mass |
| `pigchunk` | `Items/ItemPig.cs:40` | 25 | `game:item/ore/ungraded/coke` (borrowed) | n/a | 1/15 of the pig | denomination - re-cuts with the pig |
| `pigbit` | `Items/ItemPig.cs:41` | 5 | `game:item/nugget` (borrowed) | n/a | ⅕ of a chunk | denomination |
| `slagbrick` | `Items/SlagItemDefinitions.cs:27` | `= PigUnits` | shares the pig's bed cavity | n/a | follows the pig | correct by construction |
| `castplate-heavy` | `Items/CastPartItemDefinitions.cs:35` | 160 | `assets/iiex/shapes/item/heavyplate.json` (12×2×12) | 288 | 720 as drawn; settled 500 | stale, and the art must be redrawn |
| `cast-barrel` | `Items/CastPartItemDefinitions.cs` | 200 | `…/item/cast-barrel.json` (hollow) | 603 (meaningless) | not naively derivable | needs a solid-volume measure |
| `bevelgear` | `Items/BevelGearItemDefinitions.cs:12` | 40 | `…/item/gearbevel.json` (toothed) | 488 (meaningless) | not naively derivable | needs a solid-volume measure |
| `stock-shingledbar` | `BlockStructures/Forming/StockItemDefinitions.cs:24` | 400 | `…/forming/stock-shingledbar-*.json` | 144 as drawn | 405 for the settled 3 × 3 × 18 | mass settled 2026-08-12; the **art** is still the 16-long one |
| `stock-shingledslab` | `…/StockItemDefinitions.cs:25` | 1200 | `…/forming/stock-shingledslab-*.json` | 480 | 1200 | correct, art included |
| `castmold-plate` cavity | `BlockStructures/Casting/PatternItemDefinitions.cs:83` | 136 | `…/item/moldplate.json` | 292 | 730 | stale |
| `castmold-doubleingot` cavity | `…/PatternItemDefinitions.cs:91` | 152 | `…/item/molddoubleingot.json` | 328 | 820 | stale |

Art drawn but with no item yet - the rule's answer, for whoever wires them up:

| Shape | Drawn | vx³ | Rule says |
|---|---|---|---|
| `…/item/castbillet.json` | 3 × 3 × 24 | 216 | 540 u |
| `…/item/castbloom.json` | 4 × 4 × 24 | 384 | 960 u |
| `…/item/castslab.json` | 12 × 4 × 28 | 1344 | 3360 u |

### Where 160 came from

The heavy plate's casting cavity box is `Box(7,4,4, 9,14,12)` - 2 × 10 × 8 = 160 vx³ - and its mass is
160 u (`BlockStructures/Casting/PatternItemDefinitions.cs:73-78`). The two were written together at an
implicit 1 u/vx³. The other cavities are not self-consistent with that: the mold-plate box is
10 × 2 × 10 = 200 vx³ against 136 u (0.68), the double-ingot box the same 200 vx³ against 152 u (0.76), the
barrel box 8 × 8 × 8 = 512 vx³ against 200 u (0.39). Four cavities, four different implicit densities.

The cavity box is documented as the fill-glow region rather than a mass source, so the 160 = 160 match is
evidence of how the number was picked, not a second definition of mass.

---

## Code

| Member | file:line | Notes |
|---|---|---|
| `PigBreaking.UnitsPerVoxel` | `src/IronIndustryExpanded/Items/PigBreaking.cs:36-37` | `ItemPig.PigUnits / (float)PigVoxels` = 2.5. The only expression of the rule in code |
| `PigBreaking.PigVoxels` | `…/PigBreaking.cs:22` | 150 |
| `PigBreaking.Emit` | `…/PigBreaking.cs:45-55` | Converts shed voxels → whole chunks + bits, carrying the sub-bit remainder on the work item. Pure and deterministic so conservation is unit-testable |
| `ItemPig.PigUnits / ChunkUnits / BitUnits` | `…/ItemPig.cs:39-41` | 375 / 25 / 5 |
| `ItemPig.CreatePigVoxels` | `…/ItemPig.cs:141-148` | Fills a 5 × 3 × 10 metal block on the anvil |
| `ItemPig.TryPlaceOn` | `…/ItemPig.cs:104` | Places the work item and tags it so the helve patch acts only on pigs |
| `AnvilPigBreakingPatches` | `src/IronIndustryExpanded/Patches/` | Pays the shed voxels out as chunks and bits |
| Consumers of the constants | `BlockStructures/Casting/BlockEntities/BlockEntitySandCastingBed.cs:418-422`, `:479`, `:619`; `BlockStructures/Casting/SandBedLayout.cs:167`; `BlockStructures/Casting/PatternItemDefinitions.cs:75` | Denomination and cavity maths |
| Tests | `test/IronIndustryExpanded.Tests/Items/PigBreakingTests.cs` | Asserts the 2.5 u/voxel payout and end-to-end conservation |

Where a mass is declared today: as a C# `const` in the code-first item definition, then stamped onto the
item as a `materialUnits` attribute - e.g. `ItemPig.cs:62`, `CastPartItemDefinitions.cs:110`,
`StockItemDefinitions.cs:44`.

---

## Gotchas

1. "Density rule" ≠ `MaterialDensity`. `.MaterialDensity(7200)` on the cast-iron items
   (`ItemPig.cs:60`, `CastPartItemDefinitions.cs`) and `.MaterialDensity(7800)` on the wrought stock
   (`StockItemDefinitions.cs:43`) are real-world kg/m³, used by the engine for weight and physics. They are
   unrelated to the unit rule, and they disagree with it: physically, cast and wrought at equal volume
   differ by 8 %, but the unit rule is metal-blind by design. Do not "reconcile" them.

2. The `materialUnits` attribute is written and never read. A grep for `materialUnits` outside the
   `.Attribute(...)` calls returns nothing in `src/`. Every machine reads the C# constant instead
   (`ItemPig.PigUnits`, `CastPartItemDefinitions.HeavyPlateUnits`, …). Changing a mass means changing the
   constant; changing only the attribute changes nothing but the tooltip.

3. The pig's 150 anvil voxels are not the pig's drawn shape. `PigVoxels` = 150 is the anvil
   footprint - a 5 × 3 × 10 solid positioned to cover the small `smithing/pig` recipe shape. The drawn item
   is 156 vx³, rounded down to 150 so the mass divides (156 × 2.5 = 390 → settled 375 = 150 × 2.5). The
   in-source doc on `PigVoxels` states the invariant: it moves with `PigUnits`, always, or an anvil voxel
   of pig iron would be denser than a pig-iron voxel anywhere else.

4. Re-massing the pig re-cuts the denominations. Chunk and bit are 25 u and 5 u; a 375 u pig is 15
   whole chunks, and the shed-voxel remainder pays out in 5s (`PigBreaking.cs:45-55`). Any future re-mass
   must re-pick the ratios so that `PigUnits`, `ChunkUnits` and `BitUnits` still divide, and so
   `SlagBrickUnits` (which is defined as `= PigUnits`, `SlagItemDefinitions.cs:27`) still divides every
   casting-bed cavity - a test already pins that.

5. Two shape idioms defeat naive measurement. Hollow/toothed art (barrel, gear) and the stage-element
   idiom used by the rolled stock - one top-level element per rolled stage, with cut divisions as children -
   mean one file can hold many masses, or none that a box sum can find.

6. `materials.md` § "Unit economy" is a third, independent source of masses.
   `docs/design/materials.md:85-99` lists a sand pig at 200 u (code: 375, rule: 375) and a heavy plate at
   400 u (code: 160, rule: 500), plus a dozen items that do not exist. It is labelled "(baseline, tunable)"
   and agrees with nothing.

---

## Open

1. The rule is documented in neither of the two places that should carry it. A grep for
   `voxel|density|2.5 u|vx` over `docs/design/conventions.md` (425 lines) and `docs/design/materials.md`
   (162 lines) returns zero hits in both. The rule that now generates every number in the mod lives only on
   this page. Whether `conventions.md` should own a pointer to it, and `materials.md` should be deleted in
   favour of generation, is the open decision.

2. Nothing is generated yet. The plan is that masses become derived from shapes, the same trick
   `layouts-workbench.md` uses for multiblock layouts - parse the shape's `elements[].from/to`, compute solid volume,
   emit the constant. The machinery is largely present (shapes are plain JSON; `ExShapeElements.Pruned`
   already selects a named subtree; the code-first item defs are the single place a mass is written; the
   goldens harness can prove the change is pure motion). Four things are missing:
   * a solid-volume measure that handles overlap, nesting and hollow art (Gotchas #5);
   * a convention for which subtree of a stage-element shape a mass is measured from;
   * a rounding policy that respects the ladder's exact-division requirement;
   * a way to express derived masses (denominations, cavity-shared masses) that have no shape of their own.

3. Every stale mass in the audit is an unmade decision, not a bug to fix blindly. Re-massing
   `castplate-heavy` from 160 to 500 moves casting cavity capacities, the `ExRecipeCosts` catalogue and every
   machine bill of materials that consumes it. The settled decision fixes the target; the migration is not
   planned.

4. The cast stock art does not match the ladder. `castbillet` is drawn at 216 vx³, `castbloom` at 384,
   `castslab` at 1344 - while the settled stock ladder specifies 243 / 400 / 1200 so that every crop
   divides. Either the art or the ladder moves; the rule is not the thing in dispute.

5. No shared constant. If the rule stays hand-applied, `2.5` should exist once - as an exlib constant
   with the derivation in its doc-comment - instead of emerging from a division in `PigBreaking`.
