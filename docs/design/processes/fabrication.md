# Fabrication (the cast ↔ fabricated substitution)

**Status** designed - nothing built, and three of the five pairs have neither half. Of the five cast
originals only `castplate-heavy` and `cast-barrel` exist as items
(`CastPartItemDefinitions.cs:31-42`, `:44-54`); `castframe`, `castshell` and `castwheelsection` exist
nowhere in `src/`. No fabricated substitute exists for any of them, no rolled plate, beam or rivet exists to
build one from, and the RCC machinery cannot currently express "either half" at all - see
[Numbers](#numbers).
**Mods** iwex owns the cast originals and the heading machine · lpex owns the
[bending roller](../machines/bending-roller.md), the rivet die and the plate · smex supplies the cheap
steel · hpex is the first consumer that has no choice

## Owns

* the substitution loop itself - every cast structural part has a rolled/fabricated steel twin, the two are
  a pair with one consumer, and neither ever obsoletes the other (D2 / N3);
* the two-operation reduction: every substitute is bend and/or assemble, and only bending needs a machine;
* the bill grammar - `beam` + plate + rivets - and the rule that rivets are an ingredient, which removes the
  need for a riveting machine;
* the dual-path requirement a consuming recipe must satisfy, and the finding that the shipped
  `ConstructionStage.Require` cannot express it as written;
* the steel ladder stated as a guarantee ladder rather than a tier ladder, and structural fabrication as
  cheap Bessemer steel's sink;
* the state of both halves of every pair, and the cast plate's mass ruling (500 u at 10 × 2 × 10, D2
  re-affirmed 2026-08-07 - see [Numbers](#numbers)).

## Does not own — cited only, never restated

| Fact | Owner |
|---|---|
| cast iron takes compression, wrought takes tension, anything that must fit is machined - the rule that decides which parts exist at all - and the six-item part catalogue | [cast parts](../items/cast-parts.md) |
| the casting stations, the `mold` / `MoldSpec` schema, patterns, ram / imprint / shake-out | [casting cell](../machines/casting-cell.md), [long cell](../machines/long-cell.md) |
| bending - the machine, the pass model, cold work, the four tooling routes, and the no-welding-verb rule | [bending](bending.md), [bending roller](../machines/bending-roller.md) |
| the schedules that produce `beam`, plate, `heavyplate` and `boilerplate` | [rolling](rolling.md) |
| the rivet - the bench, the `ItemDie` contract, the nail/bolt/rivet/ball die split, and why the rivet die is lpex's | [heading machine](../machines/heading-machine.md) |
| the converter, blown iron, mandatory recarburisation, ferroalloys, the ladle | [bessemer](../machines/bessemer.md), [open hearth](../machines/open-hearth.md), [ladle](../machines/ladle.md), [cupola](../machines/cupola.md) |
| `1 vx³ = 2.5 u` and the measured mass of every shipped part | [density rule](../mechanics/density-rule.md) |
| RCC construction stages, grid recipes, `ExRecipeCosts`, goldens | [recipes & config](../mechanics/recipes-config.md) |
| the HP hammer, and why its frame cannot be cast | [HP hammer](../machines/hp-hammer.md) |
| the placement rule that decides which mod ships which half | [STATE.md](../../plans/STATE.md) |

**Depends on** [cast parts](../items/cast-parts.md) · [bending](bending.md) · [rolling](rolling.md) ·
[heading machine](../machines/heading-machine.md) · [casting cell](../machines/casting-cell.md) ·
[long cell](../machines/long-cell.md) · [bessemer](../machines/bessemer.md) ·
[recipes & config](../mechanics/recipes-config.md) · [density rule](../mechanics/density-rule.md)

---

## What it is

Building a machine part out of rolled sections riveted together instead of pouring it in one piece. A
19th-century machine frame started as ribbed castings bolted to plain panel castings; once cheap mild steel
and a plate mill existed, the same frame could be a riveted plate girder - beams, plate, and a few hundred
rivets. Two routes to the same part, chosen by which line the player has built.

> Cast when you have a cupola. Fabricate when you have a mill.

What the mod abstracts away:

| Real practice | Here | Why |
|---|---|---|
| drilling and reaming the rivet holes, hot-driving each rivet, caulking the seam | rivets are an ingredient; the joint is a line in the bill | a riveting minigame is a fifth verb with no machine behind it |
| plate girders, box girders, lattice, angle stiffeners - a whole structural vocabulary | two shapes only: `castframe` the beam, `castshell` the panel | "no new part item unless two machines use it, or it is a machine's signature" ([cast parts](../items/cast-parts.md)) |
| stress analysis deciding which route a given part may take | one physical rule: compression may be cast, tension may not | it decides every part in the catalogue and needs no table |
| welded fabrication (1930s onward) | absent - the period is riveted | there is no welding verb anywhere in the suite ([bending](bending.md)) |
| pattern-making, coring, dressing on the cast side | owned by [casting cell](../machines/casting-cell.md) | not this page's half of the pair |

### The pair rule

> Every cast structural part gets a rolled/fabricated steel equivalent, and both feed the same consumer.

N3, settled 2026-07-29. Not a replacement and not an upgrade path: a machine's bill asks for the part, and
either half satisfies it. Which one the player makes is an infrastructure decision. Neither half is ever
obsoleted, the same pattern that keeps the helve alive beside the steam hammer, the pig beds alive beside
direct charging, and every stand in the mill train in service.

### It is what gives cheap Bessemer steel its job

The steel ladder is settled by what the product must guarantee, not by date:

| Process | Guarantees | Therefore serves |
|---|---|---|
| Bessemer | volume, and nothing else | structural fabrication - beams, plate, shells, frames |
| open hearth | pressure grades and alloys | boilers, pressure parts, hadfield |
| crucible (iwex, Huntsman 1740) | purity | tools and weapons |
| chrome steel | hardness and accuracy | bearings and files |
| HSS | hot hardness | endgame cutting |

Bessemer steel is fast, cheap and unreliable, so it needs a sink that does not care. A riveted frame does not
hold pressure, does not take an edge and does not need a grade; it needs to be big and there needs to be a
lot of it.

Alloys inherit their base's grade as a continuous penalty (a lower maximum pressure), not a lockout, so a
poor Bessemer heat is never blocked from anything - it is only worse at the one thing structural work does
not ask for.

### And hpex makes it mandatory rather than optional

The LP [steam hammer](../machines/steam-hammer.md)'s frame is cast iron and the load is pure compression:
steam lifts, gravity drops, the standards are only ever squeezed. The HP double-action hammer drives the ram
down under steam, which puts the standards in tension.

> A cast frame snaps. The HP hammer is the first machine in the suite whose frame cannot be cast.

N3 gets its first hard consumer the moment hpex exists ([HP hammer](../machines/hp-hammer.md)).

---

## The loop

```
Bessemer ──▶ ladle ──▶ recarburise ──▶ long cell ──▶ castslab 3000 u
                                                          │
                                                 rolling (wide train)
                                                          │
                      ┌───────────────────────────────────┼──────────────────┐
                      ▼                                   ▼                  ▼
                 heavyplate 600                     boilerplate 600      beam 200  (narrow flat)
                      │                                   │                  │
                      │                          bending roller              │
                      │                                   │                  │
                      └──────────────┬────────────────────┴──────────────────┘
                                     │        + rivets  (heading machine, lpex die)
                                     ▼
                     recipe / RCC stage ──▶ fabricated castframe · castshell ·
                                            cast-barrel · castwheelsection · castplate
                                     │
                                     ▼
                    a machine bill that accepts either half
```

| # | Where | Player verb | What comes out |
|---|---|---|---|
| 1 | [bessemer](../machines/bessemer.md) → [ladle](../machines/ladle.md) | blow, then add the recarburiser - an FeMn trim, or a spiegeleisen dose ([recarburising](recarburising.md)) | steel - recarburisation is mandatory; blown iron is not steel |
| 2 | [long cell](../machines/long-cell.md) | pour | `castslab` / `castbloom` / `castbillet` |
| 3 | [rolling](rolling.md) | the wide train, or the narrow `flat` set | `heavyplate` · `boilerplate` · `beam` · plate |
| 4 | [bending roller](../machines/bending-roller.md) | multi-pass, cold - only for the curved parts | a shell, a barrel, a rim |
| 5 | [heading machine](../machines/heading-machine.md) + lpex's rivet die | one 25 u rod per blow | rivets |
| 6 | a grid recipe or an RCC stage | assemble | the fabricated part |
| 7 | any machine that wants the part | build | the bill must accept either half - see [Numbers](#numbers) |

Only step 4 needs a machine that does not already exist for other reasons; steps 3, 5 and 6 are the mill, a
bench and a recipe.

Steps 4 and 5 are both lpex, so an iwex-only player can fabricate nothing that needs a rivet or a curve.
Nothing at iron tier bends and rivets arrive with the first thing that holds pressure, so fabrication is a
steam-tier process throughout, not a steel-tier one; only the volume that makes it worth doing is smex's.

---

## Inputs and outputs

### The five pairs

| Part | Cast original | Fabricated from | Operations | Cast half exists? | Fabricated half exists? |
|---|---|---|---|---|---|
| `castplate` | sand cell, cast iron - settled 10 × 2 × 10 = 500 u | rolled plate (`heavyplate` 12 × 2 × 10 = 600 u) | — (the mill alone) | ships as `castplate-heavy` @ 160 u (`CastPartItemDefinitions.cs:21`) - the constant follows the ruling | no - `heavyplate` the item does not exist |
| `castframe` | long cell (the beam / I-section) | `beam` × N + plate + rivets | assemble - a riveted plate girder | no - no item, no `MoldSpec`, no long cell | no - `beam` does not exist |
| `castshell` | sand cell (the panel) | plate + rivets | bend + rivet the seam | no item - but `casting/cell-filling-castshell.json` is drawn and orphaned | no |
| `cast-barrel` | sand cell (a cored vessel) | plate + rivets | bend + rivet | yes - ships @ 200 u (`CastPartItemDefinitions.cs:24`) | no - and the code `cast-barrel` is already taken |
| `castwheelsection` | sand cell (flywheel segment) | bent rim + bar spokes + hub + rivets | bend + assemble | no - no item; `cell-filling-flywheelpart.json` drawn and orphaned | no |

### The bill grammar

Three ingredients cover every fabricated part:

| Ingredient | Section × length | Mass | From |
|---|---|---|---|
| `beam` | 4.5 × 2 × 9 | 200 u | narrow `flat` 2.0, cropped in half ([rolling](rolling.md)) |
| plate - `game:metalplate` · `heavyplate` · `boilerplate` | 9 × 1 × 9 · 12 × 2 × 10 · 15 × 1 × 16 | 200 · 600 · 600 u | the mill; the wide ones need [the hall](../machines/wide-hall.md) |
| rivets | — | undefined | one 25 u rod per rivet at the [heading machine](../machines/heading-machine.md), wearing lpex's rivet die |

`beam` is the panel's opposite number: `castframe` is the beam (carries load in one direction - standards,
beds, housings), `castshell` is the panel (a wall section - cistern, crusher casing). Between them they build
any machine body without a bespoke casting per machine, which is the "no new part item" rule doing its job.
The fabricated route reproduces the same vocabulary out of rolled sections.

---

## Numbers

### The dual path is not expressible with the shipped RCC code

`ConstructionStage.Require` takes one `code`, optionally with a variant placeholder and an
`allowedVariants` filter (`ExpandedLib/Definitions/ConstructionStages.cs:73-99`). There is no alternation:
a stage cannot say "a cast frame or a fabricated frame" unless the two resolve through one code.

| Route | Cost | Note |
|---|---|---|
| make the pair two variants of one code - e.g. `frame-{cast,fabricated}` - and require the wildcard with `allowedVariants` | zero new code | exactly the shape `RequireMetal` already uses for `iron` / `steel` (`ConstructionStages.cs:120-132`), including `storeWildCard` so later stages and the drops resolve to the same half |
| add alternation to `Require` | new exlib feature, and every consumer of the JSON has to understand it | only worth it if the pair genuinely cannot share a code |

This decides the item naming, and D2 has already pushed the pair apart: the cast plate is `castplate` and the
rolled one is `heavyplate`, which are not variants of anything. Decide the mechanism before authoring any of
the five pairs, or the dual path becomes ten separate recipes.

### The cast plate's mass — ruled 2026-08-07, D2 re-affirmed

| Source | Geometry | vx³ | Mass | file |
|---|---|---|---|---|
| settled (D2, re-affirmed 2026-08-07) | 10 × 2 × 10 | 200 | 500 u | — |
| the C# constant | — | — | 160 u - follows | `CastPartItemDefinitions.cs:21` |
| the shipped runtime shape | 12 × 2 × 12 | 288 | 720 u by the rule - follows | `assets/iwex/shapes/item/heavyplate.json` |
| the newly drawn editable shape | 8 × 2 × 8 | 128 | 320 u by the rule - follows | `assets/editable/shapes/item-castplate.json` (untracked) |
| the casting cavity box | `Box(7,4,4, 9,14,12)` = 2 × 10 × 8 | 160 | 160 u at an implicit 1 u/vx³ - follows | `PatternItemDefinitions.cs:73-78` |

500 u is canonical and 10 × 2 × 10 is the canonical geometry (ruled 2026-08-07). The three dissenting copies
and the cavity all follow: code and goldens land in the settled-economy batch
([economy landing](../items/economy-landing.md)), and the art redraw to 10 × 2 × 10 is owed as the
maintainer's hand-work. The move touches the cavity, the recipe costs and every machine bill that consumes
the plate ([density rule](../mechanics/density-rule.md) § Open 3), which is why it lands as a batch.

| Other shipped masses this process touches | Value | file:line |
|---|---|---|
| `cast-barrel` | 200 u | `CastPartItemDefinitions.cs:24` |
| `MaterialDensity` on the cast parts | 7200 kg/m³ | `CastPartItemDefinitions.cs:38`, `:52` - unrelated to the unit rule; do not reconcile |
| the `materialUnits` attribute | written on every item, never read by any mod code | `CastPartItemDefinitions.cs:40`, `:53` |

### Unchosen — and it is the whole balance question

| Quantity | Note |
|---|---|
| plates per `castshell` · per `cast-barrel` · per `castwheelsection` | decides whether fabricating is cheaper or dearer than casting, which is the entire point of D2 |
| rivets per part | ditto |
| `beam` count per `castframe` | ditto |
| bending passes per shell | [bending](bending.md) - and it is an input to all three rows above |
| what a rivet is - an item, a bundle, a stack size | the heading machine's die `count` is 1 per 25 u rod ([heading machine](../machines/heading-machine.md)), so 100 u of rod is 4 rivets. Whether a recipe asks for 4 or 400 is unanswered |

"Cast when you have a cupola, fabricate when you have a mill" only works if the two routes cost comparably.
Nothing anywhere has costed either.

---

## Why it is like this

One physical rule generates the entire catalogue. Cast iron is rigid and strong in compression and shatters
under shock or pull; wrought and steel are tough and survive reversal. Frames, beds, cylinders and flywheels
are cast; rods, cranks, straps and bolts are forged. The player has to run both branches - the casting line
and the wrought/steel line - because neither alone builds an engine.

Rivets as an ingredient collapses joining into a line in a bill, and makes the fastener mean something at the
same time: nails and bolts are strong, rivets are strong and tight, which is why a boiler is riveted and a
flywheel is bolted. A powered riveter stays available later as a pure throughput upgrade; nothing is blocked
without one.

Four of the five substitutes reduce to bend and assemble; `castplate` needs neither. The one block N3 costs
is the one the pipe route, the boiler shell and the wheel rim were already asking for
([bending roller](../machines/bending-roller.md)).

It closes the last open consumer question. `boilerplate` was left open "to accrete uses as features land":
a rolled shell is what a boiler barrel is, and the same plate becomes machine shells, barrels, pipe and wheel
rims.

A cupola player keeps casting; a mill player fabricates; an hpex player must fabricate one specific frame and
may still cast everything else.

---

## Gotchas

* Three of the five cast originals do not exist as items at all, so three pairs have neither half.
  `PatternItemDefinitions.Molds` declares exactly four molds - heavy plate, mold plate, double-ingot mold and
  cast barrel (`PatternItemDefinitions.cs:71-104`) - and `castframe`, `castshell` and `castwheelsection` are
  none of them.
* `ConstructionStage.Require` cannot express "either half" (`ConstructionStages.cs:73-99`). The dual path the
  RCC machinery does already express is the metal-variant wildcard, which is a different thing. See
  [Numbers](#numbers).
* The cast plate's four-mass disagreement is ruled (2026-08-07): 500 u at 10 × 2 × 10 is canonical, and the
  code constant, the shipped shape and the editable art all follow via the
  [economy landing](../items/economy-landing.md) batch. Until that batch lands, the tree still says 160 / 720
  / 320 - read the ruling, not the files.
* `cast-barrel` is already an item code (`CastPartItemDefinitions.cs:46`). The fabricated substitute needs
  its own code, or the pair needs the shared-variant naming that the dual path wants anyway - the two
  problems have one answer.
* Six of the twelve shipped `cell-filling-*` shapes are orphans - `axle`, `castshell`, `cylinder`,
  `flywheelpart`, `gearblanklarge`, `gearblanksmall`. Only `heavyplate`, `plate`, `doubleingot` and
  `moltenbarrel` are named by a `MoldSpec`, plus `base` and `half` for the ram states
  (`CastingCellLogic.cs:128`, `:132`). The art for `castshell` - the pair this page most needs - is drawn and
  wired to nothing.
* The long cell, which is what would cast `castframe`, does not exist. Seven shapes are drawn (including
  `longcell-filling-castframe.json`) and the only code is one `MoldSize` enum member
  ([long cell](../machines/long-cell.md)).
* Fabrication is gated on lpex end to end - the bend is lpex's machine and the rivet is lpex's die - so
  "cast vs fabricate" is never a choice an iwex-only player makes. That is consistent with the placement
  rule, but it means the iron tier's structural parts have exactly one route and the "two routes" framing
  does not begin until steam.
* Neither the balance nor the costs exist, so the design's own central claim (cast when you have a cupola,
  fabricate when you have a mill) is currently unfalsifiable.
* The `materialUnits` attribute is dead data. Every machine reads the C# constant; changing a mass means
  changing `CastPartItemDefinitions`, not the JSON.

---

## Open

| # | Question | Weight |
|---|---|---|
| 1 | How is the dual path expressed? Shared code + `allowedVariants` (free) or new alternation support (a feature). This decides the naming of all five pairs and must be settled first | high |
| 2 | Closed 2026-08-07 - a cast plate is 500 u at 10 × 2 × 10 (D2 re-affirmed). Code + goldens move in the [economy landing](../items/economy-landing.md) batch; the art redraw is owed. Everything downstream - cavity, recipe costs, machine bills - moves with it | closed |
| 3 | The balance: plates and rivets per substitute, against the cast original's cupola cost | high |
| 4 | Three cast originals must be built before their substitutes mean anything - `castframe`, `castshell`, `castwheelsection`, plus the long cell that casts the frame | medium |
| 5 | What is a rivet, as an item? Count per part, stack size, whether it bundles. The heading machine's die yields 1 per 25 u rod | medium |
| 6 | Does the substitution reach beyond the five? Cylinders, gear blanks and axles are cast and have orphan art; nothing says whether they get twins or stay cast-only | low |
| 7 | Is the fabricated half allowed to be better? A riveted steel frame is genuinely stronger than a cast one. Today the pair is interchangeable; if fabricated parts ever gain a property, D2's "real choice" becomes a straight upgrade and the never-obsolete pattern breaks | low |
| 8 | Does the HP hammer's frame get a cast option at all? If not, it is the first bill in the suite that is not dual-path - which is a rule exception and should be stated as one | low |
