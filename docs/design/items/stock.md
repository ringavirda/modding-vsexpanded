# Stock

**Status** blocked - two of the five forms exist as items and both are mis-massed; the other three exist only
as art and as three dead branches of the reheat hearth's recogniser
**Mod** iiex ships the two live items; smex owns the three cast forms (who owns the forming line)

**Owns**

* the five-form stock ladder - `shingledbar`, `shingledslab`, `castbillet`, `castbloom`, `castslab` - each
  one's section × length, voxel volume, settled mass, what makes it and what eats it;
* the stock item definition as an item: `MaxStackSize`, `MaterialDensity`, `combustibleProps`,
  `temperatureDamage`, `materialUnits`, and the two shipped codes' lang names;
* the stage-naming schemes - the `{Family}{t×100}` element names the authored art uses, the
  `stock-{form}-{t×10}` shape path the code resolves, and why the two cannot both express a half-step;
* the art inventory: which stock shape is wired to an item, which is authored and orphaned, which is
  generated from inputs that no longer exist, and which stage of which ladder each drawn element is;
* the volume-conservation audit of the drawn stages - every authored element against the mass its form
  should hold.

**Does not own** - cited only, never restated

| Fact | Owner |
|---|---|
| `1 vx³ = 2.5 u`, its derivation, the shipped-mass audit and the "art drawn, no item yet" table | [density rule](../mechanics/density-rule.md) |
| the pass model (a gap = two rounds, the half-step, `sides = ceil(entryWidth / barrelWidth)`), `StockForm`, `WorkPiece`, `RollingPass`, spread exponents, the shipped roll-set catalogue and every `Rolling*` key | [rolling mill](../machines/rolling-mill.md) |
| ≤ 32 lengthwise / ≤ 48 crosswise, the two mandatory crops, the seating modes, the soft-locks | [recoverability](../mechanics/recoverability.md) |
| the crop verb, `Outputs`-keyed-on-stage, the cold-cut torque gate | [shear](../machines/shear.md) |
| the long cell's interior, its three lane counts and its four fillings | [long cell](../machines/long-cell.md) |
| the hearth's contents model, `HeatingHearthLayout` and its element map | [reheat furnace](../machines/reheat-furnace.md) |
| every product cropped out of this stock | [rolled parts](rolled-parts.md) |
| the six-stand train the wide forms run on | [wide hall](../machines/wide-hall.md), [steel roll sets](../machines/steel-roll-sets.md) |

**Depends on** [density rule](../mechanics/density-rule.md) · [rolling mill](../machines/rolling-mill.md) ·
[recoverability](../mechanics/recoverability.md) · [long cell](../machines/long-cell.md) ·
[reheat furnace](../machines/reheat-furnace.md) · [shear](../machines/shear.md) ·
[steam hammer](../machines/steam-hammer.md) · [puddling furnace](../machines/puddling-furnace.md)

---

## Role

Stock is what the mill reduces and the shear crops. It is the only item family in the suite that is not a
product: it exists to be walked down a gap schedule, carried back around the stand, reheated, and eventually
cut into something. Hence `MaxStackSize(1)`, because each piece carries its own state; a real temperature on
the stack, because the carry-back is a heat budget; and no anvil recipe, because the mill forms it and the
helve only consolidated it (`StockItemDefinitions.cs:10-17`).

The ladder has two tiers and the split is wrought vs steel. The shingled pair are hammered out of puddle
balls; the cast three are poured in the long cell out of converter or open-hearth steel. Cast iron cannot be
rolled - it shatters, and the mill is a hot wrought mill - so every cast form is steel and therefore smex
content.

The tier is legible in the handling, not just the size: a 27-long billet is past the reheat hearth's
lengthwise seating from its very first half-step, so cast stock lies across the hearth while puddled bar lies
along it ([recoverability](../mechanics/recoverability.md)).

---

## The catalogue

Settled 2026-07-29. Mass is the density rule applied to the drawn section; every crop below divides exactly.

| Item | Section × length | vx³ | Mass (u) | Made by | Consumed by |
|---|---|---|---|---|---|
| `shingledbar` | 3 × 3 × 18 | 162 | 400 | helve hammer, 2 wrought balls @ 200 | the iiex mill - narrow `flat` and `grooved` |
| `shingledslab` | 8 × 3 × 20 | 480 | 1200 | [steam hammer](../machines/steam-hammer.md) (iiex), 6 wrought balls @ 200; the hammer reads the pile at the first lever pull | the iiex [wide hall](../machines/wide-hall.md) - four stands, 2.5 → 1.0 |
| `castbillet` | 3 × 3 × 27 | 243 | 600 | [long cell](../machines/long-cell.md), 3-lane billet pattern | the iiex mill's own barrel, on steel roll sets - never the train |
| `castbloom` | 4 × 4 × 25 | 400 | 1000 | long cell, 2-lane bloom pattern | the wide train extended to six stands (smex adds 3.5 / 3.0) |
| `castslab` | 12 × 4 × 25 | 1200 | 3000 | long cell, 1-lane slab pattern - its mold walls are the cell walls | the same six-stand train |

Where each one goes. Full product-side arithmetic is [rolled parts](rolled-parts.md)'s; the crop counts are
here because they are what fixes each stock's length:

| Stock | Grooved crop | Flat crop | Wide crops |
|---|---|---|---|
| `shingledbar` 162 | 2.0 → 4 × `rolledrod` | 2.0 → 2 × `beam` · 1.0 → 2 × `game:metalplate` | — |
| `castbillet` 243 | 2.25 half-step → 6, each finished to 2.0 → `rolledrod` | 2.0 → 3 × `beam` · 1.0 → 3 × plate (27 = 3 × 9) | — |
| `shingledslab` 480 | — | — | 2.0 → 2 × `heavyplate` · 1.0 → 2 × `boilerplate` |
| `castbloom` 400 | — | 3.0 → crop 5, each to the narrow flat set → 5 plate | 2.0 → 5 × `blank` · 1.0 → 5 × `skelp` |
| `castslab` 1200 | — | — | 2.0 → 5 × `heavyplate` · 1.0 → 5 × `boilerplate` |

The billet's mid-gap crop is not a product decision - it is the recoverability invariant forcing a stop a
round early, and it is owned [there](../mechanics/recoverability.md).

The wrought ball is an input with no item. 9 pigs = 3375 u puddle into 16 balls (3200 u) + 175 u of tap
cinder ([puddling](../processes/puddling.md)); 2 balls make a bar, 6 make a slab
([shingling](../processes/shingling.md)). No `ball` item exists in `src/`, and the puddling furnace cannot
light ([STATE.md § B8](../../internal/plans/STATE.md)).

---

## Numbers

### What the code ships

Only `stock-shingledbar` and `stock-shingledslab` exist. They are emitted one per `StockForm`
(`StockItemDefinitions.cs:30`), so the item list and `StockForm.All` cannot diverge. Both were renamed onto
the settled ladder and re-massed on 2026-08-12; `bloom` and `slab` survive only as `FormerNames`, which is
what carries a piece already in a world across the rename.

| Shipped item | `materialUnits` | file:line | Section × length | Rule says | Verdict |
|---|---|---|---|---|---|
| `stock-shingledbar` | 400 | `StockItemDefinitions.cs:24` | 3 × 3 × 18 = 162 vx³ | 405, rounded to 400 so it divides by four | settled |
| `stock-shingledslab` | 1200 | `StockItemDefinitions.cs:25` | 8 × 3 × 20 = 480 vx³ | 1200 | settled, exactly |

⛔ The **art** did not move with the form. The bar's ten generated stage shapes are still drawn off a
16-long base while the form is 18, so the held item is 2 voxels short of its own mass. Nothing reads a
shape's length - the item's mass, width and length all come from `StockForm` - so this is proportions
only, and wiring the authored art (drawn at 18, as two 9-long halves) closes it.

The full cross-item audit - including the four shipped mold cavities and their four different implicit
densities - is [density rule](../mechanics/density-rule.md)'s and is not repeated here.

No `castbillet`, `castbloom` or `castslab` item exists anywhere in `src/`. The only code that names them is
`HeatingHearthLayout.StockOf`'s prefix recogniser (`HeatingHearthLayout.cs:72-77`), so three of the five forms
the reheat hearth can recognise can never be held.

### Per-item properties — `StockItemDefinitions.Stock`

| Property | Value | file:line | Why |
|---|---|---|---|
| item class | `ItemStockPiece` | `:40` | composes its own mesh per state; see [rolling mill](../machines/rolling-mill.md) |
| shape | `iiex:forming/stock-{form}-{(int)(t × 10)}` | `:41` | the form's as-shingled stage; a part-rolled piece overrides per stack |
| `MaxStackSize` | 1 | `:42` | each piece carries its own gauge and heat, so two can never merge |
| `MaterialDensity` | 7800 | `:43` | real kg/m³ for wrought, for engine weight - not the unit rule ([density rule](../mechanics/density-rule.md) Gotcha 1) |
| `materialUnits` | 400 / 1200 | `:44` | written on the item and never read by any mod code |
| `stockForm` | the form name | `:45` | lands in the *itemtype* attributes, not the stack tree; `WorkPiece.FromStack` falls back to it, which is what closed B3 |
| `combustibleProps.meltingPoint` | 1500 | `:52` | |
| `combustibleProps.meltingDuration` | 30 | `:53` | |
| `combustibleProps.smeltedRatio` | 1 | `:54` | one piece melts back to one unit of its metal |
| `temperatureDamage` | 4 | `:57` | it comes off the helve at forging heat and burns on contact |
| creative | `CreativeCommon("*")` | `:58` | the only way to obtain one - there is no recipe |

Heat itself is vanilla's `temperature` stack attribute, so the cooling during the carry-back is the engine's
(`StockItemDefinitions.cs:46-47`).

### Lang

| Key | Ships | file:line | Verdict |
|---|---|---|---|
| `item-stock-shingledbar` | "Shingled Bar" | `assets/iiex/lang/en.json:91` | settled; ru «Кричный брусок», uk «Кричний брусок» - both drafts, pending the author's review |
| `item-stock-shingledslab` | "Shingled Slab" | `assets/iiex/lang/en.json:92` | settled - it used to read "Cast Slab", which is a form that does not exist. ru/uk «Кричный сляб» / «Кричний сляб», drafts |

---

## Assets

### Wired

| Asset | Path | State |
|---|---|---|
| stage shapes | `assets/iiex/shapes/forming/stock-{bloom,slab}-{5,10,15,20,30}.json` | ship, untracked in git, resolved by `StockItemDefinitions.cs:41`, measured off disk by `RolledStockStagesTests.cs:24-29` |

Ten files, five stages each (3.0 / 2.0 / 1.5 / 1.0 / 0.5), generated by `scripts/generate-rolled-stock.py`
from two editable bases that have since been deleted (`generate-rolled-stock.py:44-48` names
`item-shingledbloom` / `item-shingledslab`; both are `D` in `git status`). The script has no existence guard,
so it throws `FileNotFoundError`: the shipped output is unreproducible, and the tests pass anyway because they
read the files rather than regenerate them. The construction list (§ Open) deletes the generator and its
outputs together.

### Authored and orphaned

Every file below is untracked and referenced by no code, no test and no generator input.

| File | Elements | What it is |
|---|---|---|
| `assets/editable/shapes/item-shingled-bar.json` | 9 top-level | the whole bar ladder - both routes (below) |
| `assets/editable/shapes/item-shingled-slab.json` | `ShingledSlab1` `:15-17` + child `:29-31` | 8 × 3 × 10 twice = 8 × 3 × 20 = 480 vx³ → 1200 u - exactly the settled slab |
| `assets/editable/shapes/item-beam-rolled.json` | 5 top-level | the bar's flat route below the 2.0 gap |
| `assets/editable/shapes/item-rod-rolled.json` | 5 top-level | the `rolledrod`'s own grooved route |
| `assets/editable/shapes/item-rod-nail.json` | `NailRod1` `:16-18` + 3 children | 4 × (1 × 1 × 10) - the rod bundle, misnamed; nails come from plate ([rolled parts](rolled-parts.md)) |
| `assets/editable/shapes/item-castbillet.json` | `CastBillet1` `:15-17` + child `:29-31` | 3 × 3 × 12 twice = 3 × 3 × 24 |
| `assets/editable/shapes/item-castbloom.json` | `CastBloom1` `:15-17` + child `:29-31` | 4 × 4 × 12 twice = 4 × 4 × 24 |
| `assets/editable/shapes/item-castslab.json` | `CastSlab1` `:15-17` + child `:29-31` | 12 × 4 × 14 twice = 12 × 4 × 28 |
| `assets/iiex/shapes/item/{castbillet,castbloom,castslab}.json` | — | the same three, exported to the runtime domain, untracked, no item resolves them (a grep for these names in `src/` returns only `HeatingHearthLayout.cs:72-77`) |

All three cast shapes are drawn to the wrong length - 24 / 24 / 28 against the settled 27 / 25 / 25. The
redraw and the move to the smex domain are queued with the ladder work (§ Open); the audit row is
[density rule](../mechanics/density-rule.md)'s.

The editable files reference textures by absolute local path (`item-shingled-bar.json:12` points at
`F:/repos/modding-vsexpanded/.game/1.22/…/iron5`). That is the editable-source convention; the generator
rewrites every texture to `game:block/metal/sheet-plain/iron5` on export
(`generate-rolled-stock.py:38`, `:96`).

### The two stage-naming schemes

The art names a stage `{Family}{t×100}` - `Flattened275`, `Flattened250`, `Grooved225`, `Grooved175` - and
gives descriptive names to anything that is a product: `ShingledBar1`, `Beam`, `CutRod1…4`, `CutPlate1…2`,
`CutNailRod1…4`, `RolledRod200`. A cut is expressed as the parent element plus one child per further piece
(`item-shingled-bar.json:140-196` is one 2 × 2 bar as four 10-long pieces).

The code names a stage `t × 10`, truncated - `stock-{form}-{(int)(form.BaseThickness * 10)}`
(`StockItemDefinitions.cs:41`; `RolledStockStagesTests.cs:29` uses the same expression). The two schemes do
not agree and the code's cannot express the half-step: `(int)(2.25f * 10)` is 22, not 22.5, and
`(int)(1.75f * 10)` is 17. Every half-step in the settled schedule (2.75 / 2.25 / 1.75 / 1.25) collides with
a tenth-voxel stage under that scheme and rounds away the ¼ that makes it a half-step. Anything that resolves
stage art by path must move to `t × 100` before the settled half-step schedules land.

### What the bar's art actually draws — volume audit

The authored bar carries both routes off one 162 vx³ piece, and the art conserves volume across every stage.
Flat elements are all 9 + 9 = 18 long; grooved elements grow.

| Stage | Grooved (`w = t`) | file:line | vx³ | Flat (`w = 3 × 3/t`) | file:line | vx³ |
|---|---|---|---|---|---|---|
| 3.00 | 3 × 3 × 18 | `item-shingled-bar.json:16-18`, `:31-33` | 162.0 | *(same piece)* | | |
| 2.75 | 2.75 × 2.75 × 22 | `:47-49`, `:62-64` | 166.4 | 3.25 / 3.3 × 2.75 × 18 | `:199-201`, `:213-215` | 162.1 |
| 2.50 | 2.5 × 2.5 × 26 | `:78-80`, `:92-94` | 162.5 | 3.6 × 2.5 × 18 | `:229-231`, `:243-245` | 162.0 |
| 2.25 | 2.25 × 2.25 × 32 | `:109-111`, `:123-125` | 162.0 | 4.0 × 2.25 × 18 | `:259-261`, `:273-275` | 162.0 |
| 2.00 | 4 × (2 × 2 × 10) - cut | `:140-142` + `:154-195` | 160.0 | 4.5 × 2 × 18 - cut ×2 | `:289-291`, `:303-305` | 162.0 |
| 1.75 | — | | | 5.1 × 1.75 × 18 | `item-beam-rolled.json:47-49`, `:62-64` | 160.7 |
| 1.50 | — | | | 6.0 × 1.5 × 18 | `:78-80`, `:93-95` | 162.0 |
| 1.25 | — | | | 7.2 × 1.25 × 18 | `:109-111`, `:124-126` | 162.0 |
| 1.00 | — | | | 9.0 × 1 × 18 - cut ×2 | `:140-142`, `:154-156` | 162.0 |

Six of the eleven stages are exact; the drift is ≤ 2.7 % and lands only on half-steps and on the grooved cut
(4 × 10 drawn against 4 × 10.125 exact). The two numbers that had to be exact are exact: the grooved 2.25
stage is drawn 32.0 long and the flat 1.0 stage 9 × 1 × 9 twice, i.e. vanilla `game:metalplate` geometry to
the voxel.

---

## Code

| Member | file:line | Role |
|---|---|---|
| `StockItemDefinitions` | `src/IronIndustryExpanded/BlockStructures/Forming/StockItemDefinitions.cs:19` | `IExItemDefProvider`; one item per `StockForm` |
| `.Units` | `:23-26` | the two masses - `shingledbar` 400, `shingledslab` 1200, both from geometry at 1 vx³ = 2.5 u |
| `.Definitions` | `:29-30` | `StockForm.All.Values.Select(Stock)` - the item list cannot diverge from the form list |
| `.Stock` | `:32-59` | the def itself; every property above |
| `ItemStockPiece` | `.../Forming/Items/ItemStockPiece.cs:22` | the item class; mesh composition is [rolling mill](../machines/rolling-mill.md)'s |
| `HeatingHearthLayout.StockOf` | `.../Furnaces/HeatingHearthLayout.cs:64-79` | the prefix recogniser - the only other place a stock code is named |
| `HeatingHearthLayout.Stock` | `.../HeatingHearthLayout.cs:21-28` | the five-member enum; three members have no item |
| `RolledStockStagesTests` | `test/IronIndustryExpanded.Tests/Blocks/Forming/RolledStockStagesTests.cs:22` | 9 methods pinning the ten generated shapes to the spread model |
| `scripts/generate-rolled-stock.py` | `:44-48`, `:87-120` | the dead generator |

To add a stock form: add a `StockForm` and the item is emitted automatically (`:30`) - but its stage art must
also be supplied or `RolledStockStagesTests` fails. To **rename** one, declare the old name in
`FormerNames`: the registry resolves a piece already in a world by it, and `StockFormRenameMigration` emits
the item-code remap off the same field. The wrought pair is done; the three cast forms are still to add.

---

## Gotchas

- A stock item still has no **mass** the game can read. `materialUnits` is written and read by no mod code.
  The other two are answered now: `stockForm` resolves through `FromStack`'s itemtype fallback (B3 closed),
  and `WorkPiece` gained `Length`/`LengthAt` with the two-round model. `Mass` is the one left, and it is
  what the 48-voxel refusal needs.
- ~~`stock-slab` is called "Cast Slab" in lang and is the wrought one.~~ Fixed with the rename; both keys
  moved, and no handbook page names either item, so the `NN-` join was not involved.
- The two shipped forms are 2 voxels short and 2.7× light respectively, so any recipe or machine bill written
  against them today will need re-costing when the ladder lands.
- `MaxStackSize(1)` is load-bearing. Two pieces at different stages must never merge, and the heat is
  per-stack.
- The billet's crop is mid-gap. It stops at the 2.25 half-step, which `RollSetSpec.TryParse` cannot even
  declare as an output (it rejects any output gap not in `gaps`) - see [shear](../machines/shear.md).
- The three cast shapes are drawn but the long cell that pours them does not exist - seven shapes, one enum
  member, no block ([long cell](../machines/long-cell.md)).
- Don't read the editable files as one item each. `item-shingled-bar.json` holds nine stages of two routes;
  `item-rod-rolled.json` holds five. A box-sum over a whole file is meaningless
  ([density rule](../mechanics/density-rule.md) Gotcha 5).
- `item-shingled-bar.json:201` and `:215` disagree by 0.05 voxel - the 2.75 flat stage is drawn 3.25 wide in
  the parent and 3.3 in the child, so the "two halves of one bar" reading is broken there and only there.

---

## Open

- Nothing produces any stock. Puddling → helve shingling is the settled route for the wrought pair and
  neither is built (puddling additionally cannot light, [STATE.md § B8](../../internal/plans/STATE.md)); the long cell
  that would pour the cast three does not exist. Every piece of stock in the game today comes from creative.
- This page's construction list: replace the two forms, delete the generator and its ten outputs, wire the
  authored art, redraw the three cast shapes to 27 / 25 / 25 and move them to the smex domain, then add the
  three cast `StockForm`s with their long-cell patterns and steel roll sets.
- The stage-art path scheme must move to `t × 100` before half-steps can have art at all - and under the
  settled two-round model the half-step is the only thing that needs art, because a piece can no longer be
  lopsided.
- The wrought ball has no item. It is the input to both shingling routes; the puddling yield (16 balls +
  175 u cinder from 9 pigs) is [puddling](../processes/puddling.md)'s, the 200 u ball mass is
  [shingling](../processes/shingling.md)'s, and the mass lands with the
  [economy landing](economy-landing.md) batch - but no `ball` item exists in `src/`.
