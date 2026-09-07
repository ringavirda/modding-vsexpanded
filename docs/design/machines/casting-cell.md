# Sand Casting Cell
**Status** live   **Mod** iiex

**Owns**
- The 1 × 1 cell block: its variants, shape, textures, recipe, drops and interior geometry.
- The cell's state machine - `SandLevel`, `CellAction`, the `Decide` precedence table, and the
  shake-out-returns-to-`Full` rule.
- The pattern-carries-the-mold-spec contract: the `mold` attribute schema, every field's meaning and
  validation rule, and where it is read.
- The shipped pattern catalogue for this station (the five `cell`-size types × 12 woods), each pattern's
  cavity box, capacity, filling shape, output and minimum pour temperature, plus wooden-pattern durability.
- The cast-quality rules: misrun, short pour, and what each yields.
- The launder-face intake rule (one face, not all four).
- Green sand as the cell's one accepted molding material.

**Does not own - cited only**
- [molten network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/molten-network.md) - `BEBehaviorMoltenCell`, `SetCapacity`/`ClearCapacity`,
  the hard-coded `PullRatePerTick = 25`, `IsHardened`/`Solidified`, `MoltenChisel.BuildRecovery`, and the
  throughput question.
- [density rule](../mechanics/density-rule.md) - 1 vx³ = 2.5 u and the check of these cavities' capacities
  against it.
- [molten canal](molten-canal.md) - the run that feeds the launder.
- [casting bed](casting-bed.md) - the bulk station this one is not.
- [long cell](long-cell.md) - the 1 × 2 sibling the `MoldSize` enum already names and nothing implements.
- [recipes & config](../mechanics/recipes-config.md) - code-first defs and the goldens harness.
- [recoverability](../mechanics/recoverability.md) - the ≤ 32 / ≤ 48 handling invariant the cast stock
  ladder must satisfy.

---

## Role

The cell casts capital goods: iron molds, heavy plate, barrel blanks - parts a player makes a handful of,
not a stream of. Everything about it is priced for that.

Cast iron is not forgeable: the anvil route for a pig only shatters it into denominations
(`ItemPig.cs:86-141`). That un-forgeability is load-bearing - if cast iron could be forged, the puddling
furnace would have no reason to exist - and without a casting station cast iron is a dead end that only
remelts. The cell is the route out.

Its second job is bootstrapping the mold tier: the cell's first products are the iron molds that replace the
fired-clay ones, which is what makes iron and steel castable at all. Clay caps out at bronze (see
[molten canal § the clay heat gate](molten-canal.md)).

The architectural decision: the pattern item is the spec. The cell reads what to cast off the held stack and
never names a mod, a shape or a product in code, so another mod adds a castable part with a pattern
definition alone.

---

## Structure

A single block. No footprint, no fillers, no multiblock.

Interior, from `mods/iiex/assets/iiex/shapes/casting/sandcastingcell.json`: floor `y 0-2`, side walls at `x 0-2` and
`x 14-16` running `y 2-16`, a back wall at `z 0-2` running `y 2-16`, and the launder wall at `z 14-16`
only `y 2-14` tall - so the interior is 12 × 14 × 12 with one short wall. The rammed sand fills
`(2,2,2)-(14,14,14)` = 12³ = 1728 voxels (`cell-filling-base.json`). The launder spout itself is the
element at `[6,14,10]→[10,15,16]`.

The cell hosts one `BEBehaviorMoltenCell` declared `capacity 200, drainFitting true`
(`BlockSandCastingCell.cs:37-40`). It is not a molten-network node, it is a puller: joined to the graph it
would fill to the canal's level and then drain back out, because the network's edge rule has no "drain
fittings never give back" clause. So it drains its feed itself, restricted to the launder face so the model
does not lie about where metal enters (`BlockEntitySandCastingCell.cs:155-174`).

---

## Assets

| Asset | Path | State |
|---|---|---|
| Block shape | `mods/iiex/assets/iiex/shapes/casting/sandcastingcell.json` | tracked |
| Editable source | `workbench/shapes/molten-block-sandcell.json` | present |
| Flat sand | `mods/iiex/assets/iiex/shapes/casting/cell-filling-base.json` (`iiex:casting/cell-filling-base`) | tracked · `CastingCellLogic.cs:128` |
| Legacy half sand | `…/cell-filling-half.json` | tracked · `CastingCellLogic.cs:132` |
| Impression - heavy plate | `…/cell-filling-heavyplate.json` | tracked |
| Impression - plate mold | `…/cell-filling-plate.json` | tracked |
| Impression - ingot mold | `…/cell-filling-ingotmold.json` | untracked, exported 2026-08-05. `cell-filling-plate.json` and `-doubleingot.json` deleted with the molds they impressed |
| Impression - barrel | `…/cell-filling-moltenbarrel.json` | tracked |
| Impression - castshell, flywheelpart | `…/cell-filling-castshell.json`, `…-flywheelpart.json` | drawn, untracked in git - referenced by `iiex:pattern-castshell-*` and `iiex:pattern-castwheelsection-*` (the wheel-section filling keeps the older `flywheelpart` file name) |
| Impression - axle, cylinder, gearblanklarge, gearblanksmall | `…/cell-filling-*.json` | drawn, untracked in git, and no pattern references them - orphans; the full art census is [patterns](../items/patterns.md)'s |
| Sand texture | `game:block/stone/sand/basalt` via the `andesite` key | `BlockSandCastingCell.cs:58`, `GreenSandItemDefinitions.cs:38` |
| Brick texture | running-bond base + `{brick}1` tint overlay | `BlockSandCastingCell.cs:52-56` |
| Animation | none | — |

The filling shapes' only texture key is `andesite`, a name from when the cell accepted any sand and remapped
the key per block entity. With one prepared molding sand there is nothing to remap, so the block declares
that key as the green-sand texture and the shapes resolve straight through it
(`BlockSandCastingCell.cs:46-58`, `BlockEntitySandCastingCell.cs:366-371`). The key is harmless but wrong;
renaming it belongs to a shape pass.

Pattern art follows one rule: a pattern wears the cast item's own shape with the texture swapped to plain
wood, so it reads as a wooden positive of the part (`PatternItemDefinitions.cs:55-67, 155-168`).
`shapeByType` picks the part's shape; `TextureAll("game:block/wood/debarked/{wood}")` overrides whatever
that shape declares. A type with no entry falls back to `game:item/plate`.

> Several item shapes `PatternShapes` and `CastPartItemDefinitions` reference are untracked in git - among
> them `mods/iiex/assets/iiex/shapes/item/heavyplate.json` (drawn 12 × 2 × 12). The shapes exist; they are simply not
> committed.

---

## Construction

Two grid recipes sharing one pattern - a coloured route that captures the brick colour, and a fire-brick
route producing the `fire` default. They cannot conflict because the coloured brick course and the fire
bricks do not overlap (`CastingRecipeDefinitions.cs:50-73`).

```
B H B        B = game:brickcourse-four-running-*  (captures {brick})   | game:claybricks-good-fire
B F B        F = game:clay-fire  ×2                                     H = hammer (tool)
B K B        K = chisel (tool)                     →  iiex:sandcastingcell-{brick}-north  ×1
```

Six bricks, fire clay, hammer and chisel - the `Fhk` trio shared with the
[molten canal](molten-canal.md) crafts (`RecipeIngredients.cs:66-80`).

Green sand is a separate prepared material, mixed 8 sand + 1 blue clay → 8 green sand
(`CastingRecipeDefinitions.cs:38-48`). The `game:sand-*` wildcard is uncaptured: every rock type mixes and
none survives into the output, so there is no per-rock lookup table.

Patterns are carved from a reusable diagram + a knife + 2 planks, one recipe per type, derived from
`PatternItemDefinitions.PatternTypes` so adding a castable part carries its craft automatically
(`PatternRecipeDefinitions.cs:20-41`). The plank's wood is captured into the pattern variant.

> The diagrams themselves are creative-only - the design table cannot draft them yet. So the pattern line is
> gated one level up from where it looks gated.

---

## Operation

```
place cell → RMB green sand (ONCE) → RMB pattern (ram up) → pour from the launder
           → wait for it to freeze → RMB empty hand (shake out)
           → part (or scrap) ; the cell rakes back to FULL sand
           → RMB pattern → …
```

Two clicks and a wait per casting. Sand is rammed once and never taken away again; what the loop costs is
the labour of ramming up (`CastingCellLogic.cs:87-98`).

### Interaction routing

`CastingCellLogic.Decide` resolves one right-click into exactly one action, in this precedence
(`CastingCellLogic.cs:59-85`):

| Cell holds metal? | Held | Result | Applied at |
|---|---|---|---|
| yes, hardened | empty hand | `Harvest` | `BlockEntitySandCastingCell.cs:260-282` |
| yes, not hardened | empty hand | `TooHot` → `iiex-castingcell-toohot` | `:209-212` |
| yes | anything else | `None` - no re-ramming, no re-patterning while metal is present | `CastingCellLogic.cs:71-76` |
| no | green sand, `sand != Full` | `RamSand` → `Full`, consumes 1 | `:218-230` |
| no | `pattern-*`, `Full`, no impression | `Imprint` → set spec + capacity, damage the pattern | `:232-258` |
| no | anything else | `None`, falls through to the block's default | — |

A pattern is recognised by `FirstCodePart() == "pattern"` (`:188`) - domain-blind, which is the cross-mod
contract. The spec is read off the held stack and the full item code is persisted (`:237-248`), because a
pattern is `pattern-{type}-{wood}` and there is no `pattern-{type}` item to look up.

### Intake

Each server tick, if `CanIntake` (impression present, cavity not full, not solidified -
`CastingCellLogic.cs:124-125`), the cell drains the external `IMoltenCell` on its launder face only
(`:155-174`). `LaunderFace` is the face the spout is drawn on: the block's facing, turned around
(`ExOrientation.FacingFromSide(Variant["side"]).Opposite`, `:192-193`).

### Shake-out

| Condition | Yield | file:line |
|---|---|---|
| cavity full and not a misrun | `spec.Output` resolved against the world, `StackSize = max(1, Quantity)` | `:293-301` |
| cavity full but poured below `minPourTemp` - a misrun | recovered scrap of the metal actually in the cavity | `CastingCellLogic.cs:116-117`, `:304-311` |
| cavity under-filled - a short pour | same recovered scrap | `:267`, `:304-311` |

Then: contents cleared, pattern capacity dropped, pattern code cleared, sand back to `Full`
(`:274-278`). The impression is destroyed; the sand is not.

---

## Numbers

### Block — `BlockSandCastingCell.cs`

| key | value | file:line | what it does |
|---|---|---|---|
| hosted cell | `capacity 200`, `drainFitting true` | `:37-40` | the fallback capacity before a pattern is rammed |
| variants | `brick` (fire + 7 colours) × `side` (4) | `:44-45` | code is `sandcastingcell-{brick}-{side}` |
| shape | `iiex:casting/sandcastingcell`, spun per orientation | `:46` | north 0 · east 270 · south 180 · west 90 (`ExBlockDef.cs:200-209`) |
| max stack / resistance / mining tier | `16` / `3.5` / `0` | `:31-33` | |
| selection / collision | `0..1` / `0..0.875` | `:61-62` | |

### Block entity — `BlockEntitySandCastingCell.cs`

| key | value | file:line | what it does |
|---|---|---|---|
| `PullRatePerTick` | *(see [molten network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/molten-network.md) § Hard-coded)* | `:33` | hard-coded here; the value and the throughput conflict are that page's |
| server / client tick | `1000 ms` | `:109`, `:113` | pull+cool / refresh the surface |
| save keys | `cc_sand` (int), `cc_pattern` (string), `cc_filltemp` (float, only once the cavity has filled) | `:443-445` | `cc_sandcode` is no longer read (`:453-454`); a cell saved without `cc_filltemp` shakes out as a clean cast |

### The mold spec — `MoldSpec.cs`

The pattern's `mold` attribute (`AttributeKey = "mold"`, `:42`). Every field is validated at load:

| Field | Type | Rule | file:line |
|---|---|---|---|
| `size` | `"cell"` \| `"longcell"` | defaults to `"cell"`; anything else is an error | `:60-71` |
| `shape` | string | non-blank; the rammed-sand mesh shown while the impression is present | `:73-78` |
| `capacity` | int | > 0; units the impression holds | `:80-85` |
| `cavity` | `Cuboidf[]` | ≥ 1 box, each with `x1<x2, y1<y2, z1<z2`; the molten-renderer fill region, not a mass source | `:87-103` |
| `output` | `JsonItemStack` | must carry a `code` | `:105-110` |
| `minPourTemp` | float | defaults `0` = check disabled | `:112` |

Validation runs once at `AssetsFinalize`, server-side, and logs one error per malformed pattern
(`PatternValidation.cs:19-31`, called from `IronIndustryExpandedModSystem.cs:113-116`).

### The shipped pattern catalogue — `PatternItemDefinitions.cs`

Eight types × twelve woods = 96 pattern items; the three `longcell` stock types are the
[long cell](long-cell.md)'s, the five `cell`-size types below are this station's. The spec is per type, not
per wood. Default `minPourTemp` is 1150 °C (the `Mold(...)` default, `PatternItemDefinitions.cs:45`).

| type | capacity (u) | cavity render box | filling shape | output | file:line |
|---|---|---|---|---|---|
| `castheavyplate` | `CastPartItemDefinitions.HeavyPlateUnits` = 160 | `(7,4,4)-(9,14,12)` | `iiex:casting/cell-filling-heavyplate` | item `iiex:castplate-heavy` | `:108-113` |
| `castingotmold` | 152 | `(3,12,3)-(13,14,13)` | `…/cell-filling-ingotmold` | block `iiex:casting-mold-ingot` | `:120-126` |
| `castbarrel` | `CastBarrelUnits` = 200 | `(4,4,4)-(12,12,12)` | `…/cell-filling-moltenbarrel` | item `iiex:cast-barrel` | `:130-135` |
| `castshell` | `CastShellUnits` = 600 | `(4,4,4)-(12,14,12)` | `…/cell-filling-castshell` | item `iiex:castshell` | `:148-153` |
| `castwheelsection` | `CastWheelSectionUnits` = 600 | `(4,10,4)-(12,14,12)` | `…/cell-filling-flywheelpart` | item `iiex:castwheelsection` | `:154-161` |

| key | value | file:line |
|---|---|---|
| `WoodenPatternDurability` | 24 impressions | `:241` |
| `PatternWoods` | 12 (birch … purpleheart) | `:253` |
| pattern max stack | `1` | — |

Cavity boxes are the fill glow; the true cavity is the shape. Measured against the shipped filling shapes,
`cell-filling-heavyplate` really is `2 × 10 × 8 = 160` vx³ (the render box matches exactly) - at an implicit
1 u/vx³, which is what [density rule](../mechanics/density-rule.md) flags: the rule says 2.5.

---

## Drops

| Path | Result | file:line |
|---|---|---|
| Break the cell | the block itself (default `Block.GetDrops`; nothing is overridden) | `BlockSandCastingCell.cs` - no override |
| Metal standing in the cell | nothing. No `WouldSpillOnRemoval`, no recovery drop, no spill sound | — |
| Rammed sand | nothing. Sand is a one-off cost, never returned (`AfterShakeOut = Full`) | `CastingCellLogic.cs:98` |
| Impressed pattern | not stored - a pattern is damaged and returned to the player at ram-up, never held by the cell | `BlockEntitySandCastingCell.cs:251-253` |

---

## Code

| Type / member | file:line | Role |
|---|---|---|
| `CastingCellLogic` | `CastingCellLogic.cs:44` | pure; every rule that can be pinned without a world |
| `…Decide` | `:59` | the one interaction resolver |
| `…AfterShakeOut` | `:98` | `SandLevel.Full` - the "sand is not consumed" rule as a constant |
| `…IsMoldingSand` | `:109` | full-code match on `iiex:greensand`, so another mod's `greensand` cannot satisfy it |
| `…IsMisrun` | `:116` | `cavityFull && minPourTemp > 0 && pourTemp < minPourTemp`, where `pourTemp` is the metal's temperature in the tick the cavity filled (`PullFromLaunder` keeps it), never the shake-out temperature; an unrecorded pour is not a misrun |
| `…CanIntake` | `:124` | the pull gate |
| `…FillingShape` | `:142` | state → mesh; the fallback to flat sand when an impression's shape is unresolved |
| `SandLevel` / `CellAction` | `:3` / `:21` | `Half` is legacy - nothing produces it, saved cells clear on the next ram |
| `MoldSpec` | `MoldSpec.cs:32` | the extension point. A record + `TryParse` |
| `PatternValidation.Validate` | `PatternValidation.cs:19` | pure over a collectible sequence |
| `PatternItemDefinitions` | `PatternItemDefinitions.cs:18` | the catalogue; `Molds` (`:71`) is the single source `PatternTypes` (`:115`), the diagrams and the craft all derive from |
| `BlockSandCastingCell` | `BlockSandCastingCell.cs:18` | plain `Block`; routes every RMB to the entity |
| `BlockEntitySandCastingCell` | `BlockEntitySandCastingCell.cs:31` | plain `BlockEntity`; hosts the molten cell as a behaviour |
| `…ResolveSpec` / `SpecOf` | `:69` / `:77` | the one place a pattern's spec is read |
| `…PullFromLaunder` | `:155` | launder-face-only intake |
| `…OnTesselation` | `:372` | draws the filling shape over the default shell mesh |
| Tests | `test/…/Casting/CastingCellLogicTests.cs`, `MoldSpecTests.cs`, `PatternValidationTests.cs`, `PatternCodeLayoutTests.cs` | state machine, schema, validation, the `pattern-{type}-{wood}` code layout |

Where a caller hooks in: to add a castable part from any mod, ship a `pattern` item variant carrying a
`mold` attribute, a filling shape in your own domain, and the output item. Nothing in iiex changes and
nothing needs to reference your mod. To feed a cell: end a [molten canal](molten-canal.md) on the block's
`side` face.

---

## Gotchas

1. `MoldSize` is parsed and never consulted. `MoldSpec.Size` is set (`MoldSpec.cs:60-71`) and read nowhere
   outside tests - a grep for `MoldSize` in `src/` returns only `MoldSpec.cs`. So a `longcell` pattern rams
   into the 1 × 1 cell and casts there. There is nothing to fix it against yet because the
   [long cell](long-cell.md) does not exist.

2. Resolved: the launder face is the drawn launder's face, not the block's raw facing. The shape draws the
   launder on the +Z wall (`[6,14,10]→[10,15,16]`), and the `-north` variant is authored at `rotateY 0`
   (`BlockSandCastingCell.cs:46`, `ExBlockDef.cs:200-209`), so `north` needs `LaunderFace` to read south, not
   north. `LaunderFace` turns the raw facing around (`BlockEntitySandCastingCell.cs:192-193`), guarded at
   every side for both cells by `CastingCellFootprintGuards`.

3. The declared `capacity 200` is a decoy. It only applies before a pattern is rammed (and after shake-out,
   via `ClearCapacity`). Every real cast runs at the pattern's own capacity - 152 / 160 / 200 / 600. The
   `200` matching `castbarrel` exactly is coincidence.

4. `cavity` is the glow, not the mass. `MoldSpec.Cavity` feeds `MoltenRenderer` only
   (`BlockEntitySandCastingCell.cs:333-341`); `capacity` is what fills. A render box larger than the
   capacity's voxel volume reads as a partially full surface at a complete cast.

5. `Harvest` silently does nothing if the spec cannot be resolved. `if (Spec is not { } spec) return false`
   (`:265-266`) - a cast made with a pattern from a mod that has since been removed is unrecoverable: the
   metal stays, and `Decide` will keep returning `Harvest` forever.

6. The renderer rotates by `Block.Shape.rotateY`, the sand mesh by `ExMesh.RotateByShape` (`:340`, `:388`).
   Two different rotation paths for two meshes that must land on top of each other.

7. `GreenSandItemDefinitions`' doc comment is stale in two ways (`GreenSandItemDefinitions.cs:22-24`): it
   says "shake-out returns it" - it does not - and it `<see cref>`s `CastingCellLogic.SandIsReturned`, a
   member that does not exist (it is `AfterShakeOut`).

8. `SandLevel.Half` is legacy and must not be produced. It exists only so pre-rework saves load; one ram
   clears it (`CastingCellLogic.cs:9-17`, `:98`).

9. `holdingPattern` matches any item whose first code part is `pattern` (`:188`), from any domain, before
   the spec is read. A patternless `pattern-*` item gets `iiex-castingcell-badpattern` rather than falling
   through (`:240-244`) - deliberate, but it means the cell claims the click.

10. Drawn impression shapes without a pattern. `cell-filling-{axle,cylinder,gearblanklarge,
    gearblanksmall}.json` exist in `mods/iiex/assets/iiex/shapes/casting/` and are untracked, and no code gives them
    a pattern or a capacity. The full art census is [patterns](../items/patterns.md)'s.

11. Pattern durability is charged at ram-up, not at shake-out (`:251-253`), so a pattern is worn by 24
    impressions whether or not they ever fill.

---

## Open

- `MoldSize` is unenforced (Gotcha 1). It becomes a real gate the moment the [long cell](long-cell.md)
  exists; until then it is a parsed field with no consumer.
- Capacity is hand-written, not derived. Deriving capacity from the shape at load - so the model is the spec
  and a test can pin it - is open at [patterns § Open](../items/patterns.md). Where a capacity does match
  its art voxel-for-voxel it does so at 1 u/vx³, not the settled 2.5
  (see [density rule](../mechanics/density-rule.md)).
- The heavy plate's mass and identity are moving. Settled: `castplate` = 10 × 2 × 10 = 500 u and
  `heavyplate` = 12 × 2 × 10 = 600 u as a separate, rolled item. Code has one item at 160 u whose art is
  drawn 12 × 2 × 12. When that splits, this cell keeps the cast half and the rolled half leaves.
- No gate / riser head allowance. Whether capacity is exactly the cavity or cavity + ~10 % with the surplus
  returning as gate scrap is left open ([casting](../processes/casting.md)). Today it is exactly the cavity
  and fettling scrap does not exist.
- No separate core item. The cylinder pattern is meant to teach why a cored mold is harder than a flat one;
  it has neither a pattern nor a core ([casting](../processes/casting.md), [patterns](../items/patterns.md)).
- Metal-pattern tier. Wooden patterns wear out at 24; a metal tier for long runs is designed and unbuilt
  (`PatternItemDefinitions.cs:106-108`).
- Spill on break. Like the [casting bed](casting-bed.md), a cell broken mid-cast voids its charge.
- iiex ships nothing. The whole cross-mod half of the contract - cylinder, frame casting, flywheel segments,
  gear blanks - is still aspirational. The contract itself works; nobody is on the other end of it.
