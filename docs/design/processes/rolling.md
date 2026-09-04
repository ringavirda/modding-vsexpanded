# Rolling

**Status** partly built - the reduction simulator and the **two-round model** are live and pinned, the
three entry blockers (B3 / B4 / B17) are closed, and stock can be walked down a ladder in creative. What is
still missing is both ends: nothing produces stock, and the crop station that ends a schedule does not
exist, so not one product item in the ladder below has been written.
**Mods** iiex (the mill block, the narrow `flat` and `grooved` sets, the reheat furnace, the shear) ·
iiex (the wide roll sets and the four-stand hall) · smex (cast stock, two more stands, steel sets)

## Owns

* the player's walk - heat, carry, fit, feed, half-step, gap, carry back or step down the train, crop - and
  that a schedule is a sequence of walks, not a sequence of clicks;
* the feed arithmetic: a round is one feed per side, `sides = ceil(entryWidth / barrelWidth)`, a gap is
  two rounds, round 1 lands the half-step and round 2 lands the gap;
* the section law as the ladder's arithmetic - square sections keep `w = t` and put everything into
  length; flat sections hold length and put everything into width - both readable off the drawn stage art;
* the full schedules, end to end: which stock, on which set, how many feeds, what geometry each stage
  lands on, where the crop falls and what comes off it;
* the narrow-vs-wide trade stated as one number - 12 feeds for 2 plates against 28 feeds + 5 stamps for
  15 - and the third row nobody has written down (the wrought slab at 8 feeds for 6);
* the half-step as a legal stopping place;
* the arithmetic disagreements between the settled ladder, the drawn art and the code, listed in
  [Gotchas](#gotchas).

## Does not own — cited only, never restated

| Fact | Owner |
|---|---|
| the mill block, its footprint, the pass lifecycle, `RollingPass`'s physics (`δ_max = μ²R`, spread, elongation, cooling), the `rollset` spec format, `WorkPiece`, `StockForm`, every `Rolling*` config key, its blockers and gotchas | [rolling mill](../machines/rolling-mill.md) |
| the hall as a build, the six single-gap wide sets, the shared-shaft mechanics, the hall's power profile | [wide hall](../machines/wide-hall.md) |
| the two extra smex gaps (3.5 / 3.0) and the `MinTorque` tier | [steel roll sets](../machines/steel-roll-sets.md) |
| the crop station - its block, verbs, drops, the `Outputs`-keys-on-stage move, the cold-cut torque gate, and the crop-not-convert rule as a rule | [shear](../machines/shear.md) |
| the hearth, its seatings, the reheat rate law and the `V/A` table | [reheat furnace](../machines/reheat-furnace.md), [heat balance](../mechanics/heat-balance.md) |
| the ≤ 32 / ≤ 48 invariant, its two escapes, the definition of a soft-lock and the two mandatory crop points | [recoverability](../mechanics/recoverability.md) |
| `1 vx³ = 2.5 u` and the measured mass of every shipped item | [density rule](../mechanics/density-rule.md) |
| the energy model, `LoadTorque`, every `Mp*` key, the flywheel | [mp-energy](../mechanics/mp-energy.md), [flywheel & shafting](../machines/flywheel-and-shafting.md) |
| where the wrought stock comes from | [puddling furnace](../machines/puddling-furnace.md), [steam hammer](../machines/steam-hammer.md) |
| where the cast stock comes from | [long cell](../machines/long-cell.md), [casting cell](../machines/casting-cell.md) |
| what happens to the plate afterwards | [stamping](stamping.md), [bending](bending.md), [fabrication](fabrication.md) |
| what happens to the rod and the nail plate afterwards | [heading machine](../machines/heading-machine.md), [nail machine](../machines/nail-machine.md) |
| code-first defs, recipes, the cost catalogue, goldens | [recipes & config](../mechanics/recipes-config.md) |

**Depends on** [rolling mill](../machines/rolling-mill.md) · [wide hall](../machines/wide-hall.md) ·
[steel roll sets](../machines/steel-roll-sets.md) · [shear](../machines/shear.md) ·
[reheat furnace](../machines/reheat-furnace.md) · [recoverability](../mechanics/recoverability.md) ·
[density rule](../mechanics/density-rule.md) · [mp-energy](../mechanics/mp-energy.md) ·
[stamping](stamping.md) · [bending](bending.md)

---

## What it is

**Hot rolling on a two-high stand.** A piece of iron is heated past 900 °C, passed between two rolls set a
fixed distance apart, and comes out thinner and longer. Cort's 1783–84 pairing of the puddling furnace with
grooved rolls is the historical anchor: the mill is a hot wrought mill, and cast iron cannot go through it
at all - it shatters.

What the mod abstracts away:

| Real practice | Here | Why |
|---|---|---|
| a screw-down that re-gaps the stand between passes | no screw-down; the barrel is cut with a fixed sequence of gaps and the stock walks along it | a heavy manual two-high stand was not re-gapped mid-schedule. It makes the schedule geometry rather than a setting |
| roughing train, then a finishing train, then a shear line | one machine per verb - the mill only reduces, the [shear](../machines/shear.md) only crops, the hammer only forms | one station per verb is why none needs a mode switch |
| camber, crown, edging passes, side guards | none - a piece is a section and a length | the two-round rule already buys the "turn it over" beat that camber would have justified |
| a scheduler telling you the next pass | nothing enforces the order | a gap too deep for `δ_max` simply skids, so physics is the schedule ([rolling mill](../machines/rolling-mill.md) § the pass) |
| separate roughing and finishing crews | one player, one pair of tongs | handling is the cost; see [the trade](#the-trade-that-justifies-the-hall) |
| water-cooled rolls, roll wear, roll changes | rolls never wear | `MinTorque` is the only tooling property with teeth, and it is not read yet |

---

## The loop

```
reheat hearth ──▶ carry ──▶ [ fit set ] ──▶ feed round 1 (half-step) ──▶ carry back / step down
      ▲                                     feed round 2 (gap)       ──▶ carry back / step down
      └──────────── too cold ◀──────────────────┘                              │
                                                                  crop at the shear
                                                                               │
                                                     product  +  the remainder, still stock
```

| # | Where | Player verb | What comes out |
|---|---|---|---|
| 1 | [reheat furnace](../machines/reheat-furnace.md) | RMB a hearth row (≤ 32 lengthwise, 3 slots) or the top filler (≤ 48 crosswise, 2 slots, far row first) | a piece above `RollingTempC` |
| 2 | — | walk to the mill | a piece that is already cooling; the heat is a clock from the moment it leaves the bed |
| 3 | [rolling mill](../machines/rolling-mill.md) | RMB holding a roll set | the set is fitted; the previous one is handed back. Once per schedule on a single mill; never on a hall stand |
| 4 | the input deck | RMB = the near strip · sneak + RMB = the far strip. Where along the deck the click lands picks the gap band | round 1 of one side |
| 5 | — | walk around the stand (one mill) or along the line (a hall) | the piece, one side reduced to the half-step |
| 6 | the input deck | repeat until every side has had round 1, then again for round 2 | the piece at the gap, evenly |
| 7 | — | repeat 4–6 per gap; go back to 1 whenever the mill answers `TooCold` | the piece at the stage you want |
| 8 | [shear](../machines/shear.md) | RMB on the throat with the stock | one product, and the remainder still on the deck as stock |

The claim gesture is at the shear and nowhere else. The mill only ever makes stock: `CompletePass`
writes the thinned piece back onto the same stack and ejects it (`BlockEntityRollingMill.cs:282-288`). A
schedule ends when the player decides it does.

### Feed arithmetic — the one rule the whole page is built on

| Term | Definition | Where |
|---|---|---|
| **feed** | one trip through the rolls, of one side of the piece | `WorkPiece.Feed` (`WorkPiece.cs:97`) |
| **sides** | `ceil(entryWidth / barrelWidth)`, recomputed for this barrel at every feed | `WorkPiece.SidesFor` (`WorkPiece.cs:52`) |
| **round** | one feed per side; the gauge moves only when the last side lands | `WorkPiece.Fed` |
| **gap** | two rounds - round 1 lands the half-step, round 2 lands the gap | `WorkPiece.RoundTarget` (`:87`) |
| **feeds per gap** | `2 × sides` | `WorkPiece.PassesForGap` (`WorkPiece.cs:58`) |

The half-step is a legal stage to leave a piece in, it is what the stage art already draws (`.75` and `.25`
stages, [below](#the-drawn-stage-art-is-the-schedule)), and the cast billet's mandatory crop lands on one -
which is why the shear must key on stage, not on gap ([recoverability](../mechanics/recoverability.md),
[shear](../machines/shear.md)).

**Built 2026-08-12.** The piece carries one gauge, the gap it is half way through and a flag per side; the
gap it is half way through is what tells round 2 from round 1, and a piece is therefore never lopsided. How
that state is held is [rolling mill](../machines/rolling-mill.md)'s.

★★ **Every feed count on this page is a cost, not an upper bound** *(built 2026-08-12)*. Halving the bite
briefly doubled how far `δ_max` reached, so a bar could be walked to plate in two feeds against twelve.
`δ_max` is now **0.36** - above one round's 0.25 draft and below one gap's 0.5 - so the next rung is the
only rung that bites and the schedules below are what a player actually pays. Nothing checks the order; the
friction limit is the whole rule.

### The section law

Two laws, chosen by the roll set's section class, not by the stock:

| Class | Rule | Consequence |
|---|---|---|
| **square** (grooved) | `w = t`; the groove refuses the spread | `L(t) = V / t²` - length grows as the square |
| **flat** | length is fixed; everything goes into width | `w(t) = V / (L·t)`, until `MaxWidth`; past the cap, length grows again |

The section class also decides how a piece stacks on the hearth bed - square sections nest into pyramids,
flat sections stack.

Caution: neither law is in code. `RollingPass.SpreadWidth` (`:146`) uses a per-form exponent
(`StockForm.ShingledBar` 0.846, `StockForm.ShingledSlab` 0.463 - `StockForm.cs:48`, `:60`) and
`RollingPass.LengthMultiplier` (`:168`) puts the balance into length. Every table below is the settled
law; the shipped simulator does not reproduce it.

---

## Inputs and outputs

Masses are the [density rule](../mechanics/density-rule.md)'s (`1 vx³ = 2.5 u`); the crops that are
mandatory are [recoverability](../mechanics/recoverability.md)'s.

### Stock

| Stock | Section × length | vx³ | Mass | Cast/forged by | Mod |
|---|---|---|---|---|---|
| `shingledbar` | 3 × 3 × 18 | 162 | 400 | 2 wrought balls on the helve | iiex |
| `shingledslab` | 8 × 3 × 20 | 480 | 1200 | 6 wrought balls, [steam hammer](../machines/steam-hammer.md) only | iiex |
| `castbillet` | 3 × 3 × 27 | 243 | 600 | [long cell](../machines/long-cell.md), 3 lanes | smex |
| `castbloom` | 4 × 4 × 25 | 400 | 1000 | long cell, 2 lanes | smex |
| `castslab` | 12 × 4 × 25 | 1200 | 3000 | long cell, 1 lane | smex |

162 × 2.5 = 405 and 243 × 2.5 = 607.5; the ladder rounds to 400 and 600 because those divide
(400 / 4 = 100, 600 / 6 = 100). See [density rule](../mechanics/density-rule.md) § Rounding; the ≈ 1 % the
rounding leaves is absorbed by crop-not-convert.

### Products

| Product | Section × length | vx³ | Mass | Rolled from | Goes to |
|---|---|---|---|---|---|
| `rolledrod` | 2 × 2 × 10 | 40 | 100 | grooved 2.0 | the fork, below |
| rod @ 25 u | 1 × 1 × 10 | 10 | 25 | grooved 1.0, 4 per `rolledrod` | [heading machine](../machines/heading-machine.md) → bolts (iiex) / rivets (iiex die) |
| `nailplate` | 4 × 1 × 10 | 40 | 100 | flat 1.0, 1 per `rolledrod` | [nail machine](../machines/nail-machine.md) → 4 × `game:metalnailsandstrips` |
| `beam` | 4.5 × 2 × 9 | 81 | 200 | flat 2.0, cropped in half | the Watt engine's beam; `castframe` ([fabrication](fabrication.md)) |
| `game:metalplate` | 9 × 1 × 9 | 81 | 200 | flat 1.0 | universal |
| `blank` | 8 × 2 × 5 | 80 | 200 | wide 2.0 off `castbloom` | the boring machine |
| `skelp` | 8 × 1 × 10 | 80 | 200 | wide 1.0 off `castbloom` | [bending](bending.md) → rolled pipe |
| `heavyplate` | 12 × 2 × 10 | 240 | 600 | wide 2.0 off either slab | rolled steel - a different item from the cast `castplate` ([fabrication](fabrication.md)) |
| `boilerplate` | 15 × 1 × 16 | 240 | 600 | wide 1.0 off either slab | boiler shells · [stamping](stamping.md) · [bending](bending.md) |

Not one of the **products** exists in `src/`. The two wrought **stocks** do, at the settled names and
masses since 2026-08-12: `stock-shingledbar` (400 u) and `stock-shingledslab` (1200 u)
(`StockItemDefinitions.cs:23-26`). The roll sets no longer name a product at all, so the four dangling
output codes are gone with them.

---

## Numbers

### The drawn stage art *is* the schedule

Each top-level element is one stage; a `.75` / `.25` name is a half-step. Measured off the files (untracked
in git):

| File | Elements | Reads as |
|---|---|---|
| `assets/editable/shapes/item-shingled-bar.json` | `ShingledBar1` 3 × 3 × 9 (+ one 9-long child = 18) · `Grooved275` 2.75² × 11 (×2 = 22) · `Grooved250` 2.5² × 13 (×2 = 26) · `Grooved225` 2.25² × 16 (×2 = 32) · `CutRod1..4` 2 × 2 × 10 · `Flattened275` 3.25 × 2.75 × 9 · `Flattened250` 3.6 × 2.5 × 9 · `Flattened225` 4.0 × 2.25 × 9 · `Beam` 4.5 × 2 × 9 | both narrow schedules of the wrought bar, complete |
| `item-beam-rolled.json` | `Beam` 4.5 × 2 × 9 · `Flattened175` 5.1 × 1.75 × 9 · `Flattened150` 6.0 × 1.5 × 9 · `Flattened125` 7.2 × 1.25 × 9 · `CutPlate1` + `CutPlate2` 9 × 1 × 9 | the bar's flat schedule continued past the beam, ending on two plates |
| `item-rod-rolled.json` | `RolledRod200` 2 × 2 × 10 · `Grooved175` 1.75² × 13 · `Grooved150` 1.5² × 9 (×2 = 18) · `Grooved125` 1.25² × 13 (×2 = 26) · `CutNailRod1..4` 1 × 1 × 10 | the rod's grooved schedule |
| `item-rod-nail.json` | `NailRod1..4` 1 × 1 × 10 | the four 25 u rods, already cut |
| `item-shingled-slab.json` | `ShingledSlab1` 8 × 3 × 10 (+ one child = 20) | the wrought slab as shingled only - no wide stages drawn |

Every drawn flat stage is 9 long (two 9-long halves = 18) and every drawn grooved stage grows in
length only, so the art already encodes the section law: flat holds length, square holds section. It also
encodes `V` exactly - 3.6 × 2.5 × 18 = 162, 4.5 × 2 × 18 = 162, 9 × 1 × 18 = 162, and
`Grooved225` at 2.25² × 32 = 162.

`CutNailRod1..4` is the rivet/bolt rod, not a nail rod - a rename is owed; the geometry is finished
either way.

### Narrow `flat` — `shingledbar`, barrel **4**

| Gap | Round | Section w × t | Length | Sides | Feeds | Art element | Crop |
|---|---|---|---|---|---|---|---|
| — | as shingled | 3 × 3 | 18 | — | — | `ShingledBar1` | |
| 2.5 | 1 | 3.25 × 2.75 | 18 | 1 | 1 | `Flattened275` | |
| 2.5 | 2 | 3.6 × 2.5 | 18 | 1 | 1 | `Flattened250` | |
| 2.0 | 1 | 4.0 × 2.25 | 18 | 1 | 1 | `Flattened225` | |
| 2.0 | 2 | 4.5 × 2.0 | 18 | 1 | 1 | `Beam` | 2 × `beam` @200 |
| 1.5 | 1 | 5.1 × 1.75 | 18 | 2 | 2 | `Flattened175` | |
| 1.5 | 2 | 6.0 × 1.5 | 18 | 2 | 2 | `Flattened150` | |
| 1.0 | 1 | 7.2 × 1.25 | 18 | 2 | 2 | `Flattened125` | |
| 1.0 | 2 | 9.0 × 1.0 | 18 | 2 | 2 | `CutPlate1/2` | 2 × `game:metalplate` @200 |

12 feeds, 2 plates - 6.0 feeds per plate. The spread costs it: the piece outgrows the 4-wide barrel between
the 2.0 and 1.5 gaps and every gap after that is taken a side at a time.

### Narrow `grooved` — `shingledbar`, barrel 16, `w = t`

| Gap | Round | Section | Length | Feeds | Art | Crop |
|---|---|---|---|---|---|---|
| — | as shingled | 3 × 3 | 18 | — | `ShingledBar1` | |
| 2.5 | 1 | 2.75² | 21.4 | 1 | `Grooved275` (drawn 22) | |
| 2.5 | 2 | 2.5² | 25.9 | 1 | `Grooved250` (drawn 26) | |
| 2.0 | 1 | 2.25² | 32.0 | 1 | `Grooved225` | ← the lengthwise seating limit, on the nose |
| 2.0 | 2 | 2.0² | 40.5 | 1 | `CutRod1..4` | 4 × `rolledrod` @100 |

4 feeds, 4 rods. The bar changes seating mid-schedule: 40.5 is past 32 and inside 48, so the last
stage must be re-laid crosswise. The very first stock in the game teaches both hearth modes
([recoverability](../mechanics/recoverability.md)).

### The fork — one `rolledrod`, two fasteners, same four feeds

| Set | Gaps | Stages | Feeds | Yield |
|---|---|---|---|---|
| **grooved** | 1.5, 1.0 | 1.75² × 13 → 1.5² × 17.8 → 1.25² × 25.6 → 1 × 1 × 40 | 4 | 4 rods @25 → [heading machine](../machines/heading-machine.md) → bolts or rivets |
| **flat** | 1.5, 1.0 | 2.29 × 1.75 → 2.67 × 1.5 → 3.2 × 1.25 → 4 × 1 × 10 | 4 | 1 `nailplate` @100 → [nail machine](../machines/nail-machine.md) → 4 nails-and-strips |

Same four feeds, same 100 u, four fasteners either way - chosen per rod, at the mill. A 400 u bar is four
rods and sixteen fasteners of whichever kind, or a mix.

Nails come from plate, never from rod. A nail made from round rod is the 1870s wire nail; the cut
nail (Reed 1786, Perkins 1795) sheared nail plate. It must not be `game:metalplate` - these are bench
machines fed a narrow strip by hand, and a 9-wide 200 u plate will not go into one.

### `castbillet` on steel `grooved` — the crop that must happen mid-gap

| Gap | Round | Section | Length | Feeds | Note |
|---|---|---|---|---|---|
| — | as cast | 3 × 3 | 27 | — | already 32.1 at its first half-step - cast stock is on the crosswise seating from pass one |
| 2.5 | 1 | 2.75² | 32.1 | 1 | |
| 2.5 | 2 | 2.5² | 38.9 | 1 | |
| 2.0 | 1 | 2.25² | 48.00 | 1 | mandatory crop into 6 - each 8.0 long. Carried to 2.0 it would be 60.75, past the hearth in both seatings |
| 2.0 | 2 | 2.0² | 10.125 each | 6 | 6 × `rolledrod` @100 |

9 feeds, 6 rods - 1.5 feeds per rod, against the wrought bar's 1.0. The cast billet is dearer per rod on the
narrow line because the recoverability crop repeats the last round six times; its advantage is that it is
steel and arrives as a cast, not that it rolls cheaper.

### Wide — `shingledslab` on iiex's four stands (barrel 16, `MaxWidth` **15**)

| Stand | Round | w × t | Length | Feeds | Crop |
|---|---|---|---|---|---|
| — | as shingled | 8 × 3 | 20 | — | |
| 2.5 | 1 / 2 | 8.7 → 9.6 × 2.5 | 20 | 2 | |
| 2.0 | 1 / 2 | 10.7 → 12 × 2.0 | 20 | 2 | 2 × `heavyplate` 12 × 2 × 10 @600 - exact |
| 1.5 | 1 / 2 | 13.7 → 15 (capped) × 1.5 | 21.3 | 2 | |
| 1.0 | 1 / 2 | 15 × 1.0 | 32.0 | 2 | 2 × `boilerplate` 15 × 1 × 16 @600 - exact |

8 feeds → 2 boilerplate → [stamped](stamping.md) into 6 plates.

### Wide — `castslab` on smex's six stands

| Stand | Round | w × t | Length | Feeds | Crop |
|---|---|---|---|---|---|
| — | as cast | 12 × 4 | 25 | — | |
| 3.5 | 1 / 2 | 12.8 → 13.7 × 3.5 | 25 | 2 | |
| 3.0 | 1 / 2 | 14.8 → 15 (capped) × 3.0 | 26.7 | 2 | |
| 2.5 | 1 / 2 | 15 × 2.5 | 32.0 | 2 | |
| 2.0 | 1 / 2 | 15 × 2.0 | 40.0 | 2 | mandatory crop into 5 - each 8.0 long, 240 vx³, 600 u. Carried to 1.5 it would be 53.3, past 48 |
| 1.5 | 1 / 2 | 15 × 1.5 | 10.67 | 10 | |
| 1.0 | 1 / 2 | 15 × 1.0 | 16.0 | 10 | 5 × `boilerplate` 15 × 1 × 16 @600 |

28 feeds → 5 boilerplate → [stamped](stamping.md) into 15 plates.

The crop at 2.0 is a decision, not a step. Each of the five 600 u pieces is either claimed as a
`heavyplate` there and then, or rolled on through 1.5 to 1.0 and claimed as a `boilerplate`. Same crop, two
products, chosen per piece.

### Wide — `castbloom`

| Stand | w × t | Length | Crop |
|---|---|---|---|
| 3.0 | 5.33 × 3.0 | 25 | crop into 5 → each 80 vx³ → narrow `flat` → 5 × `game:metalplate` |
| 2.0 | 8.0 × 2.0 | 25 | crop into 5 → `blank` 8 × 2 × 5 @200 → the boring machine |
| 1.0 | 15 (capped) × 1.0 | 26.7 | intended: 5 × `skelp` 8 × 1 × 10 @200 → [bending](bending.md) |

Caution: the skelp does not fall out of this arithmetic - see [Gotchas](#gotchas).

### The trade that justifies the hall

| Route | Stock | Feeds | Stamps | Plates | Feeds / plate | Metal per schedule |
|---|---|---|---|---|---|---|
| narrow `flat` | `shingledbar` 400 u | 12 | — | 2 | 6.0 | 400 u |
| wide, iiex | `shingledslab` 1200 u | 8 | 2 | 6 | 1.33 | 1200 u |
| wide, smex | `castslab` 3000 u | 28 | 5 | 15 | 1.87 | 3000 u |

Same 200 u a plate in every row; no yield is minted anywhere. The wide route buys handling twice over:
4.5× fewer feeds per plate, and each of those feeds is a step along a line rather than a lap around a stand.

The wrought slab is the most feed-efficient route in the game, not the cast one. The cast slab pays a
mid-schedule crop it cannot avoid and then repeats the last two gaps five times; its win is absolute
volume in one schedule - 3000 u and fifteen plates without touching the hearth more than the heat budget
demands.

### Cited — the numbers that govern all of the above but belong elsewhere

| Number | Value | Owner |
|---|---|---|
| `RollingTempC` - the hot/cold line, and the reheat target | 900 °C (`IiexConfig.cs:490`) | [rolling mill](../machines/rolling-mill.md) |
| `RollingRollRadius` → `δ_max = μ²R` = 1.0 hot, 0.0324 cold | 4 (`IiexConfig.cs:506`); `HotFriction` 0.5 / `ColdFriction` 0.09 (`RollingPass.cs:33`, `:37`) | [rolling mill](../machines/rolling-mill.md) |
| `RollingCoolRate` · `RollingAmbientC` | 0.005 · 20 (`IiexConfig.cs:533`, `:537`) | [rolling mill](../machines/rolling-mill.md) |
| reheat and cooling both on `k·A/V`; no ×2 furnace multiplier | — | [reheat furnace](../machines/reheat-furnace.md) |
| ≤ 32 lengthwise (3 slots) / ≤ 48 crosswise (2 slots, LIFO) | — | [recoverability](../mechanics/recoverability.md) |
| `MaxWidth` 15 on the wide sets, and why 15 beats 16 | — | [wide hall](../machines/wide-hall.md) |
| drive headroom one bridged waterwheel leaves ≈ 0.4 N·m; a hot fresh-bloom pass ≈ 0.338 N·m | `ExlibConfig.cs:86`, `:92`, `:98` | [mp-energy](../mechanics/mp-energy.md) |

---

## Why it is like this

A two-high stand cannot be fed backwards, so every feed is a walk. The piece cools whether or not it is
moving (`BlockEntityRollingMill.cs:341-346`), so the walk is a heat budget:

* the reheat furnace exists because a vanilla forge cannot hold a bloom, let alone a slab;
* the hearth's slot count turns the wait into a pipeline - soak the next piece while rolling this one;
* the hall abolishes the lap: stands on one shaft all face the same way, so the piece moves forward;
* thin stock cools fastest and needs the most sides on a narrow barrel, so the narrow route's last gaps
  are the worst of both, and the wide barrel takes the piece in one bite exactly when the clock is
  tightest.

Every bite in the game is exactly 0.5 deep: `flat` carries 2.5 and no 0.5, which makes both narrow families
identical and every draft uniform. A gap therefore costs the same two rounds everywhere and `δ_max` does not
decide legality - it is the reason the mill is a hot mill. The cost is `game:metalsheet`, a vanilla cladding
block that has no unlock; re-adding 0.5 to the wide family alone would restore one.

The crops are derived, not chosen. There are exactly two mandatory ones in the whole ladder, and each is
the last legal stage before the piece would fall out of both hearth seatings
([recoverability](../mechanics/recoverability.md)). Every other crop is the player deciding a schedule is
finished.

Industrialisation buys labour, never material: 200 u per plate on all three routes; 25 u per nail
against vanilla's own anvil rate; a rolled rod that is `game:rod`'s exact mass and exact geometry. The
exploit question is closed by arithmetic instead of by a cap, which is only possible because
[the density rule](../mechanics/density-rule.md) makes mass a function of geometry.

Capital against labour: six stands, or one stand re-tooled six times. Both stay legal, and whoever does not
build the hall pays in walks. That is why there is no cheap fixed-gap "stand" block
([wide hall](../machines/wide-hall.md)).

---

## Gotchas

* `skelp` 8 × 1 × 10 needs a width cap of exactly 8, and no roll-set barrel supplies one. A `castbloom`
  (400 vx³, base length 25) at the 1.0 gap is `400 / 25 = 16` wide uncapped, `15 × 1 × 26.7` at the settled
  wide cap of 15, and `9 × 1 × 44` on the narrow cap. Under the ruled cap model (settled 2026-08-05, Open 1)
  the 8 comes from the stock form's own cap - `StockForm.MaxWidth`, where it already lives
  (`StockForm.cs:32`). Until that model is built, skelp has no producible geometry even on paper. `blank`
  8 × 2 × 5 is safe either way (the bloom is 8 wide at 2.0 with no cap involved).
* The stage lengths in the settled soft-lock table were computed at `MaxWidth` 16, not 15.
  [recoverability](../mechanics/recoverability.md) gives the `castslab` 37.5 at 2.0 and 50.0 at 1.5,
  and says `shingledslab` "never exceeds 30". At the settled cap of 15 those are 40.0, 53.3 and
  32.0 - each exactly `16/15` larger. The conclusions are unchanged (40 ≤ 48, 53.3 > 48, 32 ≤ 32),
  but three published numbers are 6.25 % low, and the wrought slab's true maximum sits exactly on the
  lengthwise limit rather than comfortably under it.
* "Lands the stock on plate geometry" is a property of the bar, not of the law. Under the flat law the
  terminal width is `V / L`, so only stock with `V / L = 9` finishes 9 wide: the `shingledbar` does
  (162 / 18), a cropped `castbloom` fifth does not (80 / 5 = 16). The claim at `StockForm.cs:44-46` is true
  of one form and reads as though it were general.
* The billet's rod route is 50 % dearer per rod than the bar's (1.5 feeds vs 1.0), because of the
  mandatory crop. Nothing in the docs says so, and a reader following "a cast billet is longer and buys
  proportionally more" would expect the opposite.
* The `heavyplate` cropped off a `castslab` has the wrong section. At the 2.0 stand the slab is
  15 × 2 × 40; five 600 u crops are 15 × 2 × 8 each, while `heavyplate` is canonically 12 × 2 × 10. Mass is
  exact, section is not. Legal under crop-not-convert (the product is a named item, not the leftover
  geometry), but worth stating so nobody "fixes" it.
* `item-shingled-slab.json` draws only the as-shingled stage. Both narrow schedules are fully drawn
  and no wide stage of any stock is, so the entire wide route currently has no stage art.
* ~~The shipped config is not the settled schedule.~~ **It is, as of 2026-08-12**: both narrow families
  walk 2.5 / 2.0 / 1.5 / 1.0 off the stock's ladder, `flat` runs barrel 4, and `slitting` is retired. What is
  still unbuilt from this page is the **section law** - `RollingPass.SpreadWidth` is still the per-form
  exponent, so the widths in the tables above are the law's and the code's are close but not equal.
* Where the click lands along the deck picks the gap, and on a hall stand it carries no information,
  because `MillFeed.GapZone` returns 0 unconditionally for a single-gap set (`MillFeed.cs:62-65`). Two
  different feeding idioms for the same verb; only the narrow one needs teaching.
* ~~A refused offer still mutates the piece.~~ **Fixed 2026-08-12** with the two-round model: the piece is
  written back only once the decision is accepted, so offering it to the wrong barrel no longer clears the
  round it is part way through. Owned by [rolling mill](../machines/rolling-mill.md).

---

## Open

| # | Question | Weight |
|---|---|---|
| 1 | Settled 2026-08-05: the effective width cap is `min(roll-set barrel, stock-form cap)`. It is the only answer serving all three demands at once: the barrel becomes physical (a billet has no intrinsic maximum width, the rolls do), the bar carries a form cap of 9 so its terminal 9 × 1 plate stage is legal, and `skelp` stays capped at 8 - not optional, since skelp is the only feed for the conical roller → rolled pipe, i.e. hpex's 12 atm requirement. Barrel-only kills skelp; form-only makes the barrel decorative. The barrel caps a single pass's bite, not the piece: the narrow flat schedule already rolls a 9-wide piece on a 4-wide barrel by taking it a side at a time (§ above), which is why it is `min()`, not "the barrel wins". This ruling does not close the 50-voxel hole on castbloom's 1.0 stage - that needs a declared mandatory crop mid-gap, which the crop table already expresses for the billet's 2.25 half-step | settled |
| ~~2~~ | ~~Nothing can be rolled at all (B3 / B4 / B17)~~ | **closed** - all three, see [rolling mill § Blockers](../machines/rolling-mill.md#blockers) |
| ~~3~~ | ~~The two-round model is unbuilt~~ | **built 2026-08-12** |
| ~~3b~~ | ~~How far ahead may a player skip?~~ **Ruled and built 2026-08-12: `δ_max` calibrated under one gap** - `HotFriction` 0.5 → 0.3 gives 0.36, so an ordinary 0.25 round bites and a 0.5 skip skids. Every feed count on this page is now an exact cost rather than an upper bound, and `ShippedRollSetTests` guards both halves | done |
| ~~3c~~ | ~~`RollingTorqueScale` is calibrated to nothing~~ **closed 2026-08-13** - the load became a declared state instead of a formula. `RollingLoadTorque` = 0.34 is 85 % of a bridged wheel's headroom by construction, so re-cutting a schedule can no longer move the mill's demand. Cooling remains the only thing that stalls a pass | done |
| 4 | The section law is unbuilt. Every table on this page assumes it | high |
| 5 | The shear does not exist, so no schedule can end. Build it first ([shear](../machines/shear.md)) | high |
| 6 | The reheat cycle does not exist - the hearth holds stock and puts no heat into it, so the whole clock this page is built on does not tick | high |
| 7 | No product item in the ladder exists, and neither does any wide stage art | high |
| 8 | `k`, the `V/A` pacing constant, is unchosen and cannot be settled headlessly | low |
| 9 | Is re-tooling one mill actually playable? Six single-gap sets means six tool swaps and six laps per wide schedule. The claim is tedious, not impossible; unverified in game | low |
| 10 | Does an idling stand cost anything? Six stands currently cost what one costs, which removes the hall's stated power argument ([wide hall](../machines/wide-hall.md)) | low |
