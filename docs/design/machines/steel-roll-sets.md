# Steel roll sets

**Status** ★★ **partly overtaken 2026-08-14.** The cast forms, their ladders and their crop rows are
**built** and siex-owned, so cast stock rolls today on iiex's three sets. ⛔⛔ **The per-gap wide sets this
page proposed are cancelled** (owner, 2026-08-14): the wide stand's top roller is the movable one and the
player sets the gap by working the mill's raise/lower cells, so **one wide roll set covers every wide gap**
and there is no `rollset-flatwide35` / `-flatwide30` to build. What is genuinely unbuilt here is the steel
**narrow** sets and the tier gate itself: `MinTorque` is still parsed, stored, validated and never read
**Mod** siex (`SteelIndustryExpanded`) - the sets and the cast forms. The mill block, the hall and the
cast stock items all stay iiex.

**Owns**

* what a steel roll set is in this suite - a torque grade, not a material grade - and why the name has to
  be read carefully;
* the two extra wide gaps, 3.5 and 3.0: why 4-thick cast stock needs them, that they extend iiex's hall
  rather than replace it, and that they are **rungs on a ladder rather than sets of their own**;
* the steel narrow sets that let `castbillet` run the iiex mill's own barrel;
* the `MinTorque` tier as the steel line's only gate, what it must be wired into, and why it is vapour
  today;
* the settled rejections in this area - the `shape` and `wire` sets, and the automation upgrade;
* the smex-side build list.

**Does not own** - cited only, never restated

| Fact | Owner |
|---|---|
| the mill block, its 3 × 3 × 2 footprint, the axle bus, placement/break/drops, the pass lifecycle, `RollingPass` physics (`δ_max = μ²R`, spread, elongation, cooling), the `rollset` spec format and `TryParse`'s rules, the shipped four-set catalogue, `WorkPiece`, `StockForm`, every `Rolling*` key, its blockers and gotchas | [rolling mill](rolling-mill.md) |
| the hall as a build (N ordinary mills on one shaft), the wide family's shared schema - one gap per item, barrel 16, `MaxWidth` 15 - the four iiex sets, the drawn-gap-is-the-modelled-gap table, the shared-shaft mechanics and the hall's power profile | [wide hall](wide-hall.md) |
| the energy model, `LoadTorque`, the four node contracts, every `Mp*` key - what a torque number means | [mp-energy](../mechanics/mp-energy.md) |
| the drive that turns the stands | [flywheel & shafting](flywheel-and-shafting.md) |
| every crop in the ladder, and the `Outputs` → stage move | [shear](shear.md) |
| blanking / stamping the rolled plate | [steam hammer](steam-hammer.md) |
| casting the cast stock, the cavity dimensions and the lane counts | [long cell](long-cell.md), [casting cell](casting-cell.md) |
| 1 vx³ = 2.5 u | [density rule](../mechanics/density-rule.md) |
| the ≤ 32 / ≤ 48 handling limits and the soft-locks | [recoverability](../mechanics/recoverability.md) |
| keeping stock hot between stands | [reheat furnace](reheat-furnace.md), [heat balance](../mechanics/heat-balance.md) |
| bending - a different operation, and iiex's | [bending roller](bending-roller.md) |
| code-first defs, recipes, the cost catalogue, goldens | [recipes & config](../mechanics/recipes-config.md) |

**Depends on** [rolling mill](rolling-mill.md) · [wide hall](wide-hall.md) ·
[mp-energy](../mechanics/mp-energy.md) · [shear](shear.md) · [long cell](long-cell.md) ·
[reheat furnace](reheat-furnace.md) · [recipes & config](../mechanics/recipes-config.md) ·
[rolling](../processes/rolling.md) · `../STATE.md` § Placement rule

---

## Role

Steel changes exactly two things about rolling:

| Change | Consequence |
|---|---|
| cast stock is 4 thick, not 3 | the wide train needs two more gaps at the front - 3.5 and 3.0 - before the first iiex stand can bite |
| cast steel is harder | the sets need more drive behind them - `MinTorque` (`RollSetItemDefinitions.cs:11-13`) |

Nothing about the machine changes: one mill block, used by three tiers, distinguished only by the tooling
fitted to it and how many of them are built. smex adds items, not machinery, with the single exception of
two more copies of a block iiex already ships.

"Steel roll set" names the stock, not the roll. The rolls stay chilled cast iron in every tier: a roll
takes steady compression, not shock, so it is cast rather than forged, and harder material tiers gate on
`MinTorque`, not on the roll's own material (`RollSetItemDefinitions.cs:11-13`, echoed at
`RollSetSpec.cs:30`). A steel set is a heavier set turned by a bigger plant, not a set made of steel -
`wide-flat` "is `flatwide` with a different `minTorque`".

The steel tier therefore costs no new simulation: the pass model is the same relation read at a harder
setting, which is R5 - gate efficiency, not possibility (`../conventions.md`).

---

## Structure

A roll set has no structure - it is an item. The machine it fits is
[rolling mill](rolling-mill.md)'s; the row of machines it fits into is [wide hall](wide-hall.md)'s.

What smex adds structurally is two more stands bolted onto the front of iiex's hall - two ordinary
`BlockRollingMill`s on the same drive shaft. No new block, no new footprint, no new block entity.

### The extended train

⛔ **"Ships with" is now about the ladder, not the tooling.** All six gaps are the one `flatwide` set at
six settings; what siex contributes at the front two is the two cast forms that declare 3.5 and 3.0 as
rungs, and a stand a player builds a sixth copy of.

| Stand | Gap | Rung comes from | Enters | Notes |
|---|---|---|---|---|
| 1 | 3.5 | siex (cast ladders) | `castbloom` 4 × 4, `castslab` 12 × 4 | built 2026-08-14 |
| 2 | 3.0 | siex (cast ladders) | - | built; also the stand `castbloom` is cropped at ([wide hall](wide-hall.md) § What comes off the end) |
| 3 | 2.5 | iiex | `shingledslab` 8 × 3 | art not drawn |
| 4 | 2.0 | iiex | - | drawn: `item-rollers-flatwide20.json` |
| 5 | 1.5 | iiex | - | drawn: `item-finished-rollers-flatwide15.json` |
| 6 | 1.0 | iiex | - | drawn: `item-finished-rollers-flatwide10.json` |

The entry gap is set by the stock's thickness, so the upgrade is an extension and no stand in the line
ever becomes obsolete. An iiex player's four stands keep doing what they did; smex's two sit in front of
them.

`castbillet` never sees the train. At 3 × 3 it is narrow stock and runs the iiex mill's grooved barrel
with a steel set, which is why the steel families are not wide-only.

---

## Assets

No steel-specific art is needed, and none exists. A roll set's art is its gap, and the gap is a number, so
the two smex sets need the same kind of shape the four iiex ones need. The gap-drawn-is-gap-modelled
correspondence is [wide hall](wide-hall.md)'s.

| Asset | Path | State |
|---|---|---|
| ~~wide roll art, 3.5~~ | - | no longer owed: one wide set at six settings, not six items |
| ~~wide roll art, 3.0~~ | - | " |
| cast roll blank | `assets/editable/shapes/item-sandcast-rollers-blank.json` | drawn - the roll is a cast part, so this is where a set's recipe should start |
| narrow set art | `item-finished-rollers-flat.json`, `item-finished-rollers-grooved.json` | drawn, unwired |
| item shape actually shipped | `game:item/ingot` | `RollSetItemDefinitions.cs:122` - every roll set renders as an ingot |
| cast stock item shapes | `assets/iiex/shapes/item/cast{billet,bloom,slab}.json` + 17 stage shapes under `forming/` | **ship, at the settled sections** (2026-08-14). They stayed in the iiex domain, because the item they draw is iiex's - this page owns the forms, not the pieces |
| handbook page | `docs/siex/handbook/` | stops at `04-bessemer.html` |

Caution: a steel set is visually indistinguishable from an iron one. Both render `game:item/ingot`
(`RollSetItemDefinitions.cs:122`), and the fitted set is legible only from block info
([rolling mill](rolling-mill.md) § Assets). Shipping a second family on the same placeholder makes the
mis-fit failure silent, and mis-fitting is possible in a way it never was at iron tier.

---

## Construction

There is no recipe - for any roll set, in any tier. This is a blocker.

Every shipped set is creative-only (`RollSetItemDefinitions.cs:127`, `.CreativeCommon("*")`), there are no
recipe assets in the repo, and `SiexRecipeConfig.Defaults()` - the hand-maintained list of every grid and RCC
recipe smex ships - has no roll-set row (`SiexRecipeConfig.cs:45-70`)
([recipes & config](../mechanics/recipes-config.md)).

The route the art already implies: the roll is a cast part, `item-sandcast-rollers-blank.json` exists as a
drawn blank, and the sand cell casts blanks, so a set should be cast then finished, not forged. That is the
[casting cell](casting-cell.md)'s job and it costs no new mechanic.

The economy question is smex's, not iiex's, and it is unanswered. iiex already owes an answer for four
stands ([wide hall](wide-hall.md) § Construction); smex is asking for two more mills plus their sets on
top.

---

## Operation

Every verb, refusal message, stall rule and wrench recovery is [rolling mill](rolling-mill.md)'s. Fitting a
steel set is fitting a roll set: right-click the machine holding any collectible carrying a `rollset`
attribute (`BlockRollingMill.cs:293-320`) - the gate never looks at the item's domain, which is what makes
a `siex:` set legal at an `iiex:` mill without a line of new code.

### What each stock runs on

The schedules - which stock enters which set at which gap, how many feeds, and what each stage lands on -
are [rolling](../processes/rolling.md)'s. What this page fixes is the family split: `castbillet` is narrow
stock and runs the iiex mill's own grooved barrel with a steel narrow set; `castbloom` and `castslab` are
wide stock and enter the hall's six-stand train at 3.5. Masses, crop points and what each stage is claimed
as belong to [rolling](../processes/rolling.md) and the [shear](shear.md).

### The gate

The only thing separating a steel set from an iron one at the moment of use is `MinTorque` - the drive
torque the stand needs before the set will turn at all (`RollSetSpec.cs:30`, `:37`). It is the same idiom
that is meant to gate the [shear](shear.md)'s cold cut, and it makes the steel tier a power achievement
rather than a tier unlock: continuous, no second block, no mod dependency.

Caution: it does not work today. `MinTorque` has no production call site anywhere in `src/` - see
[Numbers](#numbers).

---

## Numbers

### The proposed steel catalogue *(nothing is in code)*

The wide family's shared schema - one gap per item, `barrelWidth` 16, `MaxWidth` 15 - is
[wide hall](wide-hall.md)'s and is not restated here. What follows is only the smex entries.

⛔⛔ **The first three rows are cancelled, and the resolution was the third one.** A wide set is not one
gap: its top roller moves, so iiex's single `flatwide` covers 3.5 and 3.0 as ordinary rungs the moment a
form declares them, which is what shipped on 2026-08-14 — `flatwide.accepts` gained `castbloom` and
`castslab`, and the two cast ladders declare 3.5 down to 1.0. No wide steel set is owed.

| Set (proposed) | Family | Gaps | Accepts | `MinTorque` (proposed) | Anchor |
|---|---|---|---|---|---|
| ~~`rollset-flatwide35`~~ | ~~flat~~ | ~~3.5~~ | ~~`castbloom`, `castslab`~~ | — | cancelled: one wide set, movable roller |
| ~~`rollset-flatwide30`~~ | ~~flat~~ | ~~3.0~~ | ~~`castbloom`, `castslab`~~ | — | cancelled, same |
| ~~steel `flatwide`~~ | flat | as iiex's | + the two cast wide forms | — | **this is what happened**: iiex's own set was re-`accepts`-ed |
| `rollset-grooved-steel` | grooved | 2.5 / 2.0 / 1.5 / 1.0 | `castbillet` | 0.6 | 2 × the shipped `grooved` 0.3 (`:92`) |
| `rollset-flat-steel` | flat | 2.5 / 2.0 / 1.5 / 1.0 | `castbillet` | 0.4 | 2 × the shipped `flat` 0.2 (`:68`) |

The two narrow rows stand, and they are now the whole of this page's build list. The billet runs on iiex's
`flat` today, which is the iron-tier set — that is the tier gate missing, not the route.

Caution: the ×2 is a placeholder, not a calibration. The field is unread, so no shipped behaviour depends
on any value. The only real anchor is the drive side - one bridged waterwheel leaves roughly 0.4 N·m of
headroom and a hot fresh-bloom pass costs about 0.338 N·m ([rolling mill](rolling-mill.md) § Worked pass,
[mp-energy](../mechanics/mp-energy.md)). A steel tier that means anything must sit above what one
waterwheel carries and below what a steam engine plus a large flywheel does.

### Shipped `MinTorque` — the delta this page is the difference from

Values owned by [rolling mill](rolling-mill.md) § Shipped roll-set catalogue; repeated here as the one column
the steel tier changes.

| Shipped set | `MinTorque` | file:line |
|---|---|---|
| `flat` | 0.2 | `RollSetItemDefinitions.cs:68` |
| `flatwide` | 0.5 | `:80` |
| `grooved` | 0.3 | `:92` |
| ~~`slitting`~~ | ~~0.4~~ | the set was retired on 2026-08-12 |

### Hard-coded / structural constraints on any new set

| Constraint | file:line | Consequence for a steel set |
|---|---|---|
| `MinTorque` is never read | declared `RollSetSpec.cs:37`, parsed `:198`; no production caller | the steel tier's only gate is inert. A steel set fitted to a hand-fed waterwheel mill rolls exactly as fast as an iron one |
| gaps must strictly descend | `RollSetSpec.cs:144-157` | fine for single-gap wide sets; the narrow steel sets must carry the settled 2.5 / 2.0 / 1.5 / 1.0 in order |
| every output gap must be one of `gaps` | `:172-175` | blocks `castbillet`'s crop at the 2.25 half-step. `Outputs` must move to the [shear](shear.md) and key on stage before steel sets are authored |
| `barrelWidth` required > 0 | `:185-190` | a wide set that omits it fails validation at `AssetsFinalize` (`RollSetValidation.cs:20-32`) |
| a malformed set reports "busy" | `BlockRollingMill.cs:312` | an author error in a steel set is indistinguishable from a running pass ([rolling mill](rolling-mill.md) § Gotchas) |
| `Accepts` is matched against `StockForm` names | `RollSetSpec.cs:73-74`; `StockForm.All` = `shingledbar`, `shingledslab` only (`StockForm.cs:80`) | the three cast forms do not exist. `castbillet` / `castbloom` / `castslab` must be added as `StockForm`s first - `StockForm.Register` is public, so that is a declaration rather than a fork |
| `stackSize` 1 | `RollSetItemDefinitions.cs:125` | one set per stack, as every stateful item |

---

## Drops

Nothing smex-specific. A fitted set is returned when the mill's principal is broken, along with any stuck
piece - [rolling mill](rolling-mill.md) § Drops. Breaking a stand in the middle of a hall fractures the drive
run and does not route to its neighbours - [wide hall](wide-hall.md) § Drops.

---

## Code — what actually has to be written

smex ships its own item provider; it does not touch iiex's. The spec is authored so that "the tooling
owns the data, the machine only reads it, so any mod adds a rolling product with an item def alone"
(`RollSetSpec.cs:10-11`), and the fit gate accepts any collectible with a `rollset` attribute regardless of
domain (`BlockRollingMill.cs:293-320`). The steel family is therefore a new `IExItemDefProvider` in smex
emitting `siex:rollset-*`, mirroring `RollSetItemDefinitions` exactly.

| Work | Where | Note |
|---|---|---|
| `SteelRollSetItemDefinitions` | `src/SteelIndustryExpanded/BlockStructures/Forming/` (new folder) | copy the shape of `RollSetItemDefinitions.cs:17-128`: a `Sets` dictionary, `SetTypes` as "the single source the recipes and the handbook both derive from" (`:106-108`), `attributesByType` (`:126`) |
| three new `StockForm`s | `IronIndustryExpanded/.../StockForm.cs:57-64` | iiex-side - the forms are iiex types even though the stock is smex content. `StockItemDefinitions` emits one item per form automatically ([rolling mill](rolling-mill.md) § Where a caller hooks in) |
| wire `MinTorque` | `MillFeed.Decide` (`MillFeed.cs:95-128`) + a new `FeedVerdict` member (`MillFeed.cs:6-29`) | the refusal must be its own verdict, not folded into `WontBite` - the fix is a bigger plant, exactly as `TooCold`'s fix is a furnace |
| the two extra stands | - | no code: two more `BlockRollingMill` placements ([wide hall](wide-hall.md)) |
| draw 3.5 and 3.0 | `assets/editable/shapes/` | |
| recipes + cost rows | smex `Recipes/Grid/` + `SiexRecipeConfig.Defaults()` (`SiexRecipeConfig.cs:45-70`) | the catalogue is hand-maintained; a missing row means the set is not discountable by `/exmod steel` |

### Tests

`test/IronIndustryExpanded.Tests/Blocks/Forming/` holds 123 methods across nine files, `RollSetSpecTests` among
them ([rolling mill](rolling-mill.md) § Tests). A smex steel family needs its own suite under
`test/SteelIndustryExpanded.Tests/`, and the first test worth writing is the one that does not exist for
any tier: that a set below `MinTorque` is refused.

---

## Gotchas

* Rejected: the `shape` set and the `wire` set - a `shape` set (bloom → rail / I-beam / angle) and a
  `wire` set (billet → wire-rod) are both settled rejections:
  * Rail, I-beam, angle, channel - rejected outright: zero consumers (vanilla has no metal rail at all),
    `castframe` owns the I-section role, and the wide-flange beam is anachronistic.
  * Wire / wire-rod / draw bench - elex-era, settled as D7: wire exists, in elex.

  What survives of that list: `wide-flat` (which is the extended hall) and `pipe/skelp` (which is a
  product route, not a set - the skelp comes off the 1.0 stand and the curving is the
  [bending roller](bending-roller.md)'s).
* smex ships two mills, not zero. A second kind of mill would be a reskin; the mills are identical, which
  is why buying two more copies is legitimate. The wide gaps are one-per-item, so reducing a slab is a
  train, and smex's contribution is two more stands at 3.5 / 3.0.
* The automation upgrade (reversing / three-high) is deferred. Too advanced while the rest of the line is
  hand-fed; the train stays hand-fed. The smex side of the roll-set work is config + art, not a mill-type
  upgrade.
* Rejected: a fixed list of four named steel sets. What smex contributes is the cast stock, two stands
  and the steel roll-set family - an open family, not a catalogue of four items.
* The steam hammer is iiex, not smex ([steam hammer](steam-hammer.md), STATE.md placement rule).
* The bending roller is iiex, and it is not a roll set. "Conical pipe roller" is its old name. Bending
  changes curvature, which `WorkPiece` has no axis for - a different operation
  ([bending roller](bending-roller.md)).
* The roll-set item is live; the steel family is not. And no roll set of any tier has a recipe.
* ~~`slitting` must not be copied forward.~~ It cannot be: the set was retired on 2026-08-12, having
  accepted a form that is not a `StockForm` and so never biting anything.
* ~~Four of the five shipped output codes name items that do not exist~~ - a set no longer names a product
  at all, so there is nothing here to copy wrongly. What survives as advice: `TryParse` still resolves no
  code, because the codes are the ladder's. Authoring a steel family the same way would ship
  four more dangling codes; `Outputs` is leaving for the [shear](shear.md) precisely to stop that.
* `Outputs` is a `Dictionary<float, string>` compared with `==` (`RollSetSpec.cs:96-101`). Single-gap
  wide sets shrink the hazard to one entry each; the narrow steel sets do not.

---

## Open

| # | Question | Notes |
|---|---|---|
| ~~1~~ | ~~Are steel wide sets separate items, or do iiex's four simply gain the cast forms in `accepts`?~~ | **Settled 2026-08-14 (owner): the wide family is ONE item.** Its top roller is movable and the player sets the gap on the stand, so per-gap wide sets are cancelled outright and iiex's `flatwide` gained `castbloom` / `castslab`. ⛔ The cost is exactly what this row warned of - one item, one `MinTorque` - so the wide route carries **no torque gate at all** and cast stock rolls behind the iron-tier 0.5. If the gate must be real on the wide side it needs a mechanism other than a second set |
| 2 | What `MinTorque` values? | Unanswerable until the field is read and the mp-energy numbers settle. Anchors: hot pass ≈ 0.338 N·m, one bridged waterwheel ≈ 0.4 N·m headroom ([rolling mill](rolling-mill.md), [mp-energy](../mechanics/mp-energy.md)). ★ The **shear** side did get a gate in the meantime: every cast crop row asks `minTier: 2`, the steel blade, which is pinned |
| 3 | Wire the per-consumer idle draw into the stands. | Settled 2026-08-05 on [mp-energy](../mechanics/mp-energy.md) § Idle draw: every connected consumer contributes a standing torque, with the clutch transmission as the disconnect. The shipped code still charges friction per run, not per node - until that lands, "steel needs a bigger plant" has no mechanism behind it |
| ~~4~~ | ~~Two roll shapes to draw (3.5, 3.0)~~ | Dropped with question 1 - there is no per-gap wide item to draw. Wiring the narrow art already drawn stands |
| 5 | Recipes, and the cost-catalogue rows | none exist for any set in any tier |
| 6 | Three `StockForm`s and their long-cell patterns | the cavity redraw is a separate build item |
| 7 | Does D3's alloy-grade penalty touch roll sets? | Recommend no: the rolls are cast iron in every tier and the gate is torque, so grade has nothing to attach to. Recorded so it is not re-derived |
| 8 | Nothing feeds these sets. The mill cannot roll at all (B3, B17), the long cell's cavities are the wrong size, and no cast stock item exists | the steel sets sit downstream of at least four separate blockers ([rolling mill](rolling-mill.md), [long cell](long-cell.md)) |
