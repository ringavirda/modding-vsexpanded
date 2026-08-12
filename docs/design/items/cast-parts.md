# Cast parts

**Status** partial - five items live (`castplate-heavy`, `cast-barrel`, `castshell`, `castwheelsection`,
`bevelgear`); the rest of the family is art-only or absent   **Mod** iwex (`IronworkingExpanded`)

**Owns**
* the cast-part catalogue: which cast-iron parts exist as items, which exist as art only, which exist only in
  prose - and for each one, its shipped mass constant, its drawn geometry, and whether the two agree;
* the `castplate` / `heavyplate` split as it lands on this family - which half stays cast and what the
  shipped `castplate-heavy` actually is today (one item wearing both names);
* the fabricated-substitute mapping - for every cast part, its rolled/fabricated steel equivalent and what
  that equivalent is built from - and the fact that `cast-barrel` is the one shipped instance;
* the remelt hole: that no live cast part can be melted back by any code path, despite the constants being
  documented as existing for exactly that;
* the family's art inventory - editable sources, runtime exports, the rename that only half happened, and
  the orphans.

**Does not own - cited only, never restated**

| Fact | Owner |
|---|---|
| 1 vx³ = 2.5 u, the audit of every shipped mass, and the four different implicit cavity densities | [density rule](../mechanics/density-rule.md) |
| the cell, the `mold` attribute schema, ram-up / pour / shake-out, the shipped pattern catalogue, misrun & short pour | [casting cell](../machines/casting-cell.md) |
| the 1 × 2 station, its seven drawn fillings and the `castframe` cavity measurement | [long cell](../machines/long-cell.md) |
| the molten pool, `BEBehaviorMoltenCell`, `MoltenChisel.BuildRecovery` | [molten network](../mechanics/molten-network.md) |
| what the cupola remelts and what may be charged | [cupola](../machines/cupola.md) · [burden](burden.md) |
| bending, and the machine that makes shells, barrels and rims | [bending roller](../machines/bending-roller.md) |
| the D2 / N3 decision itself (cast ↔ fabricated substitution) | [STATE.md](../../internal/plans/STATE.md) |
| the bevel gear as a shafting component; the `gear` / `largegear` items | [flywheel & shafting](../machines/flywheel-and-shafting.md) · [gears](../machines/gears.md) |
| boring, turning, gear-cutting - everything that finishes a cast blank | [boring machine](../machines/boring-machine.md) |
| rolled masses (`beam`, `boilerplate`, rolled plate) | [rolled parts](rolled-parts.md) |
| rivets, bolts, nails and the rod they come from | [fasteners](fasteners.md) |
| the ≤ 32 / ≤ 48 handling invariant | [recoverability](../mechanics/recoverability.md) |
| code-first defs, the cost catalogue, goldens | [recipes & config](../mechanics/recipes-config.md) |

**Depends on** [density rule](../mechanics/density-rule.md) · [casting cell](../machines/casting-cell.md) ·
[long cell](../machines/long-cell.md) · [cupola](../machines/cupola.md) ·
[bending roller](../machines/bending-roller.md) · [fasteners](fasteners.md) ·
[rolled parts](rolled-parts.md) · [STATE.md](../../internal/plans/STATE.md)

---

## Role

Cast iron is not forgeable - the anvil route for a pig only shatters it into denominations
([casting cell § Role](../machines/casting-cell.md)). Every cast-iron part in the suite therefore arrives by
pouring metal into sand. This family is that output: the plates, frames, shells, wheel sections, barrels and
blanks a 19th-century machine shop got from the foundry rather than the smith.

Its second job is to be replaced. Cast iron is strong in compression and weak in tension; the fabricated
answer is riveted plate steel, and the two are a player choice rather than a tech-tree step - cast with a
cupola, fabricate with a mill ([STATE.md § D2](../../internal/plans/STATE.md)). That is what gives cheap Bessemer
steel its main job, and what turns `beam`, `plate` and `rivet` into the ingredients of every machine frame
in the game.

---

## The catalogue

"vx³" is the drawn solid volume where the art is an axis-aligned box; hollow and toothed art is marked, and
[density rule § Measuring a shape](../mechanics/density-rule.md) owns why those cannot be summed naively.

### Live — an item exists

| Item | Section × length | vx³ | Mass (u) | Made by | Consumed by |
|---|---|---|---|---|---|
| `iwex:castplate-heavy` | drawn 12 × 2 × 12 | 288 | 160 (`CastPartItemDefinitions.HeavyPlateUnits`) | casting cell, `pattern-castheavyplate-*` | flywheel web ×4 (`EnergyRecipeDefinitions`) · rolling-mill housing ×7 (`FormingRecipeDefinitions`) |
| `iwex:cast-barrel` | cored vessel, 4 wall slabs + base (hollow) | 603 naive, meaningless | 200 (`CastBarrelUnits`) | casting cell, `pattern-castbarrel-*` | `moltenbarrel-cast` grid craft (`MoltenRecipeDefinitions.cs:40-47`) |
| `iwex:castshell` | three bent panels, 72 vx³ apiece | 216 naive | 600 (`CastShellUnits`, `CastPartItemDefinitions.cs:79`) | casting cell, `pattern-castshell-*` | nothing yet in code - the [ladle](../machines/ladle.md)'s cast variant (designed), then lpex's water tank, ore crusher, engine housings |
| `iwex:castwheelsection` | three rim segments, 72 vx³ apiece | 216 naive | 600 (`CastWheelSectionUnits`, `:65`) | casting cell, `pattern-castwheelsection-*` | flywheel rim ×4, large flywheel rim ×8 (`EnergyRecipeDefinitions`) |
| `iwex:bevelgear` | toothed disc (hollow/toothed) | 488 naive, meaningless | 40 (`BevelGearItemDefinitions.cs:12`) | no recipe at all | `BlockCastIronBevel.GearItemCode` (`BlockCastIronBevel.cs:26`) |

Settled 2026-08-05: `castshell` and `castwheelsection` are both iwex's. The [ladle](../machines/ladle.md) is
ruled to iwex and its cast variant is built from shell segments (ladle.md § the 2026-08-05 ruling;
`CastPartItemDefinitions.cs:19-28` records the same). iwex owns it, lpex consumes it (water tank, ore
crusher, engine housings): the pattern and the part live in the same mod, so no cross-mod pattern
indirection is needed.

The structural pair are 600 u, not the 540 the density rule computes. The rule sizes art plausibly; the mass
is declared so the economy divides ([cast stock](../mechanics/density-rule.md), and
`CastStockItemDefinitions`' header states the same discipline). N3 settles that a cast structural part and
its rolled/riveted equivalent are alternatives, not tiers - and [bending](../processes/bending.md) already
pins both fabricated halves at 600 u (a `boilerplate` 15 × 1 × 16 bent into a shell; a `heavyplate`
12 × 2 × 10 bent into a rim). At 540 the cast route would be 10% cheaper in metal and the choice would stop
being a choice. At 600 the two cost the same iron and differ only in the plant they demand.
`CastMassParityTests` pins it, along with the general rule that a pattern's `capacity` equals its lane count
times the cast item's `materialUnits`.

One cast block also comes out of the cell and belongs to [casting cell](../machines/casting-cell.md), not
here: `iwex:casting-mold-ingot` (`BlockCastMold.cs`). Since 2026-08-05 that is the only cast mold: the plate
mold was retired (the mill rolls plate) and the surviving tray became single-bay - 100 units, one ingot -
because crucible steel has to be poured into something.

### Art-only — a shape is drawn, no item exists

| Part | Drawn as | vx³ | Station | Intended consumer |
|---|---|---|---|---|
| `castframe` | `item-castframe.json` - two 8 × 4 × 14 halves + top rails | 964 naive | [long cell](../machines/long-cell.md) (cavity 636, owned there) | engine bed · boring-machine and steam-hammer standards · mill housings |
| cylinder blank | `item-cylinder-castblank.json` - 4 walls, 12 tall | 768 naive (cored) | casting cell | → bored cylinder |
| bored cylinder | `item-cilinder-bored.json` | 425 naive (cored) | [boring machine](../machines/boring-machine.md) | Watt engine · pumps · steam hammer |
| cast pipe segment | `item-cylinder-pipesegment.json` | 336 naive (cored) | boring machine | [cast pipes](../machines/cast-pipes.md) |
| gear blank large / small | `item-gear-castblanklarge.json` / `…small.json` | — | casting cell | → cut gears at the boring machine |
| axle | `cell-filling-axle.json` only | — | casting cell | cast-iron shafting |

### Settled but not drawn and not built

| Part | Settled geometry | vx³ | Settled mass | Note |
|---|---|---|---|---|
| `castplate` (cast) | 10 × 2 × 10 | 200 | 500 u | the cast half of the split; no shape is drawn at these dimensions. Mass and geometry re-affirmed 2026-08-07 (D2); the code constant (160) and the shapes follow in a queued batch - [economy landing](economy-landing.md) |
| `heavyplate` (rolled) | 12 × 2 × 10 | 240 | 600 u | a separate, rolled item - leaves this family entirely, see [rolled parts](rolled-parts.md) |

---

## Fabricated substitutes

The rule: every cast-iron structural part gets a rolled/fabricated steel equivalent, reached through an RCC
dual path (wildcard/OR ingredient) so the two routes are alternatives rather than tiers. The decision is
[STATE.md § D2 / § N3](../../internal/plans/STATE.md)'s; the bending verb and the machine are
[bending roller](../machines/bending-roller.md)'s. This page owns the per-part mapping.

| Cast part | Fabricated equivalent is built from | Operation | Where | Shipped? |
|---|---|---|---|---|
| `castplate` | rolled plate | — (the mill already makes it) | [rolling mill](../machines/rolling-mill.md) | — |
| `castframe` | beam × N + plate + rivets | assemble | a grid recipe / RCC stage - a riveted plate girder | no |
| `castshell` | plate + rivets | bend + rivet the seam | [bending roller](../machines/bending-roller.md) | the cast half ships; the fabricated half does not |
| `cast-barrel` | plate + rivets | bend + rivet | bending roller | yes, in a cruder form - see below |
| `castwheelsection` | bent rim + bar spokes + hub + rivets | bend + assemble | bending roller + a recipe | the cast half ships; the fabricated half does not |
| cylinder blank · gear blanks · axle · `bevelgear` | — | — | — | no substitute by design: these are machined parts, not structural ones. N3 does not list them |

Rivets are the ingredient, so there is no riveting machine. The joining is abstracted into the recipe; a
fabricated frame costs rivets ([STATE.md § N3](../../internal/plans/STATE.md)). Rivets do not exist yet - see
[fasteners](fasteners.md) - so not one substitute in the table is buildable today.

### `moltenbarrel` is the pattern, already live

The molten barrel ships both routes as two `construction` variants of one blocktype
(`BlockMoltenBarrel.cs:41-46`, `:54`):

| Variant | Craft | file:line |
|---|---|---|
| `moltenbarrel-plated` | 6 × `game:metalplate-*` + 4 × fire clay + 4 × nails + hammer, 3 × 3 | `MoltenRecipeDefinitions.cs:28-37` |
| `moltenbarrel-cast` | 1 × `iwex:cast-barrel` + 4 × fire clay, 2 × 1 | `MoltenRecipeDefinitions.cs:40-47` |

Two things are off the settled shape and are the work left to do: the fabricated route costs nails, not
rivets, and it is a flat grid craft rather than a bend on the roller. The structure - one block, two
constructions, identical behaviour - is correct and should be copied for the rest of the table.

---

## Numbers

### Masses as shipped

| Item | Constant | file:line | Drawn | Rule says | Verdict |
|---|---|---|---|---|---|
| `castplate-heavy` | `HeavyPlateUnits = 160` | `src/IronworkingExpanded/Items/CastPartItemDefinitions.cs:21` | 12 × 2 × 12 = 288 vx³ | 720 as drawn; 500 as settled at 10 × 2 × 10 | stale, and the art is wrong too |
| `cast-barrel` | `CastBarrelUnits = 200` | `…/CastPartItemDefinitions.cs:24` | hollow | not naively derivable | needs a solid-volume measure |
| `castshell` / `castwheelsection` | `CastShellUnits` / `CastWheelSectionUnits`, both 600 | `:79` / `:65` | 216 naive each | declared for parity, not derived | pinned by `CastMassParityTests` |
| `bevelgear` | `GearUnits = 40` | `src/IronworkingExpanded/Items/BevelGearItemDefinitions.cs:12` | toothed | not naively derivable | needs a solid-volume measure |

The full audit of these against 1 vx³ = 2.5 u - including which are stale and why - is
[density rule § Audit](../mechanics/density-rule.md)'s, not this page's.

### The cavity capacities that produced them

Owned by [density rule § Where 160 came from](../mechanics/density-rule.md); listed here only so the
cast-parts reader knows which of these items each defect belongs to. Do not re-derive it on this page.

| Pattern | Capacity | Render box | Implied u/vx³ | Produces |
|---|---|---|---|---|
| `castheavyplate` | 160 | 2 × 10 × 8 = 160 | 1.00 | `iwex:castplate-heavy` |
| `castingotmold` | 152 | drawn tray, one bay | — | block `iwex:casting-mold-ingot` (the plate mold was retired 2026-08-05) |
| `castbarrel` | 200 | 8 × 8 × 8 = 512 | 0.39 | `iwex:cast-barrel` |

Four cavities, four densities, none of them 2.5.

### Shared item properties

| Property | Value | file:line |
|---|---|---|
| `MaterialDensity` | 7200 kg/m³ (cast iron) on the family | `CastPartItemDefinitions.cs:38`, `:51`; `BevelGearItemDefinitions.cs:20` |
| `combustibleProps` | `meltingPoint = 1150` and nothing else | `CastPartItemDefinitions.cs:39`, `:52`; `BevelGearItemDefinitions.cs:21` |
| max stack | 16 (plate, gear) / 8 (barrel) | `CastPartItemDefinitions.cs:37`, `:50`; `BevelGearItemDefinitions.cs:19` |
| GUI scale | 1.3, plate only | `CastPartItemDefinitions.cs:41` |
| pattern `minPourTemp` | 1150 °C (the shared default) | `PatternItemDefinitions.cs` |
| wooden pattern durability | 24 impressions | `PatternItemDefinitions.cs:241` |

`MaterialDensity` is the engine's kg/m³ and has nothing to do with the unit rule - see
[density rule § Gotchas 1](../mechanics/density-rule.md).

---

## Assets

Every cast-part shape declares its own texture key `cast-iron1 → iwex:block/metal/castiron`, so the item defs
set no texture at all.

### Runtime — `assets/iwex/shapes/`

| Shape | State |
|---|---|
| `item/cast-barrel.json` | tracked |
| `item/gearbevel.json` | tracked |
| `item/heavyplate.json` | untracked (`git status` reports `??`), and drawn 12 × 2 × 12 |
| `casting/cell-filling-heavyplate.json`, `…-moltenbarrel.json` | tracked - the impressions for the plate and the barrel |
| `iwex:item/castwheelsection.json` · `iwex:item/castshell.json` | untracked; exported 2026-08-04 from `item-sandcast-wheelsegment` / `item-sandcast-shell` |
| `casting/cell-filling-castshell.json` · `…-flywheelpart.json` | untracked, referenced - the shell's by `iwex:pattern-castshell-*`, the other by `iwex:pattern-castwheelsection-*`. The filling keeps the older `flywheelpart` name |
| `casting/cell-filling-{axle,cylinder,gearblanklarge,gearblanksmall}.json` | untracked, and no pattern references any of them - [casting cell § Gotcha 10](../machines/casting-cell.md) owns this |
| `casting/longcell-filling-castframe.json` | untracked; [long cell](../machines/long-cell.md) owns it |

### Editable — `assets/editable/shapes/`

| Source | State | Measured |
|---|---|---|
| `item-castplate.json` | untracked; it replaces `item-heavyplate.json`, which is staged deleted (`D`) | 8 × 2 × 8 = 128 vx³ |
| `item-castbarrel.json` | tracked; element-for-element the runtime `item/cast-barrel.json` | 603 naive |
| `item-castframe.json` | tracked | 964 naive |
| `item-sandcast-shell.json` (was `item-castshell.json`) | untracked rename | 216 naive |
| `item-sandcast-wheelsegment.json` (was `item-castflywheelpart.json`) | untracked rename | 216 naive |
| `item-cylinder-castblank.json` · `item-cilinder-bored.json` · `item-cylinder-pipesegment.json` | untracked | 768 · 425 · 336 naive |
| `item-gear-castblanklarge.json` · `item-gear-castblanksmall.json` | untracked (renames of the deleted `item-gearblank*.json`) | — |
| `item-sandcast-ingotmold.json` | untracked; the single-bay tray, exported 2026-08-05 to `item/ingotmold.json` and `molten/molds/ingot.json` | — |
| `molten-sandcellfilling-cast*.json` (11 files) | untracked (renames of the deleted `sandcasting-cell-filling*.json`) | — |

The rename only half happened. The editable source was renamed `item-heavyplate` → `item-castplate`, which
is the settled split expressed in art, but the runtime export is still `item/heavyplate.json` and the item
def still points at it (`CastPartItemDefinitions.cs:36`). So the same part is called `castplate` in the art
folder, `heavyplate` in the shape folder and `castplate-heavy` in code.

Neither drawing is the settled geometry. Settled `castplate` is 10 × 2 × 10 (200 vx³, 500 u - re-affirmed
2026-08-07) and settled `heavyplate` is 12 × 2 × 10 (240 vx³, 600 u). The editable file draws 8 × 2 × 8
(128 vx³ → 320 u) and the runtime export draws 12 × 2 × 12 (288 vx³ → 720 u). One of the two would have to
be redrawn even if the split had never been decided.

Textures: all twelve editable cast shapes carry an absolute authoring path
(`F:/repos/modding-vsexpanded/assets/editable/textures/cast-iron1`) and all three exported runtime shapes
carry `iwex:block/metal/castiron`. The absolute path is the editable-folder convention and the export
rewrites it; it is not a per-file defect.

Lang: `item-castplate-heavy`, `item-cast-barrel`, `item-bevelgear` and the `item-pattern-{type}-*` keys all
exist in all three languages (`assets/iwex/lang/en.json:338-343`, `:99`).
Handbook: no page - `docs/iwex/handbook/` holds five pages and none covers cast parts.

---

## Code

| Member | file:line | Role |
|---|---|---|
| `CastPartItemDefinitions` | `src/IronworkingExpanded/Items/CastPartItemDefinitions.cs:18` | the family - four items |
| `…HeavyPlateUnits` / `…CastBarrelUnits` / `…CastWheelSectionUnits` / `…CastShellUnits` | `:21` / `:24` / `:65` / `:79` | the masses; `public const`, read by `PatternItemDefinitions` |
| `…Definitions` | `:84` | `[HeavyPlate, CastBarrel, CastWheelSection, CastShell]` |
| `BevelGearItemDefinitions` | `src/IronworkingExpanded/Items/BevelGearItemDefinitions.cs:10` | the fifth cast-iron item, defined apart from the family |
| `PatternItemDefinitions.Molds` | `…/BlockStructures/Casting/PatternItemDefinitions.cs:108-230` | the cavities that produce cast parts; consumes the constants |
| `MoltenRecipeDefinitions.MoltenBarrel` | `…/Recipes/Grid/MoltenRecipeDefinitions.cs:24-47` | the one shipped cast ↔ fabricated dual path |
| `BlockMoltenBarrel.Definitions` | `…/BlockNetworkMolten/Blocks/BlockMoltenBarrel.cs:47-69` | the `construction` variant axis (`:53`) |
| `BlockCastMold.Definitions` | `…/BlockStructures/Casting/Blocks/BlockCastMold.cs:27-51` | the cast block output |
| goldens | `test/IronworkingExpanded.Tests/goldens/iwex/itemtypes/castplate-heavy.json`, `cast-barrel.json`, `pattern.json` | pin the emitted defs |

Where a caller hooks in: adding a cast part is one `Molds` entry, one filling shape and one output item;
nothing in the cell changes and no mod needs to be referenced
([casting cell § Code](../machines/casting-cell.md) owns that contract). Adding its fabricated twin is one
extra grid/RCC alternative on the same output.

---

## Gotchas

1. No live cast part can be remelted, by any path. The constants are documented as existing
   "for remelt/scrap maths" (`CastPartItemDefinitions.cs:20`, `:23`) and the maths does not exist:
   * their `combustibleProps` carry only `meltingPoint` - no `meltingDuration`, no `smeltedRatio`, no
     `smeltedStack` (`:39`, `:52`; confirmed in the golden, `castplate-heavy.json:8-10`), so vanilla smelting
     cannot touch them. Contrast the generated metal forms, which all declare one
     (`MetalFamilyEmitter.cs:466-477`);
   * the cupola charges only items holding the `scrap` role, plus fuel
     (`BlockEntityCupolaFurnace.cs:71-76`), and the role's holders are the two vanilla metalbits and the
     pig family (`assets/iwex/config/materialroles.json:6-10`) - no cast part is among them.

   A heavy cast plate is a one-way item: 160 u of iron that can never come back. Same for the barrel blank
   and the bevel gear. The generated `iwex:metalbit-castiron` is not in the scrap list either.

2. `castplate-heavy`'s consumers are recipes for machines that are themselves unbuilt (flywheel web,
   rolling-mill housing), so the casting cell's flagship product still has nowhere to go in a playthrough.
   Every other proposed consumer (puddling hearth plating, furnace doors and frames, machining stock, valve
   bodies) lives in prose only.

3. `bevelgear` has no recipe - it is consumed by `BlockCastIronBevel` and produced by nothing. It also
   has no pattern, so it is not even castable in creative-adjacent play.

4. The shipped plate is one item wearing both names. Code says `castplate-heavy` (the cast half of the
   split), the shape says `heavyplate` (the rolled half), and the drawn geometry matches neither settled
   figure. When the split lands, this item does not get renamed, it gets divided, and the rolled half leaves
   for [rolled parts](rolled-parts.md).

5. `materialUnits` is written on every one of these items and read by nothing - every machine reads the
   C# constant. [density rule § Gotchas 2](../mechanics/density-rule.md) owns the general fact; the
   consequence here is that fixing a cast part's mass means touching `CastPartItemDefinitions`, not the
   attribute.

6. A cast part with no drawn item shape gets a silently wrong pattern. The pattern shape table has one
   entry per pattern type today, but a type with no entry falls back to `game:item/plate` - so the wooden
   positive for a new part reads as a flat plate until its art lands, with no error anywhere.

7. Five drawn impressions still have no pattern: axle, both gear blanks and the cylinder (cell), plus
   `castframe` (long cell). [casting cell § Gotcha 10](../machines/casting-cell.md) and
   [long cell](../machines/long-cell.md) own the inventories.

8. The barrel blank is worth 200 u and the barrel it becomes holds 800. `CastBarrelUnits = 200` is the
   cavity capacity that casts the blank; `maxUnits = 800` is what the finished vessel stores
   (`BlockMoltenBarrel.cs:36`, `:60`). They are unrelated numbers that both read as "how much metal a barrel
   is about", which is how the 4× discrepancy has survived.

9. `item-cilinder-bored.json` is misspelt ("cilinder"). It has no runtime export yet, so the fix is free
   until it does.

10. A cast part standing in a broken cell is voided - [casting cell § Drops](../machines/casting-cell.md).
    Combined with Gotcha 1, cast iron committed to this family has two one-way exits.

---

## Open

1. Settled 2026-08-07 - `castplate` is 500 u at 10 × 2 × 10. The D2 figures are re-affirmed over the
   counter-argument for 600 at 12 × 2 × 10 (600 divides both slabs). The code constant
   (`HeavyPlateUnits = 160`) and both drawings still disagree with the ruling; the re-mass and the redraws
   follow in a queued batch, tracked in [economy landing](economy-landing.md), because the capacity, the
   cost catalogue and every proposed bill of materials move together.

2. The split itself is unbuilt. One item exists where two are settled, and no rolled `heavyplate` item,
   recipe or shape exists anywhere.

3. The remelt path (Gotcha 1). Options, none chosen: give the cast items `smeltedStack`/`smeltedRatio`
   like the generated metal forms do; add them to the `scrap` role so the cupola takes them; or rule that
   finished castings are unrecoverable and delete the doc-comments that promise otherwise.

4. A reachable consumer for the finished cast products (Gotcha 2). The cheapest fix that closes it is
   the puddling hearth's cast-iron bed, which is already designed to eat `castplate-heavy`
   ([puddling furnace](../machines/puddling-furnace.md)).

5. Not one fabricated substitute is buildable, because rivets, `beam` and `boilerplate` do not exist and
   the [bending roller](../machines/bending-roller.md) is unbuilt. The open balance question - how many
   plates and rivets a shell, barrel or rim costs against its cast original - is
   [bending roller § Open 4](../machines/bending-roller.md)'s, and D2's "cast vs fabricate is a real choice"
   claim rests on it.

6. `moltenbarrel-plated` should move from nails to rivets and from the grid to the roller once both
   exist. Until then it is the right structure with the wrong ingredients.

7. Solid-volume measurement for hollow and toothed art. The barrel and the bevel gear are the two shapes
   in the whole suite that block mass generation
   ([density rule § Open 2](../mechanics/density-rule.md)); both live in this family.

8. Where the cast/machined boundary sits. Cylinder blanks, gear blanks and the axle are excluded from
   the substitute table because they are machined, not structural - but nothing has checked whether a
   fabricated cylinder (rolled shell + riveted heads) should exist at the hpex tier, where cast frames
   already stop being adequate ([hp hammer](../machines/hp-hammer.md)).
