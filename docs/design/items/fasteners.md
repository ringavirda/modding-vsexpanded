# Fasteners

**Status** partial - nails ship and are the most-demanded ingredient in the suite; ★ **the 25 u blank the
headed fasteners come from now exists and is obtainable** (`iiex:rivetrod`, four off a rolled rod at the
grooved 1.0 rung, 2026-08-14). The bolt, the rivet and the ball still do not exist in any form, and no
bench is built
**Mod** the items are vanilla (`game:`) plus two generated variants from exlib; the benches are
iiex (nail, rivet) · hpex (ball die). ⛔ Whether the rivet bench is a die-fed heading machine is **open**
again after the 2026-08-15 ruling - see Gotcha 4

**Owns**
* the fastener catalogue - nails-and-strips, bolt, rivet, bearing ball - and the 25 u rod all three
  headed fasteners come from;
* the 25 u anchor: the mass of one `metalnailsandstrips`, its two independent derivations from vanilla's
  own files, and the rule that no route may beat 4 nails per 100 u;
* the fastener-by-tier rule - nails and bolts are the iron tier's, rivets are the steam tier's - and its
  physical reason: a rivet joint is strong and tight, and nothing before the boiler needs tight;
* the rod fork - one 100 u `rolledrod`, the same four feeds either way, two fastener families out;
* the consumer census: every place in the repo that requires a fastener, and the fact that the only
  fastener with any consumer is the vanilla one;
* the two generated nail variants (`iiex:metalnailsandstrips-castiron`,
  `siex:metalnailsandstrips-bessemersteel`) and the fact that no recipe in the suite can accept either.

**Does not own — cited only, never restated**

| Fact | Owner |
|---|---|
| 1 vx³ = 2.5 u and the audit of every mass | [density rule](../mechanics/density-rule.md) |
| the nail bench, its mechanism, and the 1 `nailplate` → 4 nails conversion as a machine rate | [nail machine](../machines/nail-machine.md) |
| the `ItemDie` tooling contract, the die catalogue and which mod ships each die, the bench itself | [heading machine](../machines/heading-machine.md) |
| the ball die's spec, the chrome-steel chain, the bootstrap invariant | [bearings](../machines/bearings.md) |
| every crop - the shear owns the cut that turns a `rolledrod` into four rods | [shear](../machines/shear.md) |
| the pass model, `δ_max = μ²R`, the two-round rule, gaps and barrel widths | [rolling mill](../machines/rolling-mill.md) · [steel roll sets](../machines/steel-roll-sets.md) · [roll sets](roll-sets.md) |
| `rolledrod`, `nailplate`, `beam`, `boilerplate` as rolled products | [rolled parts](rolled-parts.md) |
| where rivets are spent - riveted shells, barrels, rims, girders | [bending roller](../machines/bending-roller.md) · [cast parts](cast-parts.md) |
| the placement rule and the D2/N3 decisions | [STATE.md](../../internal/plans/STATE.md) |
| code-first defs, ingredient helpers as a system, the cost catalogue | [recipes & config](../mechanics/recipes-config.md) |

**Depends on** [density rule](../mechanics/density-rule.md) · [nail machine](../machines/nail-machine.md) ·
[heading machine](../machines/heading-machine.md) · [shear](../machines/shear.md) ·
[rolling mill](../machines/rolling-mill.md) · [rolled parts](rolled-parts.md) ·
[cast parts](cast-parts.md) · [bearings](../machines/bearings.md) · [STATE.md](../../internal/plans/STATE.md)

---

## Role

`ExIngredients.Nails` is reached for by name in machine after machine: a fastener appears in 36 ingredient
sites across all four mods. Every one consumed today is `game:metalnailsandstrips-*`, hand-forged on an
anvil, four at a time; there is no bolt, no rivet and no machine route.

The design does not raise the yield. Vanilla's rate is exactly 25 u of iron per nail bundle and the ladder
sits on that ceiling: the mechanised route contains no anvil work at all - puddle, roll, crop, head.

### The tier rule

Nails and bolts are the iron tier's fastener; rivets are the steam tier's. The reason is physical: a rivet
makes a joint that is strong **and tight**; nails and bolts are strong but not tight. A boiler is riveted
because it must hold steam; a flywheel is bolted because it merely must not fall apart. So the rivet
arrives with the first thing that holds pressure - iiex's boiler - and not one step earlier. iiex machines
that might want one use nails or bolts and accept rivets later through the standard RCC dual path
([STATE.md § Fasteners](../../internal/plans/STATE.md)).

That rule places the dies, not the bench: a machine in iiex would strand iiex's own rod, so there is one
bench and the die is the difference - see [heading machine](../machines/heading-machine.md), which owns the
die catalogue.

---

## The catalogue

### Live

| Item | Section × length | vx³ | Mass (u) | Made by | Consumed by |
|---|---|---|---|---|---|
| `game:metalnailsandstrips-{metal}` | a bundle, no single section | - | 25 | vanilla anvil, 36-voxel plan | 36 sites - see the census |
| `game:rod-{metal}` | 2 × 2 × 10 | 40 | 100 | vanilla anvil, 40-voxel plan; also the mill's `grooved` 1.0 gap (`RollSetItemDefinitions.cs:90`) | 27 sites |
| `iiex:metalnailsandstrips-castiron` | vanilla shape | - | - | exlib metal-family emitter (`MetalFamilyEmitter.cs:458`, opted in at `assets/iiex/config/metals/castiron.json:9`) | nothing can accept it |
| `siex:metalnailsandstrips-bessemersteel` | vanilla shape | - | - | ditto (`assets/siex/config/metals/bessemersteel.json:9`) | nothing can accept it |
| `iiex:rod-castiron` · `siex:rod-bessemersteel` | vanilla `game:item/rod` shape | 40 | - | ditto (`MetalFamilyEmitter.cs:384`) | nothing can accept them |

### Settled, not built — nothing below exists in `src/`

| Item | Section × length | vx³ | Mass (u) | Made by | Consumed by |
|---|---|---|---|---|---|
| **rod** (the headed-fastener feedstock) | 1 × 1 × 10 | 10 | 25 | [shear](../machines/shear.md) crop, 4 per `rolledrod` | the heading bench |
| **bolt** | - (a bundle, like nails) | - | ≤ 25 per rod, mass-neutral | heading bench + bolt die (iiex) | plated pipe - see Gotcha 6 |
| **rivet** | - (a bundle) | - | mass-neutral | heading bench + rivet die (iiex) | boiler shells · cast pipe · every fabricated substitute |
| **bearing ball** | - | - | - | heading bench + ball die (hpex) | [bearings](../machines/bearings.md) |
| `nailplate` | 4 × 1 × 10 | 40 | 100 | mill, `flat` 1.5 → 1.0 | the nail bench → 4 nails |
| `rolledrod` | 2 × 2 × 10 | 40 | 100 | mill, `grooved` 2.0 | the shear → 4 rods |

---

## Numbers

### The 25 u anchor, twice over

Two independent vanilla sources give the same figure, so it is fixed rather than chosen:

| Source | Reading | File |
|---|---|---|
| the anvil plan | a single-layer 6 × 6 = 36-voxel pattern, inside one ingot's 42 → 1 ingot (100 u) → 4 sets ⇒ 25 u each | `D:/Gaming/Others/Vintagestory/assets/survival/recipes/smithing/nails.json:4-14` |
| the smelt-back | `smeltedRatio: 4` - four bundles melt to one ingot ⇒ 25 u each | `…/assets/survival/itemtypes/resource/metalnailsandstrips.json:102-107` |

exlib's generated nails copy the second exactly (`smeltedRatio = 4`,
`src/ExpandedLib/Metals/MetalFamilyEmitter.cs:471`), so the anchor holds for mod metals too.

Rod is 100 u on the same two readings: the vanilla plan is 2 layers × 2 × 10 = 40 voxels, inside one ingot
(`…/smithing/rod.json:3-9`), and the generated rod declares `smeltedRatio = 1` (`MetalFamilyEmitter.cs:395`).
It is also the number that made the density rule land on 2.5 rather than the measured 2.450 - 40 vx³ × 2.5 =
100 exactly ([density rule § Why 2.5](../mechanics/density-rule.md)).

The second vanilla nail plan is not a mint. `nails.json:18-31` is a 9 × 6 = 54-voxel pattern outputting
8 bundles; 54 is past one ingot's 42, so it costs two ingots - the arithmetic
[density rule](../mechanics/density-rule.md) applies to the 81-voxel plate plan (`density-rule.md:73-76`) -
giving 200 u → 8 bundles = 25 u each, the same rate. `nail-machine.md:28` reads it as "200 u out of a
100 u ingot" and warns against copying it; that reading is wrong.

### The rod fork

One `rolledrod` at 100 u, the same four feeds either way, and the player chooses per rod:

| Branch | Set · gaps | Feeds | Yields | Mass out | Bench | Fastener |
|---|---|---|---|---|---|---|
| grooved | `grooved` 1.5 → 1.0 | 4 | 4 `rivetrod` @ 1 × 1 × 10 | 4 × 25 = 100 u | rivet | rivets |
| flat | `flat` 1.5 → 1.0 | 4 | 1 `nailplate` @ 4 × 1 × 10 | 100 u | nail | 4 nails-and-strips @ 25 |

Both branches conserve mass exactly and cost the same labour, which makes it a choice rather than a ladder.
The pass arithmetic behind "four feeds" (a gap is two rounds; a round is one feed per side) belongs to
[rolling mill](../machines/rolling-mill.md).

Nails come from plate, never from rod. A rod-fed nail is the 1870s wire nail - a different machine and far
out of period. The rod branch is headed, not sheared.

### Consumer census

Every fastener requirement in the repo. All of them name the vanilla item.

| | Grid ingredient sites | RCC stage sites | Total |
|---|---|---|---|
| nails (`Nails` / `NailsSteel` / `RequireMetalNails`) | 21 | 15 | 36 |
| rod (`Rod` / `RodSteel` / `RequireMetalRod`) | 13 | 14 | 27 |
| bolts · rivets · balls | 0 | 0 | 0 |

Representative and largest bills:

| Consumer | Nails | Rods | file:line |
|---|---|---|---|
| Bessemer converter (3 RCC stages) | 42 | 18 | `SteelmakingExpanded/BlockStructures/Converter/Blocks/BlockConverterBessemer.cs:102`, `:109`, `:124`, `:103`, `:125` |
| Lancashire boiler (3 stages) | 24 | - | `HighPressureExpanded/…/BlockBoilerLancashire.cs:146`, `:153`, `:158` |
| Cornish engine (5 stages) | 14 | 42 | `HighPressureExpanded/…/BlockEngineCornish.cs:64-90` |
| Cornish boiler (3 stages) | 16 | 8 | `IronIndustryExpanded/…/BlockBoilerCornish.cs:132-145` |
| Watt engine (5 stages) | 12 | 24 | `IronIndustryExpanded/…/BlockEngineWatt.cs:54-76` |
| each iiex pipe segment ×4 | 1 | - | `IronIndustryExpanded/Recipes/Grid/PipeRecipeDefinitions.cs:25`, `:34`, `:43`, `:52` |
| tall hopper | 1 | - | `IronIndustryExpanded/Recipes/Grid/FurnaceRecipeDefinitions.cs:60` |
| plated molten barrel | 4 | - | `IronIndustryExpanded/Recipes/Grid/MoltenRecipeDefinitions.cs:34` |

### Ingredient helpers

| Helper | Resolves to | file:line |
|---|---|---|
| `ExIngredients.Nails(qty)` | `game:metalnailsandstrips-*`, metal-captured | `src/ExpandedLib/Definitions/ExIngredients.cs:36-37` |
| `ExIngredients.NailsSteel(qty)` | `game:metalnailsandstrips-steel` | `:40-41` |
| `ExIngredients.Rod(qty)` / `RodSteel(qty)` | `game:rod-*` / `game:rod-steel` | `:44-45` / `:48-49` |
| `ConstructionStages.RequireMetalNails(domain, qty)` | `metalnailsandstrips-*`, `storeWildCard: "metal"`, `allowedVariants: ["iron","steel"]` | `src/ExpandedLib/Definitions/ConstructionStages.cs:109-110` → `:120-132` |
| `…RequireMetalRod(domain, qty)` | `rod-*`, same restriction | `ConstructionStages.cs:114-115` |

---

## Assets

No fastener has art of its own. Nails and rod both reuse vanilla
(`game:item/resource/metalnailsandstrips`, `game:item/rod`); a `rolledrod` drawn at vanilla `rod` geometry
is a literal drop-in.

| Asset | Path | State |
|---|---|---|
| the rod schedule | `assets/editable/shapes/item-rod-rolled.json` | untracked |
| the four cut rods | `assets/editable/shapes/item-rod-nail.json` | untracked |
| runtime exports | `assets/iiex/shapes/…` | none - neither file has ever been exported |
| `nailplate` art | - | does not exist anywhere |
| bolt · rivet · ball art | - | does not exist anywhere |
| textures | both editable files declare `iron5 → F:/repos/modding-vsexpanded/.game/1.22/assets/survival/textures/block/metal/sheet-plain/iron5` | the editable-folder convention: an absolute authoring path the export rewrites |
| reference plates | `assets/editable/refs/rivetsnails/` - the 1867 machine-tool plate (Figs 1–5), the 1795 Perkins cut-nail engraving, a wire-nail machine, Tweddell's portable hydraulic riveter, a cut-nail photograph | the whole `refs/` folder is untracked |

`item-rod-rolled.json` is mass-conserving through the whole schedule, and is the only drawn stock in the
repo that is:

| Element | Section × length | vx³ | Rule says |
|---|---|---|---|
| `RolledRod200` | 2 × 2 × 10 | 40.0 | 100 u - `game:rod` exactly |
| `Grooved175` | 1.75 × 1.75 × 13 | 39.8 | 100 |
| `Grooved150` (+ 1 division) | 1.5 × 1.5 × 9 × 2 | 40.5 | 100 |
| `Grooved125` (+ 1 division) | 1.25 × 1.25 × 13 × 2 | 40.6 | 100 |
| `CutNailRod1`–`4` | 1 × 1 × 10, four of them | 40.0 | 4 × 25 u |

The element names are stale. `CutNailRod1…4` and the filename `item-rod-nail.json` both say nail, but under
the settled rule the 25 u piece goes to the rivet bench and nails come from plate
([STATE.md § Fasteners](../../internal/plans/STATE.md)) - so the rename target is **`rivetrod`**, which is
also what the *other* drawing of the same bundle already calls it (`CutRivetRod1…4` in
`item-rolled-rod.json`, exported as `iiex:item/rolled-rivetrod` and shipped as the item's shape).

**Lang / handbook**: no key and no page for any fastener in any mod; nails and rod inherit vanilla's.

---

## Code

There is no fastener code. What exists is the demand side.

| Piece | Where | State |
|---|---|---|
| `ExIngredients.Nails` / `NailsSteel` / `Rod` / `RodSteel` | `src/ExpandedLib/Definitions/ExIngredients.cs:35-49` | live - the shared grid helpers |
| `ConstructionStages.RequireMetalNails` / `RequireMetalRod` | `src/ExpandedLib/Definitions/ConstructionStages.cs:107-115`, shared body `:120-132` | live - the shared RCC helpers |
| `MetalFamilyEmitter.Nails` / `.Rod` | `src/ExpandedLib/Metals/MetalFamilyEmitter.cs:458` / `:384` | live - generates the two unusable variants |
| `RollSetItemDefinitions.Sets["grooved"]` | `src/IronIndustryExpanded/BlockStructures/Forming/RollSetItemDefinitions.cs:86-93` | live def, unreachable - see Gotcha 7 |
| ~~`RollSetItemDefinitions.Sets["slitting"]`~~ | — | **retired 2026-08-12**, with its three lang rows - see Gotcha 8 |
| `ItemDie` (the spec), `DieItemDefinitions`, `BlockEntityDieBench` | *(proposed)* `…/BlockStructures/Forming/` | nothing - [heading machine § Code](../machines/heading-machine.md) owns the plan |
| bolt / rivet / ball items | - | nothing |
| `nailplate` item | - | nothing |

---

## Gotchas

1. **The two generated nail variants can never be used.** `iiex:metalnailsandstrips-castiron` and
   `siex:metalnailsandstrips-bessemersteel` are emitted as real items, but every consumer asks either
   `game:metalnailsandstrips-*` (`ExIngredients.cs:37` - the domain is explicit) or the RCC's domain-less
   `metalnailsandstrips-*` restricted to `allowedVariants: ["iron","steel"]`
   (`ConstructionStages.cs:110`, `:131`). Neither can match. The same holds for `iiex:rod-castiron` and
   `siex:rod-bessemersteel`.
   Cast-iron nails are physically absurd anyway - brittle metal, and the emitter's own comment says so
   while emitting them (`MetalFamilyEmitter.cs:459-460`: "a cast/brittle alloy is never tong-worked").
   The Bessemer-steel ones are exactly what the steel tier wants and only need the wildcard widened.

2. **The `metal` capture means the fastener chooses the machine's metal.** `Nails(qty)` names its ingredient
   `metal` (`ExIngredients.cs:37`) and `RequireMetalNails` stores `storeWildCard: "metal"`
   (`ConstructionStages.cs:130`), so which nails go in decides what later stages and drops resolve to.
   Widening a fastener wildcard is never a purely cosmetic change.

3. **The 8-set vanilla plan is not a mint** - see Numbers. `nail-machine.md:28` still says it is.

4. ~~**`rivetrod` is a dead name.**~~ ⛔⛔ **Struck by the owner, 2026-08-15: it is the correct name.** The
   1.0 grooved product is `iiex:rivetrod` at 25 u, cut and upset by a **rivet machine** - which is what
   `STATE.md`'s settled Fasteners row said all along, and its tier diagram needs no correction. The
   argument this row made ("a plain rod") also missed the thing that decides it: the piece is **a quarter
   of vanilla's rod by section and by mass** (1 × 1 × 10 at 25 u against 2 × 2 × 10 at 100 u), so a bare
   `rod` ships two items a player cannot tell apart. ⛔ What the ruling leaves open is the **bench**:
   [heading machine](../machines/heading-machine.md) owns an `ItemDie` contract and a bolt route that the
   2026-07-30 row's "no dies, no bolts" never had, and the two cannot both stand.

5. **Nails, bolts and rivets are not interchangeable in the fiction and must not become so in code.** The
   RCC dual path exists so an iiex machine can later accept rivets as an alternative - never so that a
   rivet silently substitutes for a nail in a joint that has to be tight.

6. **The naming half closed 2026-08-05 - but the bolt still has no consumer.** The tier was renamed
   `bolted` → `plated`, so the name no longer promises a fastener the recipe does not use: a plated pipe
   made from a plate and nails is self-consistent, and "bolted" only ever named the flange joint, which
   the `flanged` joint-family already records - and which the cast tier shares, so it never distinguished
   the tier at all.
   All four iiex segments still require `Nails(1)` (`PipeRecipeDefinitions.cs:25`, `:34`, `:43`, `:52`),
   and bolts have no consumer anywhere. That is an open question - what should cost bolts? - rather than a
   naming inconsistency to be cleaned up by retargeting this recipe
   ([heading machine § Gotchas](../machines/heading-machine.md)).

7. **The `grooved` set cannot bite fresh stock** - its first gap is 1.0 against 3.0 stock
   (`RollSetItemDefinitions.cs:89`), a 2.0 draft against `δ_max = 1.0`. That is blocker B4, and it sits
   directly upstream of every headed fastener. The settled schedule is 1.5 → 1.0, which the shipped def
   does not have.

8. ~~**The `slitting` set is dead twice over.**~~ **Retired 2026-08-12.** It accepted `"plate"`, which is
   not a `StockForm`, and named an output that did not exist. The slitting mill was also the wrong route
   under the settled design, which sends nails through plate on the `flat` set - so it was deleted rather
   than fixed, and no migration was written: a retired code must not be remapped onto a surviving one.

9. ~~**Four of the five roll-set output codes do not exist.**~~ Gone with `Outputs` on 2026-08-12: a roll
   set no longer names a product at all. What a stage becomes is the stock's own ladder, and no shipped
   rung names a `code` yet, because every shipped stopping point is a shear crop.

10. **No stock item carries a `stockForm` on its stack**, so nothing can be fed to the mill at all
    (blocker B3, [nail machine § Gotchas](../machines/nail-machine.md)). Both fork branches are behind it.

11. **Rivets do not exist, so no fabricated substitute is buildable.** Every entry in
    [cast parts § Fabricated substitutes](cast-parts.md) costs rivets as an ingredient - that is the design
    that avoids needing a riveting machine, and it makes the rivet a hard prerequisite for the whole steel
    structural tier.

12. **A fastener bundle is a bundle, not a piece.** Vanilla's `metalnailsandstrips` is one stack entry worth
    25 u of iron and an unstated number of nails; whatever the bolt and rivet items become, they must follow
    that convention or the mass ledger stops closing.

---

## Open

1. **Nothing on the settled side is built** - no rod at 25 u, no `nailplate`, no bolt, no rivet, no ball, no
   die item, and neither bench.
2. **What a "bolt" is as an item** - one bolt, a bundle, or a `bolts-and-nuts` composite in the shape of
   vanilla's `metalnailsandstrips`. The vanilla precedent argues for a bundle, and the mass ledger then fixes
   the count ([heading machine § Open](../machines/heading-machine.md)).
3. **How many fasteners one 25 u rod heads.** The heading bench's proposed die `count` is 1 per rod and
   mass-neutral; nothing has checked that against the nail branch's 4-per-100 u, which is the only anchor
   either branch has.
4. **Whether the generated variants get a use or get deleted** (Gotcha 1). Widening the nail wildcard to
   accept `*:metalnailsandstrips-*` would make Bessemer nails work and cast-iron nails work too, which is
   wrong; a per-metal `allowedVariants` list is the likelier answer.
5. **Retargeting plated pipe onto bolts** (Gotcha 6) - the smallest change that gives the bolt a reason to
   exist, and it also removes an iiex recipe's dependence on a fastener the player must hand-forge.
6. **Whether rivets ever become craftable by hand.** An older note filed "bolts, nuts, studs, rivets" under
   anvil-forged; the settled design gives them a machine only. A hand fallback would soften the boiler gate
   but weakens the tier rule.
7. **`STATE.md`'s tier diagram still draws a `rivet machine`** (Gotcha 4) - correct it when the machines
   index is written.
8. **The two editable rod shapes are untracked, unexported and misnamed** - the cheapest unblocking work in
   the family, and it has to happen before either bench can draw anything.
