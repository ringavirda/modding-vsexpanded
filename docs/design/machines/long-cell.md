# Sand Casting Long Cell
**Status** built and craftable - block, block entity, footprint, recipe, the three cast-stock patterns and
the size gate all ship; pinned by `LongCellTests.cs`   **Mod** iiex

**Owns**
- The long cell's block, block entity, footprint and recipe.
- Its drawn geometry - shell dimensions, interior volume, and the measured cavity of each of its four
  impression fillings.
- The mapping from its three lane counts (3 / 2 / 1) onto the settled cast-stock ladder.
- The size gate: a long-cell pattern is refused at the 1 × 1 cell and vice versa.

**Does not own - cited only**
- [casting cell](casting-cell.md) - the `mold` attribute schema, `MoldSize`, the state machine, the
  pattern catalogue, ram / imprint / shake-out, and the launder-face intake rule. The long cell is the
  same station at a different size; every rule there applies unless this page says otherwise.
- [casting bed](casting-bed.md) - the megablock idiom (filler footprint, `StructureAngle`, RCC stages).
- [molten network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/molten-network.md) - `BEBehaviorMoltenCell`, capacities, flow, the pull rate.
- [rolling mill](rolling-mill.md) - the consumer of everything the long cell casts.
- [multiblock & filler structures](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/multiblock.md) - the filler footprint system and the
  no-filler-graph-node rule.
- [density rule](../mechanics/density-rule.md) - 1 vx³ = 2.5 u and the settled cast-stock masses.
- [recoverability](../mechanics/recoverability.md) - the ≤ 32 / ≤ 48 handling invariant every cast stock
  length must satisfy.

---

## Role

The 1 × 1 [casting cell](casting-cell.md) tops out at a 12-voxel-long cavity. Cast slab, bloom and billet are
all longer than that, and so is the machine `castframe`. The long cell exists to cast them: it is the only
block in the suite that can pour a 24-voxel lane in one go.

It feeds the [rolling mill](rolling-mill.md), which takes cast stock on the steel/iron side and hammered
bloom on the wrought side.

Its second job is the lane ladder. One cell, three fillings, three products: three narrow lanes give billets,
two give blooms, one gives a slab - the whole cast-stock ladder from one block and a pattern swap, the same
trick the [casting cell](casting-cell.md) plays for capital goods.

---

## Structure

A 1 × 2 megablock: `BlockFilledMegastructure` with one filler at `(0, 0, 1)`
(`LongCellLayout.Footprint()`), the body extending −Z in model space while the shape spins by the side angle
alone (`ShapeSpunPerOrientation` carries no offset). `StructureAngle = AngleFromSide + 180`
(`BlockSandCastingLongCell.cs:97`) carries the half turn instead, reconciling the +Z footprint with the −Z
body - the same +180 convention as the [casting bed](casting-bed.md). The whole station is one casting: the
filler is for collision and interaction
only and hosts no molten cell of its own, which also keeps it clear of the no-filler-graph-node rule. Every
click on the filler is rerouted to the principal through `IFillerInteractionTarget`
(`BlockSandCastingLongCell.cs:121-151`), and the clicked cell is ignored - both cells do the same thing.

The impression is one pooled `BEBehaviorMoltenCell` on the principal (`drainFitting: true` - fed by a canal,
not a molten-graph node). Its pre-impression ceiling is `UnimpressedCapacity = 3400`
(`BlockSandCastingLongCell.cs:43`); an impressed pattern's own `capacity` overrides it.

Measured from `mods/iiex/assets/iiex/shapes/casting/sandcastinglongcell.json`:

| Part | Extent (16-space, principal-local) | Note |
|---|---|---|
| Floor | `y 0-2` across `z -16 … 16` | `Cube2` + `Cube7` |
| Side walls | `x 0-2` and `x 14-16`, `y 2-16`, full length | `Cube3`/`Cube4` + `Cube13`/`Cube14` |
| Launder-end wall | `z 14-16`, `y 2-14` | `Cube5` - 12 tall, the short wall the metal enters over |
| Far-end wall | `z -16 … -14`, `y 2-16` | `Cube6` - 14 tall |
| Launder spout | `[6,14,10] → [10,15,16]` | `Cube8`; identical geometry to the casting cell's and the pig bed's |

Interior: 12 (x) × 14 (y) × 28 (z). Rammed sand fills `y 2-14`, i.e. 12 × 12 × 28 = 4032 voxels
(`longcell-filling-base.json`); the half-sand legacy mesh fills `y 2-8`. The interior runs `z −14 … 14`
with 2-thick end dams, so a lane cannot exceed 24 voxels in length (`LongCellLayout.MaxLaneLength`).

---

## Assets

Seven files, all in `mods/iiex/assets/iiex/shapes/casting/`, currently untracked in git; their editable sources in
`workbench/shapes/` are marked deleted in the working tree.

| Runtime shape | Editable source | Draws |
|---|---|---|
| `sandcastinglongcell.json` | `molten-megablock-sandlongcell.json` | the brick shell |
| `longcell-filling-base.json` | `sandcasting-longcell-fillingbase.json` | flat rammed sand |
| `longcell-filling-half.json` | `sandcasting-longcell-fillinghalf.json` | legacy half sand |
| `longcell-filling-billets.json` | `sandcasting-longcell-fillingbillets.json` | 3 lanes |
| `longcell-filling-blooms.json` | `sandcasting-longcell-fillingblooms.json` | 2 lanes |
| `longcell-filling-castslab.json` | `sandcasting-longcell-fillingslab.json` | 1 lane |
| `longcell-filling-castframe.json` | `sandcasting-longcell-fillingframe.json` | 1 lane + an I-section web pad |

No textures or animations of their own - they carry the same `andesite` sand key as the
[casting cell](casting-cell.md)'s fillings, and the block declares it
(`BlockSandCastingLongCell.cs:77`).

Every element in every one of the seven is auto-named `Cube2`…`Cube14`. Nothing is semantically named, so
these shapes cannot be driven by `SelectiveElements` the way the [casting bed](casting-bed.md)'s is; the long
cell swaps whole shapes like the casting cell does, not elements.

---

## Numbers

### Settled masses (2026-08-04) — `Items/CastStockItemDefinitions.cs`

Billet 600 u, bloom 1000 u, slab 3000 u - carried on each pattern as `capacity` and on each
stock item as `materialUnits`. A multi-lane pattern's capacity is lanes × per-piece units
(`PatternItemDefinitions.cs`): `castbillets` 3 × 600 = 1800, `castblooms` 2 × 1000 = 2000,
`castslab` 3000.

The masses are declared; neither the item art nor the sand cavity is authoritative over them:

- the item shapes are settled art, already mapped for derived material and siding - sized for the forming
  line, not for mass arithmetic;
- the sand cavities are illustrative: they draw the rammed sand and the molten fill glow. A lane whose drawn
  volume does not multiply out to its capacity is not a bug.

So the [density rule](../mechanics/density-rule.md) sizes art plausibly. It is not an invariant between a
voxel count and a mass, and `CastMassParityTests` asserts nothing about voxels.

### Cavities — measured off the drawn fillings

Each filling is a sand block with walls and ribs raised out of it; the cavity is the void between them.

| Filling | Sand top | Lanes | Lane cavity (w × h × l) | Lane vx³ | Total vx³ |
|---|---|---|---|---|---|
| `castslab` | `y 10` | 1 | `8 × 4 × 24` | 768 | 768 |
| `blooms` | `y 10` | 2 (rib at `x 7-9`) | `3 × 4 × 24` each | 288 | 576 |
| `billets` | `y 11` | 3 (ribs at `x 5.5-6.5`, `x 9.5-10.5`) | `2.5 × 3 × 24` · `3 × 3 × 24` · `2.5 × 3 × 24` | 180 · 216 · 180 | 576 |
| `castframe` | `y 10` | 1, with a raised web pad `6 × 1 × 22` | `8 × 4 × 24` less 132 | 636 | 636 |

Every cavity runs `z -12 … 12` (24 long) and sits inboard of `x 4-12` (or `x 3-13` for `billets`, whose
walls are 1 voxel thick rather than 2). The renderer's fill boxes in `PatternItemDefinitions.Molds` match
these.

The billet lanes should be cut equal. They are `2.5 / 3 / 2.5` wide today, so one pour visually casts a
heavier centre billet than its neighbours; three of the same item should read as three identical lanes.
Recorded in-source at the `castbillets` entry; a redraw, not a mass change.

---

## Construction

Grid recipe `sandcastinglongcell` (`Recipes/Grid/CastingRecipeDefinitions.cs:130-152`): the 1 × 1 cell's
shell at double the length, so double the brick and double the clay - 12 bricks + 4 fire clay + hammer
+ chisel in a 3 × 3 grid. The cost is doubled through quantity, not through a taller pattern: the vanilla
grid is 3 × 3, so a 3 × 4 pattern would have passed every test and been uncraftable in the world. Two routes
as with the 1 × 1 cell - coloured brick capturing `{brick}`, and fire brick outputting the `fire` default.
Cost key `sandcastinglongcell-grid` (`IiexRecipeConfig.cs:94`).

---

## Operation

The [casting cell](casting-cell.md)'s loop unchanged - ram green sand, ram up a pattern, pour from the
launder face, shake out - with three differences:

1. **The size gate is enforced both ways.** The BE overrides `AcceptedSize => MoldSize.LongCell`
   (`BlockEntitySandCastingLongCell.cs:25`); a 1 × 1 pattern is refused with
   `iiex-longcell-wrongsize`, and the 1 × 1 cell refuses any non-`cell` pattern symmetrically.
2. **The impression spans two cells**, so the filler reroutes interaction to the principal
   (`IFillerInteractionTarget`), exactly as the [casting bed](casting-bed.md) does.
3. **One pool, not two.** A multi-lane pattern still yields more than one item, but out of a single
   charge: `capacity` is the whole impression, so a short pour fills lanes progressively and comes out as
   scrap exactly like any other under-filled cast.

---

## Drops

As the [casting cell](casting-cell.md): the block drops itself; metal standing in it follows the shared
molten-recovery rules.

---

## Code

| Piece | file:line |
|---|---|
| `BlockSandCastingLongCell : BlockFilledMegastructure, IFillerHost, IFillerInteractionTarget, IExBlockDefProvider` | `BlockStructures/Casting/Blocks/BlockSandCastingLongCell.cs:30`; def `:47-85`, `StructureAngle` `:97`, filler reroute `:121-151` |
| `BlockEntitySandCastingLongCell : BlockEntitySandCastingCell` | `BlockStructures/Casting/BlockEntities/BlockEntitySandCastingLongCell.cs:22` - changes `AcceptedSize` and the error code, nothing else |
| `LongCellLayout` (pure) | `BlockStructures/Casting/LongCellLayout.cs` - `CellCount` 2, `MaxLaneLength` 24, `Footprint()` |
| the three long patterns | `BlockStructures/Casting/PatternItemDefinitions.cs` - `castbillets` / `castblooms` / `castslab`, `size: "longcell"`, capacities from `CastStockItemDefinitions.Forms` |
| the stock masses | `Items/CastStockItemDefinitions.cs` - `BilletUnits` 600, `BloomUnits` 1000, `SlabUnits` 3000 |
| recipe | `Recipes/Grid/CastingRecipeDefinitions.cs:130-152`; cost key `IiexRecipeConfig.cs:94` |
| tests | `mods/iiex/tests/Blocks/Casting/LongCellTests.cs`, `MoldSpecTests.cs`, `CastMassParityTests.cs` |

---

## Gotchas

1. **The cavities live at negative Z.** The body extends −Z from the principal, so a cavity box's `z1` is
   `-12`. Every other cavity box in the mod is in `0…16`. Anything that assumes non-negative box
   coordinates (a naive renderer bound, a naive volume measure) will be wrong here.

2. **The launder-end wall is 12 tall, the far wall 14.** The asymmetry is where the metal comes in, and it
   means the shell is not mirror-symmetric, so the +180 orientation convention matters more here than it
   does for a symmetric block.

3. **The billet lanes are unequal** (2.5 / 3 / 2.5 voxels wide) and two of the three widths are
   fractional. Three castings out of one pour must read as identical or the item cannot be one item.
   Visual only - the masses come from `capacity`.

4. **The shape spins by the side angle alone; `StructureAngle` carries the +180** on top of it
   (`BlockSandCastingLongCell.cs:92-97`). Spinning the shape by the same +180 too would turn the body back
   onto the footprint's declared cell instead of the one opposite it, which is where the body is drawn.

---

## Open

- **Whether `castframe` stays.** The filling is drawn (`longcell-filling-castframe.json`) but no
  `castframe` pattern exists in either mod - it is an iiex machine part, and iiex owning a pattern for
  another mod's product is the coupling the spec-on-the-pattern design exists to avoid
  (`PatternItemDefinitions.cs`, the `LongCellMolds` note). If the frame casting lands in iiex, it needs a
  mass; if it does not, the shape is an orphan.
- **Cut the billet lanes equal** (Gotcha 3) next time the fillings are redrawn.
- **Recoverability.** A 24-voxel cast piece is inside the ≤ 32 lengthwise escape, but the forming line
  grows a piece every pass. Whether the long cell's output is already at a crop point is a
  [recoverability](../mechanics/recoverability.md) question that has to be answered before any new, longer
  stock form is added.
