# Dies

**Status** designed - nothing exists. A repo-wide grep of `src/` for `ItemDie`, `DieSpec` or a `"die"`
attribute key returns zero hits (the only match for `die` is the word "die" in an exlib doc-comment,
`ExpandedLib/Blocks/Healing/BlockEntityHealModSystem.cs:18`). No spec record, no item def, no runtime shape,
no lang key in any of the three languages, no recipe, no cost-catalogue row, no test. One die shape is drawn
and untracked. Every one of the five settled dies is also downstream of at least one unbuilt machine.
**Mod** both content mods are settled to ship dies - iiex (nail, bolt, rivet, stamping), siex (ball) -
against one spec type whose home is undecided.

## Owns

* the die as an item - an ordinary collectible carrying a `die` attribute at `MaxStackSize(1)`, the
  `IExItemDefProvider` each shipping mod must write, and what a die item does not carry (no mass, no density,
  no durability decision);
* the family build census - all five dies in one table with the state of each and the machine, item and
  blocker each sits behind. Zero of five exist;
* the finding that the `Bench` field's declared value set is too small - the settled catalogue has die
  consumers the contract's `heading | nail` does not name;
* the mass-exactness invariant across all three tooling families - that `Count` is the only field anywhere in
  the idiom that can mint or destroy metal, and what must be true of every future die because of it;
* the die art census - the one drawn shape, measured, and what the "batch size = die cavity count" rule reads
  off it;
* the cross-mod emit consequences - four mods shipping die items against one spec type, and the second dead
  copy of `MinTorque`.

## Does not own — cited only, never restated

| Fact | Owner |
|---|---|
| the `ItemDie` tooling contract - its fields, their meanings, `TryParse`, fitting, refusal - and the die catalogue's rows (which die takes what and ships with whom) | [heading machine](../machines/heading-machine.md) |
| the stamping die: `boilerplate` → 3 × `game:metalplate`, the batch-size-is-cavity-count rule, the two die tiers (plain / quench-hardened), forged-never-cast, the die-set-renders-both-faces decision, the docked anvil | [steam hammer](../machines/steam-hammer.md) |
| the nail die's conversion (1 `nailplate` → 4 × `game:metalnailsandstrips`) and the no-minting anchor | [nail machine](../machines/nail-machine.md) |
| the ball die's own spec, chrome steel, the race gap and the bearing bootstrap | [bearings](../machines/bearings.md) |
| the ≥ 2-voxel HP side of the thickness gate and the finding that it is empty | [hp hammer](../machines/hp-hammer.md) |
| the shear-cuts-across / die-cuts-out verb split, why the hammer never shears, blade sets, the cold-cut torque gate | [shear](../machines/shear.md) |
| the spec-carrying tooling idiom - its five invariants, the four places a new family must touch, the array-not-float-key rule, the double-not-float authoring rule | [roll sets](roll-sets.md) |
| the `mold` sibling and its 96 items | [patterns](patterns.md) |
| `1 vx³ = 2.5 u` and every mass the ledger uses | [density rule](../mechanics/density-rule.md) |
| the fastener placement rule (nails and bolts are iron, rivets are steam) | [STATE.md § Fasteners](../../../../docs/plans/STATE.md) |
| code-first defs, `attributesByType`, goldens, the cost catalogue | [recipes & config](../mechanics/recipes-config.md) |

**Depends on** [heading machine](../machines/heading-machine.md) ·
[steam hammer](../machines/steam-hammer.md) · [nail machine](../machines/nail-machine.md) ·
[bearings](../machines/bearings.md) · [hp hammer](../machines/hp-hammer.md) ·
[shear](../machines/shear.md) · [roll sets](roll-sets.md) · [patterns](patterns.md) ·
[density rule](../mechanics/density-rule.md) · [STATE.md § Fasteners](../../../../docs/plans/STATE.md)

---

## Role

A die is the third member of the spec-carrying tooling family. Where a [roll set](roll-sets.md) says "these
are the thicknesses this stand can reach" and a [pattern](patterns.md) says "this is the cavity this cell
fills", a die says: this input becomes that output, this many times. That is why one bench can be a bolt works
or a rivet works - Fig 1 of the 1867 machine-tool plate is captioned "rivet making machine" and the same
machine made bolts, the die being the whole difference ([STATE.md § Fasteners](../../../../docs/plans/STATE.md)). One
block, a tooling slot, and iiex adds a rivet without touching iiex
([heading machine](../machines/heading-machine.md)).

It is also the cheapest cross-mod extension point in the suite. A pattern needs an impression shape drawn and
an output item; a roll set needs a schedule and a barrel. A die needs four fields and an output, which is why
hpex's bearing balls cost no new machine - they are a die on a bench that already exists
([bearings](../machines/bearings.md)).

Nothing below is in code.

---

## The catalogue

A die has no mass row and no length row. Like every tooling item in the suite it declares neither
`materialUnits` nor `MaterialDensity` - compare the two shipped families
([roll sets § The catalogue](roll-sets.md), [patterns § The catalogue](patterns.md)) - so the
[density rule](../mechanics/density-rule.md) has nothing to check on the die itself. The masses below are the
ledger of what it converts, and every one is cited, not owned.

### The five settled dies, and the state of each

The `Input` / `Output` / `Ships with` columns are [heading machine](../machines/heading-machine.md)'s
catalogue for four rows and [steam hammer](../machines/steam-hammer.md)'s for the fifth. The State and
Blocked behind columns are this page's.

| Die | Fits | Input | Output | Ships with | State | Blocked behind |
|---|---|---|---|---|---|---|
| nail | [nail machine](../machines/nail-machine.md) | `nailplate` 100 u | 4 × `game:metalnailsandstrips` | iiex | nothing | the bench · `nailplate` · the `flat` 1.5 → 1.0 schedule · B3 |
| bolt | [heading machine](../machines/heading-machine.md) | rod @ 25 u | bolts | iiex | nothing | the bench · `rolledrod` · the bolt item · the [shear](../machines/shear.md) that crops the rod · B4 |
| rivet | [heading machine](../machines/heading-machine.md) | rod @ 25 u | rivets | iiex | nothing | everything the bolt die is blocked behind, plus an iiex forming section that does not exist |
| stamping | [steam hammer](../machines/steam-hammer.md) | `boilerplate` 600 u | 3 × `game:metalplate` | iiex | nothing; the drawn die art is a *flat* die, not the stamping die | the hammer · `boilerplate` · the wide hall · the whole `castslab` route |
| ball | [heading machine](../machines/heading-machine.md) | chrome-steel rod | bearing balls | hpex | nothing | everything above, plus chrome steel, ferrochrome and the ring race ([bearings](../machines/bearings.md)) |

Two benches and one hammer, five dies, four mods, one spec. The
[heading machine](../machines/heading-machine.md) owns four rows and the
[steam hammer](../machines/steam-hammer.md) owns the fifth, so no other page counts the family.

### The `Bench` field cannot name every consumer

[heading machine](../machines/heading-machine.md) § The `ItemDie` spec declares
`Bench: string` with the value set `heading | nail`, and gives its rationale: "the one field neither
predecessor needs, and it is what keeps the nail die off this block."

The settled catalogue has three kinds of die consumer, not two:

| Consumer | Verb | Needs a `Bench` value |
|---|---|---|
| [heading machine](../machines/heading-machine.md) | upset - adds a head, mass-neutral | `heading` - declared |
| [nail machine](../machines/nail-machine.md) | cut - shears a plate into nails | `nail` - declared |
| [steam hammer](../machines/steam-hammer.md) | blank / stamp - punches a shape out of a strip | not declared |
| [hp hammer](../machines/hp-hammer.md) | blank / stamp, ≥ 2 voxel output | not declared - and the gate is on the die |

So either `Bench` gains at least a `hammer` value (and the LP/HP split becomes a second field, because
[hp hammer](../machines/hp-hammer.md) is explicit that the ≥ 2-voxel gate lives on the die and not on the
block), or the hammer's dies are a separate family with their own attribute key and the "one die contract"
claim is narrower than it reads. Decide before the first die item is authored - this is a schema change after
the fact otherwise, and the tooling idiom has no version field ([roll sets § Gotchas](roll-sets.md)).

The verbs are already three, and the split is already settled prose: "the [shear](../machines/shear.md) cuts
across; a die on the [steam hammer](../machines/steam-hammer.md) cuts out; this bench upsets"
([heading machine](../machines/heading-machine.md) § Gotchas).

---

## Numbers

Nothing on this page is in code. There is no config section, no key, no constant and no `file:line` in
`src/` for any die, because no die type exists. Every number below is cited from the page that settled it.

### The ledger — and the one invariant this page owns

| Die | In | Out | Total out | Exact? | Owner of the row |
|---|---|---|---|---|---|
| nail | 100 u (`nailplate` 4 × 1 × 10) | 4 × 25 u | 100 u | exact | [nail machine](../machines/nail-machine.md) |
| bolt | 25 u (rod) | 1 × 25 u | 25 u | exact - "a bolt is the rod plus a head" | [heading machine](../machines/heading-machine.md) |
| rivet | 25 u (rod) | 1 × 25 u | 25 u | exact, by symmetry with the bolt | [heading machine](../machines/heading-machine.md) |
| stamping | 600 u (`boilerplate` 15 × 1 × 16) | 3 × 200 u | 600 u | exact | [steam hammer](../machines/steam-hammer.md) |
| ball | chrome rod | balls | unfixed | no - no mass has been chosen for a ball | [bearings](../machines/bearings.md) |

The family invariant: `Count` is the only field in the whole tooling idiom that can mint or destroy metal. A
[roll set](roll-sets.md) changes a piece's thickness - spread and elongation move width and length, but no
gap adds or removes a unit. A [pattern](patterns.md) fills a fixed `capacity` and the cell pays out exactly
what went in, minus a declared misrun. A die is the only one of the three that turns one stack into N stacks,
and nothing in the designed contract checks that `Count × output mass == input mass`.

Four of the five settled dies satisfy it by construction: the nail conversion sits on vanilla's own anvil
ceiling (4 nails per ingot's worth, [nail machine](../machines/nail-machine.md)), and the stamping conversion
is 3 × 200 against 600 ([steam hammer](../machines/steam-hammer.md)). The ball has no mass yet, which makes
it the first die that could break the invariant without anyone noticing.

So the die family needs a check the other two families do not: a load-time or test-time assertion that a
die's declared yield conserves mass. `RollSetValidation` and `PatternValidation` have no analogue, because
neither family can violate it. `materialUnits` is written on every item and read by nothing
([density rule § Gotchas](../mechanics/density-rule.md)), so such a validator would be the first consumer of
that attribute in the codebase, or it would need a C# constant per output.

### The one number a die will need that is not a mass

| Field | Note |
|---|---|
| `MinTorque` | the second dead copy. The identical field on `RollSetSpec` has been parsed, stored, validated and never read since it was written (`RollSetSpec.cs:37`, `:198`; [roll sets § The dead API](roll-sets.md)). Shipping a die spec that declares it before the [shear](../machines/shear.md) makes the first one work means two inert torque gates instead of one. Proposed anchors - heading 0.3, nail 0.15 - are [heading machine](../machines/heading-machine.md)'s and [nail machine](../machines/nail-machine.md)'s |

---

## Assets

### One drawn shape, and it is not a stamping die

| Asset | Path | State |
|---|---|---|
| die-set, flat (editable) | `workbench/shapes/item-steamhammerdie-flat.json` | drawn, untracked (`??`), wired to nothing. Renamed from `item-steamhammerdieflat.json`, which git still reports deleted |
| die item art, every other die | — | nothing drawn for nail, bolt, rivet, ball or stamping |
| runtime shape | `assets/{iiex,iiex,hpex}/shapes/` | none |
| lang | all three `lang/*.json` in every mod | no die key exists in any language |
| handbook | `docs/{iiex,iiex}/handbook/` | none |
| reference art | `workbench/refs/rivetsnails/machine-tools-…-1867-technology-RY93PB.jpg` Fig 1 | the folder is untracked |

Measured off the file (Blockbench voxels; one cell = 16). The die-set-renders-both-faces decision and the
element roles are [steam hammer](../machines/steam-hammer.md)'s; the geometry below is this page's.

| Element | from → to | Size | Reads as |
|---|---|---|---|
| `DieUp` | `[5,5,6] → [11,7,10]` | 6 × 2 × 4 | the ram half |
| `DieUp/Cube3`, `/Cube4` | 1 × 1 × 2 each (parent-local) | — | two guide lugs |
| `Buffer` | `[6,4,4] → [7,5,12]` | 1 × 1 × 8 | one of the two guide rails |
| `Buffer/Cube9` | 1 × 1 × 8 (parent-local) | — | the other rail |
| `DieBottom` | `[7,0,5] → [9,2,11]` | 2 × 2 × 6 | the anvil half's spine |
| `DieBottom/Cube6` | 8 × 2 × 6 (parent-local) | — | the working face |
| `DieBottom/Cube7`, `/Cube8` | 1 × 2.5 × 4 each | — | the seat lips |

Textures: `iron5 → block/metal/sheet-plain/iron5` and `generic → block/wood/planks/generic`. Unlike the
hammer and anvil shapes, this file carries no absolute `F:/…` authoring path - it is already domain-relative
and is the cleanest of the three drawn steam-hammer assets to export.

### The flat die has no cavities, and the rule reads batch size off cavities

[steam hammer](../machines/steam-hammer.md) settles that batch size = die cavity count, read off the die
mesh, "which is why the die mesh, not a config number, is the extension point." The one drawn die is a flat
die: `DieUp` is a plain 6 × 2 × 4 block and `DieBottom/Cube6` a plain 8 × 2 × 6 face. There is no cavity in
it at all.

That is correct for a flat/drawing die - what shingling and drawing down use, and shingling is the hammer's
first job - but it means:

* the stamping die (which must read as 3 cavities to yield 3 plates) is not the drawn asset and still has to
  be modelled;
* a mesh-derived batch size must define what a zero-cavity die means. Flat dies are not batch operations at
  all, so "cavity count" cannot be the only thing a die's behaviour is read from - the `Count` field and the
  mesh must agree, or one of them is decorative.

---

## Construction

No die of any kind has a recipe, an RCC path, or a cost-catalogue row. Neither
`IiexRecipeConfig.DefaultCatalogue` nor `IiexRecipeConfig` nor `SiexRecipeConfig.Defaults()` carries a die
entry ([recipes & config](../mechanics/recipes-config.md)).

What is settled about how a die is made, all cited:

| Fact | Owner |
|---|---|
| a die is forged, never cast - "a die takes the blow and imparts the profile, so it is tough, not brittle" | [steam hammer](../machines/steam-hammer.md) § Construction |
| two tiers - plain steel for soft stock, quench-hardened for hadfield and HSS | [steam hammer](../machines/steam-hammer.md) |
| hardened die sets are one of crucible steel's four named consumers | [STATE.md § D9](../../../../docs/plans/STATE.md) |
| "dies are crafted separately and are the extension point: many dies, one machine" | [heading machine](../machines/heading-machine.md) § Construction |

The die is therefore the only one of the three tooling families that is forged. A roll set is cast (chilled
cast iron, `RollSetItemDefinitions.cs:11-14`); a pattern is carved from planks
(`PatternRecipeDefinitions.cs:20-41`). That is the compression/tension rule applied three times, and it means
a die recipe cannot borrow either sibling's route.

---

## Code — the item def each mod must write

Nothing exists. The spec record's plan is [heading machine](../machines/heading-machine.md) § Code; the
item-def half is this page's, and it is a near-verbatim copy of the roll set's:

| Piece | Where | Copy from |
|---|---|---|
| `DieItemDefinitions : IExItemDefProvider` | one per shipping mod | `RollSetItemDefinitions.cs:17-128` - a `Dictionary<string, object> Dies`, a `public static readonly string[] DieTypes` as the single source recipes and lang derive from (`:108`), a private `Die(...)` helper authoring in `double` (`:22-32`), and `.Raw("attributesByType", byType)` with the `"*-" + type` wildcard (`:114-116`, `:126`) |
| `MaxStackSize(1)` | on the def | `RollSetItemDefinitions.cs:125` - every stateful/fitted item in the suite is stack 1 |
| item shape | per type | do not copy `RollSetItemDefinitions.cs:122`'s `game:item/ingot` placeholder - [heading machine](../machines/heading-machine.md) § Assets names it explicitly as the thing not to repeat |
| a validation sweep | `DieValidation.Validate(IEnumerable<CollectibleObject>)`, called from each mod system's `AssetsFinalize` | `RollSetValidation.cs:20-32` · `PatternValidation.cs:19-31` |
| golden | `test/<Mod>.Tests/goldens/<domain>/itemtypes/die.json` | `goldens/iiex/itemtypes/rollset.json` |
| lang | `item-die-{type}` × 3 languages × N mods | `en.json:113-116` is the roll-set precedent |

### The cross-mod emit question

The gate that fits a die will be domain-blind, exactly as the roll set's and the pattern's are
(`BlockRollingMill.cs:294`, `BlockEntitySandCastingCell.cs:188`) - that is invariant 4 of
[the idiom](roll-sets.md). So each mod emits its own `die` itemtype and nothing has to reference anything.

What that costs, and it is worse here than for the roll sets because there are four mods rather than three:

| Consequence | Detail |
|---|---|
| four `DieTypes` lists, no union | the "single source recipes and the handbook derive from" idiom (`RollSetItemDefinitions.cs:106-108`) fragments four ways |
| four goldens, four lang sets | `iiex:die-nail`, `iiex:die-bolt`, `iiex:die-rivet`, `iiex:die-stamping`, `siex:die-ball` |
| the spec type's home is undecided | `ItemDie` in iiex means iiex and hpex take an iiex reference. The chain allows it (`exlib ← iiex ← iiex ← smex ← hpex`), and `MoldSpec` set the precedent by staying in iiex (`MoldSpec.cs:6`) - but no other tooling spec has three downstream consumers. [heading machine § Open](../machines/heading-machine.md) records this as open; it is the same open question as [roll sets § Open 6](roll-sets.md) and [patterns § Open 9](patterns.md), and answering it once for all three is cheaper than three times |
| the double-not-float rule must be re-derived four times | `RollSetItemDefinitions.cs:22-24` is a comment in iiex |
| a die will be visually indistinguishable from another die unless the art lands with the item | the roll sets shipped without art and every one of them renders as an ingot; do not repeat it |

---

## Gotchas

* `Bench` is under-declared. See [the catalogue](#the-bench-field-cannot-name-every-consumer). The hammer is
  a die consumer and has no `Bench` value; the HP thickness gate lives on the die and has no field at all.
* `Count` is the only mint hazard in the entire tooling idiom, and nothing validates it. See
  [Numbers](#the-ledger--and-the-one-invariant-this-page-owns).
* Shipping `MinTorque` on a second spec before the first one is read doubles a dead field. The
  [shear](../machines/shear.md) is designed to be `MinTorque`'s first consumer anywhere; until it lands, a
  die's torque number is documentation.
* The nail bench may not want a die at all. The nail die is the only die that bench can ever hold, so the
  fitted-tooling slot may be pure ceremony - and if the nail machine hard-codes its die, the `Bench` field
  loses its stated reason to exist ([nail machine § Open](../machines/nail-machine.md),
  [heading machine § Open](../machines/heading-machine.md)).
* Fitting must hand the tooling back on refusal. `FitRollSet` takes the item out of the slot before asking
  and puts it back if the machine says no (`BlockRollingMill.cs:306-314`). Getting that order wrong eats the
  player's die - and a die is a forged, hardened, expensive item, unlike a roll set that no player can craft
  at all yet.
* A malformed die will report the machine as "busy" if the fit path copies the mill's, which maps every
  `false` from `TryFitRollSet` to one error code (`BlockRollingMill.cs:312`;
  [roll sets § Gotchas](roll-sets.md)). Copy the shape of that method, not its error handling.
* `ResolveBlockOrItem` after `GetItemstack`. A fitted die read back off the save tree carries no resolved
  `Collectible`, so its `die` attribute is unreadable; the mill and the hearth both document the trap
  in-source (`BlockEntityRollingMill.cs:440-442`, `BlockEntityHeatingHearth.cs:148-150`).
* Do not key anything on a float. If a die ever keys on a thickness, and the HP gate is exactly a thickness,
  read `RollSetSpec.cs:159-161` first: `0.5` and `"0.50"` never compare equal, and `OutputAt` compares
  floats with `==` (`:95-101`) as the standing example of what not to do.
* The drawn die is a flat die with zero cavities, so it cannot be used to prototype the cavity-count rule.
  See [Assets](#the-flat-die-has-no-cavities-and-the-rule-reads-batch-size-off-cavities).
* No die shears. The shear cuts across; a hammer die cuts out; the bench upsets
  ([steam hammer § Gotchas](../machines/steam-hammer.md), [shear](../machines/shear.md)).
* smex ships no die. smex's forming contribution is roll sets and two mill stands
  ([steel roll sets](../machines/steel-roll-sets.md)). The hardened die tier is smex-and-above material, but
  the die items belong to iiex and hpex.

---

## Open

| # | Question | Notes |
|---|---|---|
| 1 | What is `Bench`'s value set? | blocking the first die item. `heading` / `nail` cannot name the hammer or the HP hammer, and the ≥ 2-voxel gate has no field of its own |
| 2 | Where does the spec type live? | iiex (precedent, `MoldSpec.cs:6`) or exlib (three downstream consumers). Answer it once for `RollSetSpec`, `MoldSpec` and `ItemDie` together - [roll sets § Open 6](roll-sets.md), [patterns § Open 9](patterns.md) |
| 3 | Does anything validate that a die conserves mass? | the invariant is real, four of five dies satisfy it, and no mechanism exists. It would be the first consumer of `materialUnits` in the codebase |
| 4 | Cavity count vs `Count` - which is authoritative, and what does a zero-cavity (flat) die mean? | the drawn asset forces the question |
| 5 | Ruled 2026-08-05: a die wears. | [tooling-wear.md](../mechanics/tooling-wear.md) owns the rule for the whole family (dies, roll sets, blade sets, boring bits, patterns) and the metal grade sets the life - which is what makes `STATE.md` D9's "longer-lasting machine heads" mean something. The [pattern](patterns.md)'s 24 impressions become the family's first instance. Only the numbers are still open |
| 6 | What is a "bolt" as an item - one, a bundle, or a `bolts-and-nuts` composite | vanilla's `metalnailsandstrips` argues for a bundle, and the count then fixes the mass ([heading machine § Open](../machines/heading-machine.md)) |
| 7 | A ball's mass | the only ledger row with no number, and therefore the first place the mint invariant can break ([bearings](../machines/bearings.md)) |
| 8 | Art for five dies, plus exporting the one that is drawn | and do not ship them on a shared placeholder the way the roll sets were |
| 9 | Nothing feeds any die. | `nailplate`, `rolledrod`, `boilerplate`, the bolt, the rivet, the ball and chrome steel all do not exist; three of the four machines do not exist; the mill cannot roll at all (B3/B4) |
