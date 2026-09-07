# Rolled parts

**Status** ★★ **the catalogue is BUILT and all six items are obtainable, 2026-08-14** —
`RolledItemDefinitions` ships `rivetrod`, `nailplate`, `beam`, `blank`, `skelp` and `boilerplate`, and two crop
tables reach them: `mods/iiex/assets/iiex/config/processjobs/shear.json` (wrought and the rod fork) and
`mods/siex/assets/siex/config/processjobs/shear.json` (cast). **Both tiers of the forming line produce a finished
product, and both branches of the fork work.** ⛔ Two declared rows are still unreachable: see
[What is reachable today](#what-is-reachable-today)
**Mod** iiex owns the rolled products, narrow and wide alike, and the forming line that makes them - the
mill, the wide hall and the bending roller; siex only feeds bigger stock into iiex's train, and owns the
steel roll sets (settled by M1/M.7, 2026-08-14)

**Owns**

* the rolled-product catalogue - `rolledrod`, `rivetrod`, `nailplate`, `beam`, `blank`, `skelp`, `heavyplate`,
  `boilerplate` and the mod's use of vanilla `game:metalplate` - each one's section × length, voxel volume,
  settled mass, owning mod, and what consumes it;
* every crop point: which stage of which stock each product is claimed at, how many come off one piece, and
  which of those counts are exact and which are rounded;
* the rod fork - one 100 u `rolledrod`, four feeds either way, two different products - and the rule that
  nails come from plate, never from rod;
* that the rolled and the cast heavy plate are **one item with a metal axis** at one mass, and what remains
  of the merge (M.8);
* the fabricated-substitute demand side: `beam` + plate + rivets is what gives these products their consumer
  list;
* the art inventory for these products - what is drawn, what is missing, and which drawn element is which
  product.

**Does not own - cited only, never restated**

| Fact | Owner |
|---|---|
| the pass model, `δ_max = μ²R`, spread, elongation, `sides`, the two-round gap, `RollSetSpec`, `WorkPiece`, the shipped roll-set catalogue and its four dangling codes | [rolling mill](../machines/rolling-mill.md) |
| `1 vx³ = 2.5 u` and the shipped-mass audit | [density rule](../mechanics/density-rule.md) |
| ≤ 32 / ≤ 48, the two mandatory crops and why cold shear is not an escape | [recoverability](../mechanics/recoverability.md) |
| the crop station itself, crop-not-convert, `Outputs`-keyed-on-stage, the cold-cut torque gate, "the shear cuts across, the die cuts out" | [shear](../machines/shear.md) |
| the stock these are cropped from, its masses and its stage art | [stock](stock.md) |
| the six wide roll sets, the hall, the shared shaft | [wide hall](../machines/wide-hall.md) · [steel roll sets](../machines/steel-roll-sets.md) |
| stamping, blanking and the die catalogue | [steam hammer](../machines/steam-hammer.md) |
| the benches that eat rod and nail plate | [heading machine](../machines/heading-machine.md) · [nail machine](../machines/nail-machine.md) |
| curling `skelp` into pipe | [bending roller](../machines/bending-roller.md) · [rolled pipe](../machines/rolled-pipe.md) |
| the cast `castplate` / `castplate-heavy` item | [cast parts](cast-parts.md) · [casting cell](../machines/casting-cell.md) |

**Depends on** [rolling mill](../machines/rolling-mill.md) · [shear](../machines/shear.md) ·
[stock](stock.md) · [density rule](../mechanics/density-rule.md) ·
[recoverability](../mechanics/recoverability.md) · [wide hall](../machines/wide-hall.md) ·
[steam hammer](../machines/steam-hammer.md) · [heading machine](../machines/heading-machine.md) ·
[nail machine](../machines/nail-machine.md) · [bending roller](../machines/bending-roller.md)

---

## Role

The mill only ever makes stock; the shear turns a stage into a product; this page is the list of products.

- Vanilla has no hand route from a bloom to plate, rod or nails at scale, and every machine downstream is
  built out of `game:metalplate` and `game:metalnailsandstrips` - `ExIngredients.Plate` and `.Nails`
  (`ExIngredients.cs:28-37`) are reached for by name in more than twenty recipe sites across all four mods.
- Four of the five shipped mill output codes name items that do not exist
  ([rolling mill](../machines/rolling-mill.md)), so the reduction simulator terminates in nothing.
- Every cast-iron structural part gets a rolled/fabricated equivalent built from beam + plate + rivets
  through the RCC dual path, which is what gives `beam` and the wide plates a consumer list
  ([STATE.md § D2](../../internal/plans/STATE.md)).

No route mints a unit against the hand route. The chain is conserved end to end: one 400 u bar is 2 plates or
4 rods or 16 nails, and 4 nails per 100 u is vanilla's own anvil rate
([nail machine](../machines/nail-machine.md)).

---

## What is reachable today

*Built 2026-08-14; the cast routes and the rod fork followed the same day.* The six items exist and carry
their settled masses. What a player can actually obtain is **ten** routes — the five wrought and one rod
crop row in iiex's table, the five cast ones in siex's, merged by machine at load, plus the flat 1.0 stage
the mill claims as nail plate without a crop. What is left unreachable is two rows on two blockers, neither
of them art:

| Route | State | Blocked on |
|---|---|---|
| `shingledbar` → `game:rod-iron` ×4 (grooved 2.0) | **works** | — |
| `shingledbar` → `beam` ×2 (flat 2.0) | **works** | — |
| `shingledbar` → `game:metalplate-iron` ×2 (flat 1.0) | **works** | — |
| `shingledslab` → `boilerplate` ×2 (flat 1.0) | **works** | — |
| `castbillet` → `beam` ×3 (flat 2.0) | **works** *(2026-08-14)* | — |
| `castbillet` → `game:metalplate-iron` ×3 (flat 1.0) | **works** *(2026-08-14)* | — |
| `castbloom` → `blank` ×5 (wide 2.0) | **works** *(2026-08-14)* | — |
| `castbloom` → `skelp` ×5 (wide 1.0) | **works** *(2026-08-14)* | — |
| `castslab` → `boilerplate` ×5 (wide 1.0) | **works** *(2026-08-14)* | — |
| `game:rod-iron` → `rivetrod` ×4 (grooved 1.0) | **works** *(2026-08-14)* | — |
| `game:rod-iron` → `nailplate` (flat 1.0, no crop) | **works** *(2026-08-14)* | — the one whole-piece conversion: the mill itself claims it, since the stage names a `code` |
| `castbillet` → `rolledrod` ×6 (grooved 2.25) | declared, unreachable | the **mid-gap crop**. 2.25 is a half-step, not a rung, and its six pieces each still need a pass — which is not the one-product-plus-remainder stroke the shear does. The billet's ladder is flat-only until that is ruled |
| `castbloom` → plate ×5 (wide 3.0) | declared, unreachable | its crop yields **stock**, not a product: five pieces to be finished on the narrow flat set. Same open question as the billet's |
| either slab → `heavyplate` | not declared | **M.8**, which merges `heavyplate` with `castplate-heavy` onto one metal axis. Shipping a rolled-only one now is the duplication that stage exists to remove |

⛔⛔ **Deferring `heavyplate` still leaves the cast-slab route half-walkable.** The forms landed, so the
slab now rolls and its 1.0 boilerplate crop works; what is missing is the **mandatory** 2.0 crop
([recoverability](../mechanics/recoverability.md)), so a player who rolls straight past it reaches 1.0 at
80 long against the 48 limit. M.8 is the whole of what is left there.

`ShippedCropTableTests` (iiex) and `ShippedCastCropTableTests` (siex) hold both tables to reaching real
rungs, naming real outputs and asking less drive than the mill does — the check the four dangling roll-set
output codes never had. The cast one additionally pins the blade tier and the merge: contributing both
tables gives ten rows, not five.

---

## The catalogue

Settled 2026-07-29, **built 2026-08-14**. Every mass is the density rule applied to the drawn section, and
every one is now a `const` in `RolledItemDefinitions` with the section in its comment.

| Item | Section × length | vx³ | Mass (u) | Mod | Made by | Consumed by |
|---|---|---|---|---|---|---|
| `game:rod-{metal}` | 2 × 2 × 10 | 40 | 100 | vanilla | shear crops the grooved 2.0 stage - 4 per `shingledbar`, 6 per `castbillet` | the mill again (the fork, below); it is `game:rod` rather than a drop-in for it |
| `stock-rod` (the work piece) | 2 × 2 × 10 | 40 | 100 | iiex | **ships 2026-08-14**: auto-emitted by `StockItemDefinitions` from the `Rod` `StockForm` | what a rod becomes on entering the mill - `MaxStackSize 1`, stage-rendered, carries heat. `StockForm.Feedstock` is the conversion, and it is why no consumer of `game:rod-*` had to change |
| `rivetrod` | 1 × 1 × 10 | 10 | 25 | iiex | shear crops the grooved 1.0 stage of a `rolledrod` - 4 per rod | the [rivet machine](../machines/heading-machine.md), which cuts and upsets it → rivets |
| `nailplate` | 4 × 1 × 10 | 40 | 100 | iiex | one `rolledrod` taken flat 1.5 → 1.0 - 1 per rod, no crop | [nail machine](../machines/nail-machine.md) → 4 × `game:metalnailsandstrips` |
| `beam` | 4.5 × 2 × 9 | 81 | 200 | iiex | shear crops the flat 2.0 stage in half - 2 per `shingledbar`, 3 per `castbillet` | fabricated steel frames (beam + plate + rivets); the iiex beam engine |
| `game:metalplate` | 9 × 1 × 9 | 81 | 200 | vanilla | shear crops the flat 1.0 stage - 2 per bar, 3 per billet, 5 per `castbloom` (crop 5 at 3.0, then narrow flat); or a `boilerplate` stamped on the hammer → 3 | everything - `ExIngredients.Plate` (`ExIngredients.cs:28-29`) |
| `blank` | 8 × 2 × 5 | 80 | 200 | iiex | wide 2.0 off `castbloom` - 5 per bloom | [boring machine](../machines/boring-machine.md): cranks, gear blanks |
| `skelp` | 8 × 1 × 10 | 80 | 200 | iiex | wide 1.0 off `castbloom` - 5 per bloom | [bending roller](../machines/bending-roller.md), conical → [rolled pipe](../machines/rolled-pipe.md) |
| `heavyplate` | 12 × 2 × 10 | 240 | 600 | iiex | wide 2.0 off either slab - 2 per `shingledslab`, 5 per `castslab` | the fabricated substitute for cast `castplate`: machine frames, hearth plating |
| `boilerplate` | 15 × 1 × 16 | 240 | 600 | iiex | wide 1.0 off either slab - 2 per `shingledslab`, 5 per `castslab` | boiler shells (RCC dual path, plate-or-boilerplate); or the hammer die → 3 plate |

`blank` and `skelp` are the same 80 vx³ at two gauges - one at 2 thick for machining, one at 1 thick for
curling into pipe. Five of each per bloom.

600 u is the wide tier's quantum, and it is what makes both slabs divide into both products at 2.0 and at 1.0:
shingled 1200 → 2, cast 3000 → 5. The crop is therefore a decision rather than a step - each of the five 600 u
pieces off a cast slab is either claimed as `heavyplate` or rolled on to 1.0 and claimed as `boilerplate`.

### Every crop point, and whether it is exact

Section here is what the stock actually is at that stage, before the crop. The two rows marked mandatory are
[recoverability](../mechanics/recoverability.md)'s and are listed for completeness, not claimed here.

| Stock | Set | Stage | Section × length there | Crop | Each piece | Product | Exact? |
|---|---|---|---|---|---|---|---|
| `shingledbar` 400 | grooved | 2.0 | 2 × 2 × 40.5 | ×4 | 2 × 2 × 10.125 | `rolledrod` | mass exact, length rounds 10.125 → 10 |
| `shingledbar` | flat | 2.0 | 4.5 × 2 × 18 | ×2 | 4.5 × 2 × 9 | `beam` | exact |
| `shingledbar` | flat | 1.0 | 9 × 1 × 18 | ×2 | 9 × 1 × 9 | `game:metalplate` | exact, vanilla geometry |
| `rolledrod` 100 | grooved | 1.0 | 1 × 1 × 40 | ×4 | 1 × 1 × 10 | `rivetrod` | exact |
| `rolledrod` | flat | 1.0 | 4 × 1 × 10 | - | 4 × 1 × 10 | `nailplate` | exact, whole piece |
| `castbillet` 600 | grooved | 2.25 (mandatory) | 2.25 × 2.25 × 48.0 | ×6 | finished to 2.0 individually | `rolledrod` | exact - 40.5 vx³ each |
| `castbillet` | flat | 2.0 | 4.5 × 2 × 27 | ×3 | 4.5 × 2 × 9 | `beam` | exact |
| `castbillet` | flat | 1.0 | 9 × 1 × 27 | ×3 | 9 × 1 × 9 | `game:metalplate` | exact - 27 = 3 × 9 |
| `shingledslab` 1200 | wide | 2.0 | 12 × 2 × 20 | ×2 | 12 × 2 × 10 | `heavyplate` | exact - this is where the drawn section comes from |
| `shingledslab` | wide | 1.0 | 15 × 1 × 32 | ×2 | 15 × 1 × 16 | `boilerplate` | exact |
| `castbloom` 1000 | wide | 3.0 | 5.33 × 3 × 25 | ×5 | → narrow flat to 1.0 | `game:metalplate` | mass exact (80 vx³ → 81 drawn) |
| `castbloom` | wide | 2.0 | 8 × 2 × 25 | ×5 | 8 × 2 × 5 | `blank` | exact |
| `castbloom` | wide | 1.0 | 8 × 1 × 50 | ×5 | 8 × 1 × 10 | `skelp` | exact - but 50 > 48, [B12](../mechanics/recoverability.md) |
| `castslab` 3000 | wide | 2.0 (mandatory) | 15 × 2 × 40 | ×5 | 15 × 2 × 8 | `heavyplate` | mass exact, section is not - see Gotchas |
| `castslab` | wide | 1.0 | 15 × 1 × 80 | ×5 | 15 × 1 × 16 | `boilerplate` | exact |
| `boilerplate` 600 | (die) | - | 15 × 1 × 16 | ×3 | 9 × 1 × 9 | `game:metalplate` | mass exact (3 × 200 = 600); 3 × 81 = 243 against 240 |

Dropped from the ladder: `game:metalsheet` and the 0.5 gap that would have made it. Sheets have no consumer
and plates do, so the finest gap goes on both flat families; the cost is that a vanilla cladding block
(23 metals × 6 faces of art) is left with no recipe anywhere. Reversible by re-adding 0.5 to the wide family
alone.

---

## The rod is the fork

> Settled 2026-08-05: the rod is vanilla's, and the identity splits by role. `iiex:rolledrod` is not created.
> The shear's claimed product is `game:rod-iron`; the re-rollable piece is `iiex:stock-rod`. Vanilla's rod is
> `2 × 2 × 10 = 100 u` exactly ([fasteners](fasteners.md)), `game:rod-iron` is already the mill's only output
> code that resolves, and 32 call sites across four mods ask for `game:rod-*` by name.
> The mill admits `game:rod-iron` at its deck and converts it to `iiex:stock-rod` on entry, so no consumer
> needs editing - and an anvil-made rod can be rolled to nail plate before the player owns a puddling furnace.

★★ **Built 2026-08-14, exactly as ruled.** `StockForm.Rod` (2 × 2 × 10, ceiling 4, e = 1) and its ladder
`mods/iiex/assets/iiex/config/processroutes/rod.json` ship, and the admission is `StockForm.Feedstock` — a table of
*offered code → the stock item it enters as*, applied by `BlockEntityRollingMill.Admit`. Keeping it as
codes rather than forms is what lets a caller admit their own feedstock without knowing a form exists.

★ **The fork is where drawn stage art earns its place.** The rod's ladder names a `shape` and a per-stage
`element`, so `ItemStockPiece` renders the authored `Grooved150` / `Flattened150` / `NailPlate` instead of
scaling the base — a groove is not a thinner rectangle, and this is the one family drawn at every rung.

One rod, four feeds, two products, chosen per rod: the set that is fitted decides what comes out.

| Route | Set | Gaps | Feeds | Comes out as | Then |
|---|---|---|---|---|---|
| round | `grooved`, barrel 16 | 1.5 → 1.0 | 4 | 1 × 1 × 40 | shear ×4 → 4 `rivetrod` @ 25 → rivet machine → rivets |
| flat | `flat`, barrel 4 | 1.5 → 1.0 | 4 | 4 × 1 × 10 | no crop → 1 `nailplate` @ 100 → nail machine → 4 `game:metalnailsandstrips` |

Both routes hold the rod at 10 long (the flat section law puts all reduction into width; the groove puts all
of it into length and then the crop takes it back), so both land on exactly 100 u. The pass counts are equal
because `sides = 1` on both - the rod is 2 wide entering and 4 wide leaving, and the narrow barrel is 4.

Nails come from plate, never from rod. A rod-drawn nail is the 1870s wire nail and is out of period; the
nail-machine reference is a cut-nail bench working a strip. The flat branch exists to make a small plate.

Nails and bolts are the iron fastener; rivets are the steam one, because a rivet joint is strong and tight, so
it arrives with the first thing that holds pressure.

⛔⛔ **The bench and the blank's name are contested, and the owner ruled on 2026-08-15: it is a rivet rod,
cut and upset by a rivet machine.** So the 25 u product is `iiex:rivetrod` (display "Rivet Rod"), and it is
named for what it feeds. The naming half is not a matter of taste - the piece is **a quarter of vanilla's
rod by section and by mass** (1 × 1 × 10 at 25 u against 2 × 2 × 10 at 100 u), so a bare `rod` puts two
items a player cannot tell apart beside each other in the handbook and the crop table.
[fasteners.md](fasteners.md)'s *"`rivetrod` is a dead name"* is struck by that ruling, and it agrees with
[STATE.md](../../internal/plans/STATE.md)'s own settled Fasteners row - *"the rivet machine cuts and upsets
rod @ 25 u"* - which the later heading-machine page had overwritten without noticing.

⛔ **What is still open is the bench, not the blank**: the ruling restores the rivet machine, while
[heading machine](../machines/heading-machine.md) owns the `ItemDie` contract and a bolt route that the
2026-07-30 Fasteners row ("no dies, no bolts") never had. That page's premise needs re-settling; the item
name does not wait on it.

---

## The rolled and the cast plate are one item

⛔ **Struck 2026-08-15 (owner): `castplate` was never a second item.** It is the old name of
`castplate-heavy`, so the 500 u 10 × 2 × 10 plate this section used to describe does not exist and should
not. The rolled route and the sand-cast route make **the same item at the same 600 u**, differing only in
metal — cast iron out of the casting cell, wrought or steel off the wide rolls — and the redrawn cast art
carries that mass (`item-sandcast-heavyplate.json`, 10 × 2 × 12 = 240 vx³ = 600 u, the rolled one's volume
exactly).

| | cast | rolled |
|---|---|---|
| Metal | cast iron | wrought / steel |
| Section | 10 × 2 × 12 | 12 × 2 × 10 |
| Mass | **600** | **600** |
| Made by | [casting cell](../machines/casting-cell.md), heavy-plate pattern | wide 2.0 off either slab |
| Role | structural only - it is cast iron | the same, plus everything cast iron is too brittle for |

★ Because the masses and the volumes agree, the two are interchangeable in most recipes and the metal is
what a recipe restricts when it needs to. Which recipes restrict is **deferred** until the recipe pass
(owner, 2026-08-15).

⛔ **What ships today is still two items**: `castplate-heavy` at **160 u** (`CastPartItemDefinitions.cs:18`)
and the rolled `heavyplate` at 600. M.8 is the merge — one item, a metal variant, the cast one re-massed
160 → 600 with its casting-pattern capacity following.

---

## Assets

Nothing in this family is exported to a runtime domain; every file below is an untracked editable source.

| Product | Drawn where | Element | Measures |
|---|---|---|---|
| `rolledrod` | `workbench/shapes/item-rod-rolled.json` | `RolledRod200` `:16-18` | 2 × 2 × 10 - exact |
| `rivetrod` | `workbench/shapes/item-rod-nail.json` | `NailRod1` `:16-18` + 3 children `:30-32`, `:44-46`, `:58-60` | 4 × (1 × 1 × 10) - the file name is stale, this is the rod bundle |
| `rivetrod` (in situ) | `item-rod-rolled.json` | `CutNailRod1` `:108-110` + 3 children | the same bundle drawn as the cut of the 1.0 grooved stage |
| `beam` | `workbench/shapes/item-beam-rolled.json` | `Beam` `:16-18` + child `:31-33` | 4.5 × 2 × 18, i.e. two beams - the planned rename is `CutBeam1` |
| `game:metalplate` | `item-beam-rolled.json` | `CutPlate1` `:140-142` + `CutPlate2` `:154-156` | 2 × (9 × 1 × 9) - vanilla geometry |
| `nailplate` | - | - | missing, and so are all four flat stages of the rod (1.75 / 1.5 / 1.25 / 1.0) |
| `blank` | - | - | missing |
| `skelp` | - | - | missing |
| `heavyplate` (rolled) | - | - | missing; `mods/iiex/assets/iiex/shapes/item/heavyplate.json` is the cast part at 12 × 2 × 12 |
| `boilerplate` | - | - | missing |

The rod's grooved stages are drawn - `Grooved175` (`item-rod-rolled.json:31-33`), `Grooved150` (`:46-48`
+ child `:61-63`), `Grooved125` (`:77-79` + child `:92-94`) - and conserve 40 vx³ to within 1.6 %. The fork
has art on the round branch and none on the flat one.

Every editable file references its texture by absolute local path
(`item-rod-rolled.json:12` → `F:/repos/…/sheet-plain/iron5`); export rewrites it to `game:`.

---

## Numbers

No mass on this page exists as a constant anywhere. The table below is what a caller would have to create. The
only related constants that ship are the cast part's, and they are the wrong item:

| Constant | Value | file:line | Note |
|---|---|---|---|
| `CastPartItemDefinitions.HeavyPlateUnits` | 160 | `mods/iiex/src/Items/CastPartItemDefinitions.cs:21` | the cast plate; settled 500, and this is not the rolled 600 |
| `CastPartItemDefinitions.CastBarrelUnits` | 200 | `:24` | cited for scale only |
| `ExIngredients.Plate(qty)` | `game:metalplate-*`, metal capture | `exlib/src/Definitions/ExIngredients.cs:28-29` | the consumer side of `game:metalplate` |
| `ExIngredients.Nails(qty)` | `game:metalnailsandstrips-*` | `:36-37` | what `nailplate` ultimately feeds |
| `ExIngredients.Rod(qty)` | `game:rod-*` | `:44-45` | what `rolledrod` is a drop-in for |

The four dangling output codes the roll sets already name - `iiex:rolledplate-iron`, `iiex:rolledsheet-iron`,
`iiex:wirerod-iron`, `iiex:nailrod-iron` - resolve to nothing; that fact and its cause (`TryParse` never
resolves a code) belong to [rolling mill](../machines/rolling-mill.md). Under the settled catalogue they map,
respectively, onto `game:metalplate`, deleted, `rivetrod`, and deleted - the `slitting` set that produced the last
one goes entirely.

The pass counts behind each product ("12 feeds to plate on narrow rolls against 8 on wide") are the pass
model's and are [rolling mill](../machines/rolling-mill.md)'s; the throughput comparison for the wide route
(1.9 passes per plate against 6.0) is [steam hammer](../machines/steam-hammer.md)'s and
[wide hall](../machines/wide-hall.md)'s.

---

## Gotchas

- `heavyplate`'s drawn section only matches the wrought route. The shingled slab at the 2.0 gap is 12 wide and
  20 long, so two 12 × 2 × 10 pieces fall out exactly - that is where 12 × 2 × 10 came from. The cast slab is
  capped at the barrel's `MaxWidth` 15 from its 3.0 gap on, so its 2.0 stage is 15 × 2 × 40 and its five crops
  are 15 × 2 × 8. Same 240 vx³, same 600 u, different section, and nothing in the design says which one the
  item takes. `boilerplate` has no such problem: both routes are 15 wide, so both give 15 × 1 × 16 exactly.
  [recoverability](../mechanics/recoverability.md)'s crop table gives the cast slab's 2.0 length as 37.5
  (which implies a 16-wide stage), not 40; the two cannot both be right.
- A crop is not a conversion. Taking one product off a piece leaves the remainder on the deck as stock at the
  same stage, which is what stops odd-sized and part-rolled pieces needing a rounding rule
  ([shear](../machines/shear.md)). The counts in the table above are what a whole piece yields, not a forced
  split.
- Stamping is not shearing. 15 × 16 does not partition into 9 × 9 by straight cuts, so `boilerplate` → 3 plate
  is die work on the [steam hammer](../machines/steam-hammer.md). The hammer never shears; the shear never
  blanks.
- `game:metalplate` is a vanilla item and stays one. Nothing here re-skins it - the mill route reaches the
  same object the anvil does, at the same 200 u, which is what makes the two routes comparable.
- `item-rod-nail.json` is the rod, not a nail rod. The name predates the rule that nails come from plate; the
  rename is queued with the art work. Anyone wiring art off the filename will wire the wrong product.
- `castbloom`'s `skelp` stage is 50 long. Five 8 × 1 × 10 skelps come out of it exactly, but the uncropped
  stage is past the 48-voxel limit - the hole in the invariant that
  [recoverability](../mechanics/recoverability.md) flags as its highest-value open item.
- ~~A product cannot be declared at a half-step today.~~ **Fixed 2026-08-12.** `RollSetSpec` no longer holds
  `gaps` or `outputs` at all: the states are the stock's process route, a half-step is an ordinary rung, and
  a stopping point is a stage carrying a `code`
  ([process-extension](../mechanics/process-extension.md)). Gauges match on a tolerance, not `==`. The
  shear's own crop table is a terminal registry and is still unshipped - see [shear](../machines/shear.md).
- The four grooved half-steps have art and the four flat ones do not, so the fork currently looks like two
  branches of unequal maturity when it is meant to be symmetric.

---

## Open

- Nothing is built. No item definition, no recipe, no lang key, no handbook page for any of the nine. The
  construction sequence is: `rolledrod` / `rivetrod` / `beam` first, then `rivet` + `nailplate`, then `boilerplate`
  + rolled `heavyplate` together with the cast plate's re-mass (160 → 500).
- The `heavyplate` section question above must be settled before the item is drawn, because the drawn section
  is what the mass is measured from.
- `blank`, `skelp`, `nailplate` and `boilerplate` have no art at all, and `nailplate` additionally has no
  stage art on the route that makes it.
- Consumers are named but not written. No recipe consumes `heavyplate` or `boilerplate`; the boiler's dual
  normal-plate-or-boilerplate RCC path is designed and unbuilt; `beam` is named for the iiex beam engine and
  nothing references it. The fabricated-substitute family (`castframe`, `castshell`, `castwheelsection`,
  `cast-barrel`) is the intended demand side and none of those recipes exist either.
- Whether a crop yields one product plus a remainder, or a full split in one action, is undecided - the crop
  table here reads as a split, the crop-not-convert rule reads as one at a time, and the code cannot do both
  by accident ([shear](../machines/shear.md)).
