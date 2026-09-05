# Roll sets

**Status** live as an item, inert as a mechanism. Four `iiex:rollset-*` items ship - variant-emitted,
lang-keyed in three languages, golden-pinned, and read by the mill's fit gesture. None of them can produce
anything: four of the five output codes name items that do not exist, and the one field the steel tier is
supposed to gate on is never read. The other eight settled sets (six iiex, two smex) exist only on paper.
**Mod** iiex ships the itemtype and all four shipped sets. siex is settled to ship its own -
see [The cross-mod emit question](#the-cross-mod-emit-question).

## Owns

* the spec-carrying **tooling idiom** shared by all three tooling families in the suite (`rollset` / `mold` /
  `die`) - its five invariants and the four places a new family must touch. [patterns](patterns.md) and
  [dies](dies.md) cite this section instead of restating it;
* the roll-set family census - every set that exists in code or is settled on paper, across iiex / iiex /
  smex, in one table, with the page that owns each one's numbers;
* the `iiex:rollset` item definition as an item - one itemtype, the `type` variant group, the placeholder
  shape, `MaxStackSize(1)`, the `attributesByType` wildcard, the double-not-float authoring rule, the golden
  and the lang rows;
* the roll-art census - the seven drawn `item-rollers-*.json` files, which settled set each maps to, and
  the measurements of `flat`, `grooved` and `castblank`;
* the cross-mod emit question - how iiex and smex ship sets without touching iiex's itemtype, and what
  that costs in `SetTypes`, lang and goldens.

## Does not own — cited only, never restated

| Fact | Owner |
|---|---|
| the `rollset` **spec format**, every `TryParse` validation rule, the shipped four-set catalogue's numbers, `WorkPiece`, `StockForm`, the pass model, the mill's blockers and its gotchas | [rolling mill](../machines/rolling-mill.md) |
| the six single-gap `flatwide` sets (1.0 … 3.5, barrel 16, `MaxWidth` 15), the drawn-gap-is-the-modelled-gap table for the four `flatwide` shapes, the hall as a build | [wide hall](../machines/wide-hall.md) |
| the two smex wide sets (3.5 / 3.0), the steel narrow sets, the `MinTorque` tier, and what "steel roll set" means | [steel roll sets](../machines/steel-roll-sets.md) |
| `Outputs` / `OutputAt` moving off the spec onto a **stage** table, every crop, and `MinTorque`'s first real consumer (the cold cut) | [shear](../machines/shear.md) |
| `1 vx³ = 2.5 u`, `materialUnits` being dead data, and the audit of shipped masses | [density rule](../mechanics/density-rule.md) |
| the code-first def builders, `attributesByType` mechanics, the goldens harness, the cost catalogue | [recipes & config](../mechanics/recipes-config.md) |
| the energy model and what a torque number means | [mp-energy](../mechanics/mp-energy.md) |
| the `ItemDie` field contract and the four-die catalogue | [heading machine](../machines/heading-machine.md), [dies](dies.md) |
| the `mold` field contract and the shipped pattern catalogue | [casting cell](../machines/casting-cell.md), [patterns](patterns.md) |

**Depends on** [rolling mill](../machines/rolling-mill.md) · [wide hall](../machines/wide-hall.md) ·
[steel roll sets](../machines/steel-roll-sets.md) · [shear](../machines/shear.md) ·
[recipes & config](../mechanics/recipes-config.md) · [density rule](../mechanics/density-rule.md) ·
[patterns](patterns.md) · [dies](dies.md) · [STATE.md § placement rule](../../internal/plans/STATE.md)

---

## Role

A roll set is **the machine's data, held in the player's hand**. The mill is a general two-high stand that
names no product anywhere in its code; what it makes is read off the fitted item's `rollset` attribute at
`BlockEntityRollingMill.cs:129`. That is stated as an explicit design goal in-source: *"the tooling owns the
data, the machine only reads it, so any mod adds a rolling product with an item def alone"*
(`RollSetSpec.cs:10-12`).

The roll set is where the suite's central idiom was invented. The casting pattern copies it
(`MoldSpec.cs:18-25`), and the designed `ItemDie` is *"modelled exactly on `RollSetSpec`"*
([heading machine](../machines/heading-machine.md)). The idiom below applies to all three families.

### The idiom's five invariants

| # | Invariant | Where it is enforced |
|---|---|---|
| 1 | One attribute key per family, declared as a `const string` on the spec record | `RollSetSpec.cs:66` (`"rollset"`) · `MoldSpec.cs:42` (`"mold"`) · `"die"` designed |
| 2 | `TryParse(JsonObject?, out spec, out error)` returning a human-readable error, never an exception and never a silent default | `RollSetSpec.cs:108-201` · `MoldSpec.cs:49-116` |
| 3 | A load-time sweep over every collectible, run once at `AssetsFinalize`, logging one line per malformed item - *"a load-time complaint rather than a mystery at the mill"* | `RollSetValidation.cs:20-32` · `PatternValidation.cs:19-31` |
| 4 | The machine's gate is domain-blind - it tests only that the attribute exists, never the item's domain, which is what makes an `iiex:` set legal at an `iiex:` mill with no new code | `BlockRollingMill.cs:294` · `BlockEntitySandCastingCell.cs:188` (`FirstCodePart() == "pattern"`) |
| 5 | Per-variant specs via `attributesByType`, so one itemtype carries N specs and the variant list is the single source recipes and handbook derive from | `RollSetItemDefinitions.cs:114-116`, `:126` · `PatternItemDefinitions.cs:145-147`, `:172` |

### The four places a new tooling family must touch

1. a `sealed record` + `TryParse` next to the machine that reads it;
2. a `*Validation.Validate(IEnumerable<CollectibleObject>)` static, called from the mod system's
   `AssetsFinalize` (iiex's is `IronIndustryExpandedModSystem.cs:113-116`);
3. an `IExItemDefProvider` emitting one itemtype with a `type` variant group and `attributesByType`;
4. a `public static readonly string[]` of the variant names, so recipes and lang derive from one list
   (`RollSetItemDefinitions.SetTypes`, `:108`).

Nothing in the idiom validates that an output code resolves. `RollSetSpec.TryParse` checks only that an
output's gap is one of the barrel's gaps (`:172-175`); `MoldSpec.TryParse` checks only that the output stack
carries *a* code (`:105-110`). That omission is how four dangling roll-set codes ship
([rolling mill](../machines/rolling-mill.md) § Shipped roll-set catalogue).

---

## The catalogue

A roll set has no mass row. It is tooling, not stock: the def declares neither `materialUnits` nor
`MaterialDensity` (`RollSetItemDefinitions.cs:118-127`, confirmed by the golden
`mods/iiex/tests/goldens/iiex/itemtypes/rollset.json`), so the
[density rule](../mechanics/density-rule.md) has nothing to check. The mass columns are replaced by which
stock a set bites and where it sits.

### Shipped — four items, all iiex

| Item | Family | Bites | Gaps | Barrel | Fitted to | Makes | file:line |
|---|---|---|---|---|---|---|---|
| `iiex:rollset-flat` | flat | `shingledbar`, `billet` | the stock's ladder: 2.5 / 2.0 / 1.5 / 1.0 | 4.0 | [rolling mill](../machines/rolling-mill.md) | none - a set names no product | `RollSetItemDefinitions.cs:50-57` |
| `iiex:rollset-flatwide` | flat | `shingledslab`, `shingledbar` | the stock's ladder | 16.0 | " | none | `:59-66` |
| `iiex:rollset-grooved` | grooved | `shingledbar`, `billet` | the bar's grooved branch: 2.5 / 2.0 / 1.5 / 1.0 | 16.0 | " | none | `:70-76` |

The gap, barrel, `minTorque` and output values are [rolling mill](../machines/rolling-mill.md)'s to own; they
are reproduced here only as the census key, and the delta against the settled schedules is that page's table.

### Settled but unwritten — eight more sets, two more mods

| Set | Ships with | Gaps | Purpose | Numbers owned by |
|---|---|---|---|---|
| `flatwide` 2.5 | iiex | 2.5 | first stand a `shingledslab` enters | [wide hall](../machines/wide-hall.md) |
| `flatwide` 2.0 / 1.5 / 1.0 | iiex | one each | the rest of the four-stand train | " |
| `flatwide` 3.5 / 3.0 | smex | one each | the two stands cast steel needs in front | [steel roll sets](../machines/steel-roll-sets.md) |
| steel `grooved` | smex | 2.5 / 2.0 / 1.5 / 1.0 | `castbillet` on the iiex mill's own barrel | " |
| steel `flat` | smex | 2.5 / 2.0 / 1.5 / 1.0 | " | " |

So the settled family is twelve sets across three mods against **three** shipped in one. Two of the three
deltas closed on 2026-08-12: `slitting` is deleted and the iiex `flat` schedule is 2.5 / 2.0 / 1.5 / 1.0 on
a 4-wide barrel. What is left is that `flatwide` is still one item where the settled train is six one-gap
items
([rolling mill](../machines/rolling-mill.md) § the authored roll art;
[wide hall](../machines/wide-hall.md) § The six wide roll sets).

### What is *not* a roll set

Bending is not a roll-set job. The [bending roller](../machines/bending-roller.md) walks curvature up in
passes while the mill walks thickness down in gaps; `WorkPiece` has no curvature axis, so expressing a bend as
a `gaps` array would mean an array that silently means something else
([STATE.md § Bending is a separate machine](../../internal/plans/STATE.md)). Blade sets for the
[shear](../machines/shear.md) and die sets for the [steam hammer](../machines/steam-hammer.md) are separate
tooling families with their own attribute keys, not roll-set variants - see [dies](dies.md).

---

## Numbers

### The item definition — every constant it declares

| Property | Value | file:line |
|---|---|---|
| itemtype code | `rollset` (asset `{domain}:itemtypes/rollset.json`) | `RollSetItemDefinitions.cs:119` |
| item shape | `game:item/ingot` - placeholder | `:122` |
| texture (`all`) | `iiex:block/metal/castiron` | `:123` |
| variant group | `type` over `SetTypes` = `flat`, `flatwide`, `grooved` | `:88`, `:103` |
| max stack size | 1 | `:125` |
| per-variant specs | `attributesByType["*-{type}"]` | `:126`, built at `:114-116` |
| creative inventory | `*` in `general` + `iiex` | `:127` |
| `materialUnits` | none declared | — |
| `MaterialDensity` | none declared | — |
| durability | none declared today - ruled 2026-08-05 that a roll set does wear. [tooling-wear.md](../mechanics/tooling-wear.md) owns the rule for the whole family; the metal grade sets the life. Numbers are still open there. | — |

The spec is authored in `double`, not `float`. A `float` widened on the way into JSON leaks its binary error
(`0.4f` becomes `0.4000000059604645`), which destabilises the emitted def against its golden; the spec reads
them back as `float` and only the authoring side needs the exact literal
(`RollSetItemDefinitions.cs:22-24`, helper signature `:25-32`). A set added in another mod must copy this or
the golden harness fails on a number that looks identical.

### The spec's field roster — pointers, not rules

The meaning and the validation rule of every field belong to
[rolling mill](../machines/rolling-mill.md) § the roll-set spec format. This table exists so an item author
knows what an entry must contain and where to read the rule.

*Re-cut 2026-08-12. `gaps` and `outputs` are gone: a set declares only what the tooling itself knows, and
the states the metal passes through are the stock's process route
([process-extension](../mechanics/process-extension.md)).*

| JSON key | C# member | Required? |
|---|---|---|
| `schema` | `Schema` | no - absent reads as 1 |
| `family` | `Family` | yes, non-blank. The roller family, which selects this set's branch of a ladder |
| `accepts` | `Accepts` | yes, ≥ 1 entry. The tooling's own geometry, and **not** derived from the ladder - a narrow barrel refuses a slab whatever states the slab has |
| `barrelWidth` | `BarrelWidth` | yes, > 0 |
| `minTorque` | `MinTorque` | no - defaults to `0f` |

⛔ ~~The array-not-object decision for `outputs`.~~ Retired with `outputs` itself. The lesson it carried -
a float is a poor JSON key - survives as the tolerance every gauge comparison now uses.

### The dead API — what a roll set carries that nothing consumes

Measured by a repo-wide grep over `src/` excluding the declaration site.

| Member | Declared | Parsed | Production callers | Note |
|---|---|---|---|---|
| `MinTorque` | `RollSetSpec.cs:37` | `:198` | none | the steel tier's only gate. A steel set on a hand-fed waterwheel rolls exactly as fast as an iron one. First real consumer is designed to be the [shear](../machines/shear.md)'s cold cut |
| `Outputs` | `:35` | `:159-183` | none | leaving for the [shear](../machines/shear.md), keyed on stage |
| `OutputAt` | `:95-101` | — | none | compares `float` with `==` |
| `IsWide` | `:70` | — | none | already the right definition (`Gaps.Length == 1`) |
| `PassesAt` | `:54-59` | — | none | the two-pass floor and the side-by-side rule |
| `OverhangsBarrel` | `:63` | — | none | |
| `NextGap` / `NextDraft` | `:81-87` / `:90-91` | — | none | the "you cannot skip a segment" rule, unwired |
| `Family` | `:32` | `:119-125` | none | validated as non-blank, then never looked at - flavour + handbook grouping only |

Exactly five members of `RollSetSpec` are reached from production code, and they are the minimum a fit
gesture and a raw index need:

| Member | Production call sites |
|---|---|
| `AttributeKey` | `BlockEntityRollingMill.cs:129`, `:148`, `BlockRollingMill.cs:294` |
| `TryParse` | `BlockEntityRollingMill.cs:148`, `RollSetValidation.cs:29` |
| `AcceptsForm` | `MillFeed.cs:107` |
| `Gaps` | `BlockEntityRollingMill.cs:199`, `BlockRollingMill.cs:334`, `MillFeed.cs:109`, `:114` |
| `BarrelWidth` | `BlockEntityRollingMill.cs:181`, `BlockRollingMill.cs:337` |

So the mill uses the roll set as *"which forms, which gap array, how wide"* and nothing more. The schedule
walk, the product stops and the torque gate have no caller. `PassesAt` is referenced only by a `<see cref>`
in a doc comment (`RollingPass.cs:141`), which is how it reads as wired when it is not.

### Four output codes that name nothing

`iiex:rolledplate-iron`, `iiex:rolledsheet-iron`, `iiex:wirerod-iron`, `iiex:nailrod-iron` do not exist
anywhere in `src/` (`RollSetItemDefinitions.cs:66`, `:77`, `:90`, `:100`). Only `game:rod-iron` (`:90`)
resolves. `RollSetValidation` cannot catch this - see the caution at the end of
[the idiom's invariants](#the-idioms-five-invariants).

---

## Assets

### The item art census — seven drawn files, none wired

All seven are untracked in git (`??`) and referenced by no code; the item ships `game:item/ingot`
(`RollSetItemDefinitions.cs:122`), so every roll set in every tier renders as an ingot.

| Editable shape | Draws | Maps to | State |
|---|---|---|---|
| `workbench/shapes/item-rollers-flat.json` | four 4-voxel barrel segments named `20Gap`/`15Gap`/`10Gap`/`05Gap`, up-and-down roll pairs | iiex `flat` | drawn to the shipped schedule, not the settled one |
| `item-rollers-grooved.json` | four 4-voxel segments named `25Gap`/`20Gap`/`15Gap`/`10Gap` | iiex/smex `grooved` | drawn to the settled schedule |
| `item-rollers-flatwide20.json` · `…15` · `…10` | one 16-long roll pair each | iiex 2.0 / 1.5 / 1.0 | gap measurements are [wide hall](../machines/wide-hall.md)'s |
| `item-rollers-flatwide5.json` | one 16-long roll pair | the 0.5 gap is deleted by the settled schedule | orphan unless 0.5 is re-added |
| `item-rollers-castblank.json` | two plain 16-long roll blanks (`Blank1`/`Blank2` → `Blank11`/`Blank12`), no grooves | the cast roll blank a set should be made from | no recipe consumes it |
| — | — | iiex 2.5, smex 3.0 / 3.5 | must be drawn |

Measured, this page's (the four `flatwide` gaps are [wide hall](../machines/wide-hall.md)'s):

| Shape | Segment | Down-roll top y | Up-roll bottom y | Gap drawn |
|---|---|---|---|---|
| `item-rollers-flat.json` | `20GapDown` / `20GapUp` | 11.0 | 13.0 | 2.0 |
| " | `15Gap*` | 11.25 | 12.75 | 1.5 |
| " | `10Gap*` | 11.5 | 12.5 | 1.0 |
| " | `05Gap*` | 11.75 | 12.25 | 0.5 |
| `item-rollers-grooved.json` | `25Gap*` (up-roll parent offset `+8` y) | 10.998 | 13.500 | ≈ 2.5 |
| " | `20Gap*` | 10.997 | 13.000 | ≈ 2.0 |
| " | `15Gap*` | 11.247 | 12.750 | ≈ 1.5 |
| " | `10Gap*` | 11.497 | 12.500 | ≈ 1.0 |

Every barrel segment in both narrow shapes is exactly 4 voxels wide (`x −16 → −12 → −8 → −4 → 0`), which is
the settled `flat` barrel of 4. The config's `barrelWidth: 6.0` (`RollSetItemDefinitions.cs:67`) matches
nothing drawn. The grooved set keeps `barrelWidth: 16` (`:91`) because a groove constrains spread rather than
letting the work run sideways (`RollSetItemDefinitions.cs:83-85`) - so for grooved the declared barrel is a
never-overhangs sentinel, not the segment width. Two families, two meanings for the same field, and nothing
says so in the schema.

### Other art facts

| Fact | Detail |
|---|---|
| stale in-source comment | `RollSetItemDefinitions.cs:120-121` says *"the item-rollers source holds all the families in one file, so they ship together"*. That file - `workbench/shapes/item-rollers.json` - is deleted (`git status` reports ` D`); it was split into the seven files above |
| absolute authoring paths | all seven declare `cast-iron1 → F:/repos/modding-vsexpanded/workbench/textures/cast-iron1`. That resolves to nothing in game and fails silently; `editable/` is source-only by convention |
| an animation on an item shape | `item-rollers-flat.json` and `item-rollers-grooved.json` each carry a 30-frame `cycle` clip with 2 keyframes and `onAnimationEnd: EaseOut`. Nothing plays an item shape's clip, and a looping clip must be `Repeat` or the mesh vanishes - if this art is ever hosted on the mill, both facts bite |
| runtime shapes | none. `mods/iiex/assets/iiex/shapes/forming/` holds `rollingmill.json` and the ten generated `stock-*.json` only |
| handbook | none for the roll set in `mods/iiex/docs/handbook/` |

### Lang — the one part of the family that is complete

| Key | en (`mods/iiex/assets/iiex/lang/en.json`) | ru | uk |
|---|---|---|---|
| `item-rollset-flat` | "Flat Roll Set" - `:113` | `ru.json:130` | `uk.json:130` |
| `item-rollset-flatwide` | "Wide Flat Roll Set" - `:114` | `:131` | `:131` |
| `item-rollset-grooved` | "Grooved Roll Set" - `:115` | `:132` | `:132` |

The three surviving rows are correct for what ships. `slitting`'s row was translated into Russian and
Ukrainian for a set that could never accept anything; it went with the set on 2026-08-12.

---

## Construction

There is no recipe for any roll set, in any tier. Every set is creative-only
(`RollSetItemDefinitions.cs:127`), there are no hand-written recipe assets in the repo, and neither
`IiexRecipeConfig.DefaultCatalogue` nor `SiexRecipeConfig.Defaults()` carries a roll-set cost row
([recipes & config](../mechanics/recipes-config.md)).

The route the art implies: `item-rollers-castblank.json` is a drawn cast blank, and the rolls are chilled
cast iron in every tier (`RollSetItemDefinitions.cs:11-14`) - so a set should be cast in the
[casting cell](../machines/casting-cell.md), then finished, never forged. That needs a
[pattern](patterns.md) entry, which is the shortest live path from this page to a craftable set. The economy
question - whether four (iiex) or six (smex) sets plus their mills is a sane ask - is
[wide hall](../machines/wide-hall.md)'s and [steel roll sets](../machines/steel-roll-sets.md)'.

---

## Code

| Piece | file:line | Role |
|---|---|---|
| `RollSetSpec` | `mods/iiex/src/BlockStructures/Forming/RollSetSpec.cs:31` | the record; `TryParse` at `:108-201` |
| `RollSetItemDefinitions` | `…/Forming/RollSetItemDefinitions.cs:17` | `IExItemDefProvider`; `Sets` at `:56-104`, `SetTypes` at `:108`, the emit at `:112-128` |
| `RollSetItemDefinitions.Set` / `.Out` | `:25-44` / `:46` | the authoring helpers that force `double` |
| `RollSetValidation.Validate` | `…/Forming/RollSetValidation.cs:20-32` | the `AssetsFinalize` sweep; pure over a collectible sequence |
| the fit gate | `BlockRollingMill.cs:294` | attribute-exists only, domain-blind |
| the read | `BlockEntityRollingMill.cs:129`, `:148` | the only two production parses |
| golden | `mods/iiex/tests/goldens/iiex/itemtypes/rollset.json` | pins all four specs byte-for-byte |
| tests | `mods/iiex/tests/Blocks/Forming/RollSetSpecTests.cs` - 15 `[Fact]`/`[Theory]`, 230 lines | the schema; the mill's other eight files are [rolling mill](../machines/rolling-mill.md)'s |

### The cross-mod emit question

A second mod ships sets by emitting its own itemtype, not by extending iiex's. The fit gate never looks at
the domain (`BlockRollingMill.cs:294`), and `attributesByType` is per-itemtype, so iiex would ship
`iiex:rollset-*` from its own `IExItemDefProvider` and smex `siex:rollset-*` from its own
([steel roll sets](../machines/steel-roll-sets.md) § Code). That works today with no iiex change.

What it costs, and nobody has written it down:

| Consequence | Detail |
|---|---|
| `SetTypes` stops being *the* single source | `RollSetItemDefinitions.SetTypes` (`:108`) is documented as *"the single source the recipes and the handbook both derive from"*. With three providers there are three such lists and no union anywhere |
| three goldens, three lang files | each mod gets its own `itemtypes/rollset.json` golden and its own `item-rollset-*` rows; a shared name across domains is legal but reads identically in a tooltip |
| a set is visually indistinguishable from any other set | all of them render `game:item/ingot` (`:122`), and the fitted set is legible only from block info. Mis-fitting an iiex wide set where a smex one is needed is silent |
| the double-not-float rule must be re-derived | `RollSetItemDefinitions.cs:22-24` is a comment in iiex; a new provider that authors `float[]` gaps ships a destabilised golden and nothing warns |
| `RollSetSpec` itself stays in iiex | so iiex and smex take an iiex reference. The chain allows it (`exlib ← iiex ← iiex ← smex`), but the same question is open for `ItemDie` - see [dies § Open](dies.md) |

---

## Gotchas

* The whole product half of the record is unwired. `Outputs`, `OutputAt`, `NextGap`, `NextDraft`,
  `PassesAt`, `IsWide`, `OverhangsBarrel` and `MinTorque` have no production caller. A reader who takes
  `RollSetSpec`'s doc comment at face value - *"a flat set running 2.0 → 1.5 → 1.0 → 0.5 yields plate at 1.0
  and sheet at 0.5"* (`RollSetSpec.cs:22-23`) - will believe the mill makes plate. It does not; it makes
  thinner stock, forever ([rolling mill](../machines/rolling-mill.md) § Gotchas).
* `minTorque` is optional and defaults to `0` (`RollSetSpec.cs:198`). A set authored without it is valid
  and, if the gate is ever wired, will turn on any drive at all. There is no validation requiring it, and the
  shipped four all set it - which hides the default.
* `accepts` is matched against `StockForm` names, and `StockForm.All` holds only `shingledbar` and `shingledslab`
  (`StockForm.cs:57-64`). Two shipped sets accept `billet` and one accepts `plate`; none of the three is a
  form, so those entries are unreachable strings that `TryParse` happily accepts (`RollSetSpec.cs:126-134`
  checks only that the array is non-empty and its entries non-blank).
* A malformed set reports "busy". `TryFitRollSet` returns `false` both for a running pass and for a parse
  failure (`BlockEntityRollingMill.cs:144-154`), and the block maps every `false` to
  `iiex-rollingmill-busy` (`BlockRollingMill.cs:312`). An author error in a new mod's set is
  indistinguishable from the mill being in use.
* The validation sweep is the only thing that will ever tell you a set is broken, and it runs server-side
  at `AssetsFinalize` into the log. There is no in-game surface for it.
* `barrelWidth` means two different things across the shipped families - the segment width for `flat`, a
  never-overhangs sentinel for `grooved` and `flatwide`. See the note under
  [the art census](#the-item-art-census--seven-drawn-files-none-wired).
* The idiom has no version field. A mod that ships a `rollset` attribute against a future schema will fail
  `TryParse` with a message about a missing key, not a message about a version.

---

## Open

| # | Question | Notes |
|---|---|---|
| 1 | **Wire `MinTorque`, or delete it.** | It has been parsed and unread through the whole life of the item. The [shear](../machines/shear.md) is designed to be its first consumer, and [steel roll sets](../machines/steel-roll-sets.md) § Open 2 cannot pick values until it is read |
| 2 | **Validate that an output code resolves** | one added check in `RollSetValidation` (`:20-32`) would have caught all four dangling codes at load. It must run after the object loader, which is a load-order question |
| 3 | **Wire the seven drawn shapes** and re-texture them with domain-relative references | plus draw 2.5 (iiex) and 3.0 / 3.5 (smex) |
| 4 | **Redraw `item-rollers-flat.json`** to the settled 2.5 / 2.0 / 1.5 / 1.0 | the grooved shape is already correct; the flat one is drawn to the schedule that is being replaced |
| 5 | **A recipe - any recipe - for any set** | the cast-blank route is implied by `item-rollers-castblank.json` and needs a [pattern](patterns.md) entry first |
| 6 | **Does `RollSetSpec` move to exlib?** | Three mods will ship sets, and the identical question is open for `ItemDie` ([heading machine § Open](../machines/heading-machine.md)). `MoldSpec` set the precedent by staying in iiex (`MoldSpec.cs:6`); nobody has re-examined it since the family grew to three |
| 7 | **Should the idiom carry `MaxWidth`?** | [wide hall](../machines/wide-hall.md) settles that `MaxWidth` moves off `StockForm` onto the roll set. That is a sixth field on the record and a golden change for every shipped set |
| ~~8~~ | ~~**Delete `slitting`**~~ | **done 2026-08-12**, with its three lang rows. Retired rather than renamed, so no migration - remapping a retired code onto a surviving one hands the player an item they never had |
